extern alias protobuf;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Reflection.V1Alpha;
using Microsoft.Extensions.Logging;
using protobuf::Google.Protobuf.Reflection;
using protobuf::ProtoBuf.Reflection;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Configuration;
using ServiceConnector.Common;
using ServiceConnector.Common.Extensions;
using ServiceConnector.Common.Interfaces;
using ServiceConnector.Jobs.Extensions;

namespace ServiceConnector.Jobs.Jobs;

public class GrpcRequestJobConfig : BaseJobConfig
{
	public required string Url { get; init; }
	public required string Service { get; init; }
	public required string Method { get; init; }

	public required JsonElement Data { get; set; }
}

[PipelineJob]
public class GrpcRequestJob(
	GrpcRequestJobConfig config,
	TypeBuilder typeBuilder,
	TypeBuilderFromSchema typeBuilderFromSchema,
	IAssemblyBuilderFactory factory,
	ExpressionGeneratorFactory generator,
	PipelineDefinition definition,
	ILogger<GrpcRequestJob> logger
) : BaseJob<GrpcRequestJobConfig, GrpcRequestJobRunner>(config, isAsync: true)
{
	private GrpcChannel _channel = null!;
	private Type _requestType = null!;
	private Type _responseType = null!;

	public Func<PipelineStore, object?> GetData { get; private set; } = null!;
	public Func<object> GetClient { get; private set; } = null!;

	public Func<object, object?, CancellationToken, ValueTask<object?>> Send { get; private set; } = null!;

	public override async Task<Type> Compile(TypesStore types, CancellationToken cancellationToken)
	{
		_channel = GrpcChannel.ForAddress(Config.Url);

		var protoFiles = await GetProtoFiles(cancellationToken);
		var serviceType = BuildService(protoFiles);

		var method = serviceType.GetMethods().First(x => x.Name.StartsWith(Config.Method));
		var requestType = method.GetParameters().First().ParameterType;
		var responseType = method.ReturnType;

		if (requestType.CanTo(typeof(IAsyncEnumerable<>)))
		{
			throw new Exception("Streaming doesnt support");
		}

		if (method.ReturnType.CanTo(typeof(IAsyncEnumerable<>)))
		{
			throw new Exception("Streaming doesnt support");
		}

		if (requestType.CanTo(typeof(CallContext)))
		{
			requestType = typeof(void);
		}

		if (responseType.CanTo(typeof(ValueTask)))
		{
			responseType = typeof(void);
		}

		if (responseType.TryTo(typeof(ValueTask<>), out var valueTask))
		{
			responseType = valueTask.GetGenericArguments().First();
		}

		_requestType = requestType;
		_responseType = responseType;

		GetData = BuildGetData(types).Compile();
		GetClient = BuildGetClient(serviceType).Compile();
		Send = BuildSend(serviceType, method).Compile();

		return _responseType;
	}

	private async Task<RepeatedField<ByteString>> GetProtoFiles(CancellationToken cancellationToken)
	{
		var client = new ServerReflection.ServerReflectionClient(_channel);
		using var call = client.ServerReflectionInfo(cancellationToken: cancellationToken);

		await call.RequestStream.WriteAsync(new ServerReflectionRequest
		{
			FileContainingSymbol = Config.Service,
		}, cancellationToken);

		await call.ResponseStream.MoveNext(cancellationToken);
		var response = call.ResponseStream.Current;

		await call.RequestStream.CompleteAsync();

		if (response.MessageResponseCase == ServerReflectionResponse.MessageResponseOneofCase.ErrorResponse)
		{
			throw new Exception($"Reflection error: {response.ErrorResponse.ErrorMessage}");
		}

		var rawDescriptors = response.FileDescriptorResponse.FileDescriptorProto;
		return rawDescriptors;
	}

	private Type BuildService(RepeatedField<ByteString> protoFiles)
	{
		var set = new FileDescriptorSet();

		foreach (var protoFile in protoFiles)
		{
			using var ms = new MemoryStream(protoFile.ToByteArray());

			var fdp = ProtoBuf.Serializer.Deserialize<FileDescriptorProto>(ms);
			fdp.IncludeInOutput = true;
			fdp.ApplyImports();
			set.Files.Add(fdp);
		}

		set.ApplyFileDependencyOrder();
		set.Process();

		var files = CSharpCodeGenerator.Default.Generate(set, options: new()
		{
			{ "services", "grpc" },
			{ "langver", "14" },
		});

		var builder = factory.Create(Config.Service + Random.Shared.Next());

		foreach (var file in files)
		{
			builder.AddRaw(file.Text);
		}

		var serviceType = builder.Build().BuiltAssembly!.GetTypes()
			.First(t => t.GetCustomAttributes<ServiceAttribute>(inherit: false).Any(x => x.Name == Config.Service));
		return serviceType;
	}

	private Expression<Func<object>> BuildGetClient(Type serviceType)
	{
		var createGrpcService = typeof(GrpcClientFactory)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(method => method.Name == nameof(GrpcClientFactory.CreateGrpcService))
			.Where(method => method.IsGenericMethod)
			.First(method => method.GetParameters().First().ParameterType == typeof(ChannelBase))
			.MakeGenericMethod(serviceType);

		var methodCallExpression = Expression.Call(
			createGrpcService,
			Expression.Constant(_channel),
			Expression.Convert(Expression.Constant(null), typeof(ClientFactory))
		);

		return Expression.Lambda<Func<object>>(methodCallExpression)
			.Log($"{definition.RequestId}.{Id} {nameof(GetClient)}", logger);
	}

	private Expression<Func<PipelineStore, object?>> BuildGetData(TypesStore types)
	{
		var builder = generator.Create(Linker);

		var store = builder.CreateParameter(typeof(PipelineStore), "store");

		var schema = typeBuilder.GetSchema(types, Config.Data);
		var value = typeBuilder.BuildObject(types, schema, _requestType, store, Linker);

		return builder.CreateLambda<Func<PipelineStore, object?>>(value)
			.Log($"{definition.RequestId}.{Id} {nameof(GetData)}", logger);
	}

	private Expression<Func<object, object?, CancellationToken, ValueTask<object?>>> BuildSend(Type serviceType,
		MethodInfo method)
	{
		var builder = generator.Create(Linker);

		var client = builder.CreateParameter(typeof(object), "client");
		var data = builder.CreateParameter(typeof(object), "data");
		var cancellationToken = builder.CreateParameter(typeof(CancellationToken), "cancellationToken");

		var dataVar = builder.CreateVariable(Expression.Convert(data, _requestType), "data");
		var clientVar = builder.CreateVariable(Expression.Convert(client, serviceType), "client");

		var result = builder.CreateVariable(Expression.Call(clientVar, method, dataVar,
			Expression.Convert(cancellationToken, typeof(CallContext))), "result", checkNull: false);

		var value = Expression.Call(typeof(GrpcRequestJob), nameof(Await), [_responseType], result);

		return builder.CreateLambda<Func<object, object?, CancellationToken, ValueTask<object?>>>(value)
			.Log($"{definition.RequestId}.{Id} {nameof(Send)}", logger);
	}

	private static async ValueTask<object?> Await<T>(ValueTask<T> task)
	{
		return await task;
	}
}

public class GrpcRequestJobRunner(GrpcRequestJob job, PipelineStore store) : IRunner
{
	public async Task<object?> Run(CancellationToken cancellationToken)
	{
		var client = job.GetClient();
		var data = job.GetData(store);
		var res = await job.Send(client, data, cancellationToken);

		return res;
	}
}