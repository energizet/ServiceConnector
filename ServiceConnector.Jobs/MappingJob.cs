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
	public override async Task<Type> Compile(TypesStore types)
	{
		Linker.Link("array");
		return typeof(List<object>);
	}

	public override async Task<object?> Run(PipelineStore store)
	{
		return new List<object>();
	}
}