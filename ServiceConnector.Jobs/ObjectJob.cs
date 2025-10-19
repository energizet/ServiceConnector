using System.Text.Json;
using ServiceConnector.Common;

namespace ServiceConnector.Jobs;

public class ObjectJobConfig : BaseJobConfig
{
	public required JsonElement Data { get; init; }
}

[PipelineJob]
internal class ObjectJob : BaseJob<ObjectJobConfig>
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