extern alias protobuf;
using System.Linq.Expressions;
using System.Reflection;
using System.ServiceModel;
using System.Text.Json;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Reflection.V1Alpha;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Configuration;
using ServiceConnector.Common;
using ServiceConnector.Common.Extensions;
using ServiceConnector.Common.Interfaces;
using FileDescriptorSet = protobuf::Google.Protobuf.Reflection.FileDescriptorSet;
using FileDescriptorProto = protobuf::Google.Protobuf.Reflection.FileDescriptorProto;
using CSharpCodeGenerator = protobuf::ProtoBuf.Reflection.CSharpCodeGenerator;

namespace ServiceConnector.Jobs.Jobs;

public class GrpcRequestJobConfig : BaseJobConfig
{
	public required string Url { get; init; }
	public required string Service { get; init; }
	public required string Method { get; init; }

	public required JsonElement Data { get; set; }
	public required JsonElement Response { get; set; }
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

	public override async Task<Type> Compile(TypesStore types, CancellationToken cancellationToken)
	{
		_channel = GrpcChannel.ForAddress(Config.Url);

		_responseType = BuildResponseType();
		GetData = BuildGetData(types).Compile();

		var builder = factory.Create("IGreeterService");

		var interfaceBuilder = builder.CreateInterface("IGreeterService")
			.AddAttribute($"{typeof(ServiceContractAttribute).ToDisplayString()}(Name = \"{Config.Service}\")");

		interfaceBuilder.CreateMethod(Config.Method, typeof(ValueTask<>).MakeGenericType(_responseType),
		[
			$"{_requestType.ToDisplayString()} request",
			$"{typeof(CancellationToken).ToDisplayString()} cancellationToken"
		]);

		var inter = builder.Build().First();

		GetClient = BuildGetClient(inter).Compile();

		return _responseType;
	}

	private Type BuildResponseType()
	{
		return typeBuilderFromSchema.BuildType(Config.Response, $"{definition.RequestId}_{Id}");
	}

	private Expression<Func<PipelineStore, object?>> BuildGetData(TypesStore types)
	{
		var builder = generator.Create(Linker);

		var store = builder.CreateParameter(typeof(PipelineStore), "store");

		Expression value = Expression.Constant(null, typeof(object));

		var schema = typeBuilder.GetSchema(types, Config.Data);
		_requestType = typeBuilder.BuildType(schema, $"{definition.RequestId}_{Id}_data");
		value = typeBuilder.BuildObject(types, schema, _requestType, store, Linker);

		return builder.CreateLambda<Func<PipelineStore, object?>>(value)
			.Log($"{definition.RequestId}.{Id} {nameof(GetData)}", logger);
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

		return Expression.Lambda<Func<object>>(methodCallExpression);
	}
}

public class GrpcRequestJobRunner(GrpcRequestJob job, PipelineStore store) : IRunner
{
	public Task<object?> Run(CancellationToken cancellationToken)
	{
		var client = job.GetClient();
		var data = job.GetData(store);

		var sayHello = client.GetType().GetRuntimeMethods()
			.First(x => x.Name == $"IGreeterServiceDynamic.{job.Config.Method}");
		var response = sayHello.Invoke(client, [data, cancellationToken]);

		var res = response.GetType().GetProperty("Result").GetValue(response);

		return Task.FromResult(res);
	}
}
/*
// Определение сервиса
[ServiceContract(Name = "test.GreeterService")] // Указываем оригинальное имя пакета и сервиса из proto
public interface IGreeterService
{
	// Метод. Асинхронность обязательна для gRPC (Task или ValueTask)
	ValueTask<HelloReply> SayHelloAsync(HelloRequest request, CancellationToken cancellationToken);
}

// Определение сообщения запроса
[ProtoContract]
public class HelloRequest
{
	[ProtoMember(1)] // Порядковый номер поля из proto (name = 1)
	public string Name { get; set; } = string.Empty;
}

// Определение сообщения ответа
[ProtoContract]
public class HelloReply
{
	[ProtoMember(1)] // Порядковый номер поля из proto (message = 1)
	public string Message { get; set; } = string.Empty;
}
/**/