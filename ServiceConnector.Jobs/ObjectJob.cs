using System.Text.Json;
using ServiceConnector.Common;

namespace ServiceConnector.Jobs;

public class ObjectJobConfig : BaseJobConfig
{
	public required JsonElement Data { get; init; }
}

[PipelineJob]
public class ObjectJob(ObjectJobConfig config) : BaseJob<ObjectJobConfig>(config)
{
	public override async Task<Type> Compile(TypesStore types)
	{
		Linker.Link("AlarmPost");
		Linker.Link("AlarmRequestTotalCount");
		return typeof(object);
	}

	public override async Task<object?> Run(PipelineStore store)
	{
		return new();
	}
}