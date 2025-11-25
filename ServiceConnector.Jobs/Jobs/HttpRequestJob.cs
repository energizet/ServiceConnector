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
	ILogger<HttpRequestJob> logger,
	ExpressionGeneratorFactory generator
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
				$"{Definition.RequestId}.{Id}.Method unknown")
		};
	}

	private Type BuildResponseType()
	{
		return TypeBuilderFromSchema.BuildType(Config.Response, $"{Definition.RequestId}_{Id}");
	}

	private Expression<Func<PipelineStore, object?>> BuildGetUrl(TypesStore types)
	{
		var builder = generator.Create();

		var store = builder.CreateParameter(typeof(PipelineStore), "store");

		var lambda = TypeBuilder.BuildObject(types, Config.Url, store);

		return builder.CreateLambda<Func<PipelineStore, object?>>(lambda)
			.Log($"{Definition.RequestId}.{Id} {nameof(GetUrl)}", logger);
	}

	private Expression<Func<PipelineStore, QueryList>> BuildGetQuery(TypesStore types)
	{
		var builder = generator.Create();

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

						var type = TypeBuilder.BuildType(types, item.Value, $"{Definition.RequestId}_{Id}_{item.Name}");
						var value = TypeBuilder.BuildObject(types, item.Value, type, store);
						var valueStr = Expression.Call(value, nameof(ToString), null);

						builder.Body.Add(Expression.Call(queryList, nameof(QueryList.Add), null, name, valueStr));
					}

					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(Config.Params), Config.Params?.ValueKind,
						$"{Definition.RequestId}.{Id}.Params unsupported type");
			}
		}

		return builder.CreateLambda<Func<PipelineStore, QueryList>>(queryList)
			.Log($"{Definition.RequestId}.{Id} {nameof(GetUrl)}", logger);
	}

	private Expression<Func<PipelineStore, HeaderList>> BuildGetHeaders(TypesStore types)
	{
		var builder = generator.Create();

		var store = builder.CreateParameter(typeof(PipelineStore), "store");

		var headerList = builder.CreateVariable(Expression.New(typeof(HeaderList)), "headerList");

		foreach (var header in Config.Headers)
		{
			var name = Expression.Constant(header.Key);

			var value = TypeBuilder.BuildObject(types, header.Value, store);
			var valueStr = Expression.Call(value, nameof(ToString), null);

			builder.Body.Add(Expression.Call(headerList, nameof(HeaderList.Add), null, name, valueStr));
		}

		return builder.CreateLambda<Func<PipelineStore, HeaderList>>(headerList)
			.Log($"{Definition.RequestId}.{Id} {nameof(GetHeaders)}", logger);
	}

	private Expression<Func<PipelineStore, object?>> BuildGetData(TypesStore types)
	{
		var builder = generator.Create();

		var store = builder.CreateParameter(typeof(PipelineStore), "store");

		Expression value = Expression.Constant(null, typeof(object));

		if (Config.Data != null)
		{
			var type = TypeBuilder.BuildType(types, Config.Data.Value, $"{Definition.RequestId}_{Id}_data");
			value = TypeBuilder.BuildObject(types, Config.Data.Value, type, store);
		}

		return builder.CreateLambda<Func<PipelineStore, object?>>(value)
			.Log($"{Definition.RequestId}.{Id} {nameof(GetData)}", logger);
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