using System.Linq.Expressions;
using System.Net.Http.Json;
using System.Reflection;
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

	public int? RetryCount { get; set; }
	public float? RetryDelay { get; set; }
	public float? Timeout { get; set; }
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
	public delegate ValueTask<object?> DeserializerStreamDelegate(Stream stream, JsonSerializerOptions? options,
		CancellationToken ct);

	public delegate object? DeserializerDelegate(string json, JsonSerializerOptions? options);

	public Func<PipelineStore, object?> GetUrl { get; private set; } = null!;
	public Func<PipelineStore, QueryList> GetQuery { get; private set; } = null!;
	public Func<PipelineStore, HeaderList> GetHeaders { get; private set; } = null!;
	public Func<PipelineStore, object?> GetData { get; private set; } = null!;
	public DeserializerStreamDelegate DeserializeStream { get; private set; } = null!;
	public DeserializerDelegate Deserialize { get; private set; } = null!;
	public Type ResponseType { get; private set; } = typeof(object);

	public JsonSerializerOptions Options { get; } = new()
	{
		PropertyNameCaseInsensitive = true,
	};

	public override Task<Type> Compile(TypesStore types, CancellationToken cancellationToken)
	{
		Config.RetryCount ??= 1;
		Config.RetryDelay ??= 0.1f;
		Config.Timeout ??= 300;

		if (Config.RetryCount <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(Config.RetryCount), Config.RetryCount,
				$"{definition.RequestId}.{Id}.RetryCount must be a positive number");
		}

		if (Config.RetryDelay < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(Config.RetryDelay), Config.RetryDelay,
				$"{definition.RequestId}.{Id}.RetryDelay must be a positive number");
		}

		if (Config.Timeout < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(Config.Timeout), Config.Timeout,
				$"{definition.RequestId}.{Id}.RetryCount must be a positive number");
		}

		ResponseType = BuildResponseType();
		GetMethod();
		GetUrl = BuildGetUrl(types).Compile();
		GetQuery = BuildGetQuery(types).Compile();
		GetHeaders = BuildGetHeaders(types).Compile();
		GetData = BuildGetData(types).Compile();
		DeserializeStream = BuildDeserializeStream().Compile();
		Deserialize = BuildDeserialize().Compile();
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

	private Expression<DeserializerStreamDelegate> BuildDeserializeStream()
	{
		var builder = generator.Create(Linker);

		var stream = builder.CreateParameter(typeof(Stream), "stream");
		var options = builder.CreateParameter(typeof(JsonSerializerOptions), "options");
		var token = builder.CreateParameter(typeof(CancellationToken), "ct");

		var type = typeof(DeserializerWrapper<>).MakeGenericType(ResponseType);
		var methodInfo = type.GetMethod(
			nameof(DeserializerWrapper<>.DeserializeStreamAsync),
			BindingFlags.Public | BindingFlags.Static
		)!;

		var expression = Expression.Call(
			methodInfo,
			stream,
			options,
			token
		);

		return builder.CreateLambda<DeserializerStreamDelegate>(expression)
			.Log($"{definition.RequestId}.{Id} {nameof(DeserializeStream)}", logger);
	}

	private Expression<DeserializerDelegate> BuildDeserialize()
	{
		var builder = generator.Create(Linker);

		var json = builder.CreateParameter(typeof(string), "json");
		var options = builder.CreateParameter(typeof(JsonSerializerOptions), "options");

		var type = typeof(DeserializerWrapper<>).MakeGenericType(ResponseType);
		var methodInfo = type.GetMethod(
			nameof(DeserializerWrapper<>.DeserializeAsync),
			BindingFlags.Public | BindingFlags.Static
		)!;

		var expression = Expression.Call(
			methodInfo,
			json,
			options
		);

		return builder.CreateLambda<DeserializerDelegate>(expression)
			.Log($"{definition.RequestId}.{Id} {nameof(Deserialize)}", logger);
	}

	private static class DeserializerWrapper<T>
	{
		public static async ValueTask<object?> DeserializeStreamAsync(Stream stream,
			JsonSerializerOptions? options,
			CancellationToken ct)
		{
			return await JsonSerializer.DeserializeAsync<T>(stream, options, ct);
		}

		public static object? DeserializeAsync(string json, JsonSerializerOptions? options)
		{
			return JsonSerializer.Deserialize<T>(json, options);
		}
	}
}

public class HttpRequestJobRunner(HttpRequestJob job, PipelineStore store, IHttpClientFactory clientFactory) : IRunner
{
	public async Task<object?> Run(CancellationToken cancellationToken)
	{
		var maxRetries = job.Config.RetryCount!.Value;
		var timeout = TimeSpan.FromSeconds(job.Config.Timeout!.Value);
		var delay = TimeSpan.FromSeconds(job.Config.RetryDelay!.Value);

		for (var attempt = 1; attempt <= maxRetries; attempt++)
		{
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cts.CancelAfter(timeout);

			try
			{
				return await Send(cts.Token);
			}
			catch (Exception)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					throw;
				}

				if (attempt == maxRetries)
				{
					throw;
				}

				await Task.Delay(delay, cts.Token);
			}
		}

		return null;
	}

	private async Task<object?> Send(CancellationToken cancellationToken)
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

		using var response = await clientFactory.CreateClient().SendAsync(message,
			HttpCompletionOption.ResponseHeadersRead, cancellationToken);

		response.EnsureSuccessStatusCode();

		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var result = await job.DeserializeStream(stream, job.Options, cancellationToken);

		//var json = await response.Content.ReadAsStringAsync(cancellationToken);
		//var result = job.Deserialize(json, job.Options);

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