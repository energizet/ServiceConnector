using System.Text.Json;
using ServiceConnector.Common;

namespace ServiceConnector.Jobs;

public class MappingJobConfig : BaseJobConfig
{
	public string? List { get; set; }
	public List<string>? Lists { get; set; }
	public required JsonElement Map { get; init; }
}

[PipelineJob]
public class MappingJob(MappingJobConfig config) : BaseJob<MappingJobConfig>(config)
{
	public override async Task<Type> CompileAsync(TypesStore types)
	{
		return typeof(List<object>);
	}

	public override async Task<object?> RunAsync(PipelineStore store)
	{
		return new List<object>();
	}
}