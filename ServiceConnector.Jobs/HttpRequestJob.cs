using System.Text.Json;
using ServiceConnector.Common;

namespace ServiceConnector.Jobs;

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
	public required JsonElement Params { get; set; }

	public FormatEnum Format { get; set; } = FormatEnum.Json;
	public JsonElement? Data { get; set; }
}

[PipelineJob]
public class HttpRequestJob : BaseJob<HttpRequestJobConfig>
{
	public override async Task<Type> CompileAsync(TypesStore types)
	{
		return typeof(object);
	}

	public override async Task<object?> RunAsync(PipelineStore store)
	{
		return new();
	}
}