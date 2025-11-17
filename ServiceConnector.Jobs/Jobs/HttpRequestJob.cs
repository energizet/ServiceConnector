using System.Text.Json;
using ServiceConnector.Common;

namespace ServiceConnector.Jobs.Jobs;

public class HttpRequestJobConfig : BaseJobConfig
{
	public enum MethodEnum
	{
		Get = 0,
		Post = 1,
	}

	public enum FormatEnum
	{
		Json = 0,
	}

	public MethodEnum Method { get; init; } = MethodEnum.Get;
	public required string Url { get; init; }

	public Dictionary<string, string> Headers { get; set; } = [];
	public JsonElement? Params { get; set; }

	public FormatEnum Format { get; set; } = FormatEnum.Json;
	public JsonElement? Data { get; set; }
}

[PipelineJob]
public class HttpRequestJob(HttpRequestJobConfig config) : BaseJob<HttpRequestJobConfig, HttpRequestJobRunner>(config)
{
	public override async Task<Type> Compile(TypesStore types, CancellationToken cancellationToken)
	{
		return typeof(object);
	}
}

public class HttpRequestJobRunner(HttpRequestJob job, PipelineStore store) : IRunner
{
	public async Task<object?> Run(CancellationToken cancellationToken)
	{
		return new();
	}
}