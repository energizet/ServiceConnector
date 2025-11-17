using System.Text.Json;
using ServiceConnector.Common;

namespace ServiceConnector.Jobs.Jobs;

public class MappingJobConfig : BaseJobConfig
{
	public string? List { get; set; }
	public List<string>? Lists { get; set; }
	public required JsonElement Map { get; init; }
}

[PipelineJob]
public class MappingJob(MappingJobConfig config) : BaseJob<MappingJobConfig, MappingJobRunner>(config)
{
	public override async Task<Type> Compile(TypesStore types, CancellationToken cancellationToken)
	{
		//Linker.Link("array");
		return typeof(List<object>);
	}
}

public class MappingJobRunner(MappingJob job, PipelineStore store) : IRunner
{
	public async Task<object?> Run(CancellationToken cancellationToken)
	{
		return new List<object>
		{
			new
			{
				Title = "11",
				TitleType = "11--12",
			},
			new
			{
				Title = "21",
				TitleType = "21--22",
			}
		};
	}
}