using System.Linq.Expressions;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ServiceConnector.Common;

namespace ServiceConnector.Jobs.Jobs;

public class HttpRequestJobConfig : BaseJobConfig
{
	public enum MethodEnum
	{
		Get = 1,
		Post = 2,
		Put = 3,
		Delete = 4,
		Patch = 5,
	}

	public MethodEnum Method { get; init; } = MethodEnum.Get;
	public required string Url { get; init; }

	public Dictionary<string, string> Headers { get; set; } = [];
	public JsonElement? Params { get; set; }

	public JsonElement? Data { get; set; }
	public JsonElement Response { get; set; }
}

[PipelineJob]
public class HttpRequestJob(
	HttpRequestJobConfig config,
	TypeBuilder typeBuilder,
	TypeBuilderFromSchema typeBuilderFromSchema,
	ExpressionGeneratorFactory generator,
	PipelineDefinition definition,
	ILogger<HttpRequestJob> logger
) : BaseJob<HttpRequestJobConfig, HttpRequestJobRunner>(config, isAsync: true)
{
	public Func<PipelineStore, object?> GetUrl { get; private set; } = null!;
	public Func<PipelineStore, QueryList> GetQuery { get; private set; } = null!;
	public Func<PipelineStore, HeaderList> GetHeaders { get; private set; } = null!;
	public Func<PipelineStore, object?> GetData { get; private set; } = null!;
	public Type ResponseType { get; private set; } = typeof(object);

	public JsonSerializerOptions Options { get; } = new()
	{
		PropertyNameCaseInsensitive = true,
	};

	public override Task<Type> Compile(TypesStore types, CancellationToken cancellationToken)
	{
		ResponseType = BuildResponseType();
		GetMethod();
		GetUrl = BuildGetUrl(types).Compile();
		GetQuery = BuildGetQuery(types).Compile();
		GetHeaders = BuildGetHeaders(types).Compile();
		GetData = BuildGetData(types).Compile();
		return Task.FromResult(ResponseType);
	}

	public HttpMethod GetMethod()
	{
		return Config.Method switch
		{
			HttpRequestJobConfig.MethodEnum.Get => HttpMethod.Get,
			HttpRequestJobConfig.MethodEnum.Post => HttpMethod.Post,
			HttpRequestJobConfig.MethodEnum.Put => HttpMethod.Put,
			HttpRequestJobConfig.MethodEnum.Delete => HttpMethod.Delete,
			HttpRequestJobConfig.MethodEnum.Patch => HttpMethod.Patch,
			_ => throw new ArgumentOutOfRangeException(nameof(Config.Method), Config.Method,
				$"{definition.RequestId}.{Id}.Method unknown")
		};
	}

	private Type BuildResponseType()
	{
		return typeBuilderFromSchema.BuildType(Config.Response, $"{definition.RequestId}_{Id}");
	}

	private Expression<Func<PipelineStore, object?>> BuildGetUrl(TypesStore types)
	{
		var builder = generator.Create(Linker);

		var store = builder.CreateParameter(typeof(PipelineStore), "store");

		var lambda = typeBuilder.BuildObject(types, Config.Url, store, Linker);

		return builder.CreateLambda<Func<PipelineStore, object?>>(lambda)
			.Log($"{definition.RequestId}.{Id} {nameof(GetUrl)}", logger);
	}

	private Expression<Func<PipelineStore, QueryList>> BuildGetQuery(TypesStore types)
	{
		var builder = generator.Create(Linker);

		var store = builder.CreateParameter(typeof(PipelineStore), "store");

		var queryList = builder.CreateVariable(Expression.New(typeof(QueryList)), "queryList");

		if (Config.Params != null)
		{
			switch (Config.Params?.ValueKind)
			{
				case JsonValueKind.String:
					throw new NotImplementedException();
					break;
				case JsonValueKind.Object:
					foreach (var item in Config.Params.Value.EnumerateObject())
					{
						var name = Expression.Constant(item.Name);

						var schema = typeBuilder.GetSchema(types, item.Value);
						var type = typeBuilder.BuildType(schema, $"{definition.RequestId}_{Id}_{item.Name}");
						var value = typeBuilder.BuildObject(types, schema, type, store, Linker);
						var valueStr = Expression.Call(value, nameof(ToString), null);

						builder.Body.Add(Expression.Call(queryList, nameof(QueryList.Add), null, name, valueStr));
					}

					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(Config.Params), Config.Params?.ValueKind,
						$"{definition.RequestId}.{Id}.Params unsupported type");
			}
		}

		return builder.CreateLambda<Func<PipelineStore, QueryList>>(queryList)
			.Log($"{definition.RequestId}.{Id} {nameof(GetUrl)}", logger);
	}

	private Expression<Func<PipelineStore, HeaderList>> BuildGetHeaders(TypesStore types)
	{
		var builder = generator.Create(Linker);

		var store = builder.CreateParameter(typeof(PipelineStore), "store");

		var headerList = builder.CreateVariable(Expression.New(typeof(HeaderList)), "headerList");

		foreach (var header in Config.Headers)
		{
			var name = Expression.Constant(header.Key);

			var value = typeBuilder.BuildObject(types, header.Value, store, Linker);
			var valueStr = Expression.Call(value, nameof(ToString), null);

			builder.Body.Add(Expression.Call(headerList, nameof(HeaderList.Add), null, name, valueStr));
		}

		return builder.CreateLambda<Func<PipelineStore, HeaderList>>(headerList)
			.Log($"{definition.RequestId}.{Id} {nameof(GetHeaders)}", logger);
	}

	private Expression<Func<PipelineStore, object?>> BuildGetData(TypesStore types)
	{
		var builder = generator.Create(Linker);

		var store = builder.CreateParameter(typeof(PipelineStore), "store");

		Expression value = Expression.Constant(null, typeof(object));

		if (Config.Data != null)
		{
			var schema = typeBuilder.GetSchema(types, Config.Data.Value);
			var type = typeBuilder.BuildType(schema, $"{definition.RequestId}_{Id}_data");
			value = typeBuilder.BuildObject(types, schema, type, store, Linker);
		}

		return builder.CreateLambda<Func<PipelineStore, object?>>(value)
			.Log($"{definition.RequestId}.{Id} {nameof(GetData)}", logger);
	}
}

public class HttpRequestJobRunner(HttpRequestJob job, PipelineStore store, HttpClient client) : IRunner
{
	public async Task<object?> Run(CancellationToken cancellationToken)
	{
		var message = new HttpRequestMessage
		{
			Method = job.GetMethod(),
			RequestUri = new($"{job.GetUrl(store)}{job.GetQuery(store)}"),
		};

		foreach (var header in job.GetHeaders(store).Headers)
		{
			message.Headers.Add(header.Key, header.Value);
		}

		var data = job.GetData(store);
		if (data != null)
		{
			message.Content = JsonContent.Create(data);
		}

		using var response = await client.SendAsync(message, cancellationToken);
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = await JsonSerializer.DeserializeAsync(stream, job.ResponseType, job.Options, cancellationToken);

		return result;
	}
}

public class QueryList
{
	public Dictionary<string, string> Query { get; } = new();

	public void Add(string name, string value)
	{
		Query[name] = value;
	}

	public override string ToString()
	{
		if (Query.Count == 0)
		{
			return "";
		}

		return $"?{string.Join("&", Query.Select(x => $"{x.Key}={x.Value}"))}";
	}
}

public class HeaderList
{
	public Dictionary<string, string> Headers { get; } = new();

	public void Add(string name, string value)
	{
		Headers[name] = value;
	}
}