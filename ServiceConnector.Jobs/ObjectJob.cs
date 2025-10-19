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
	public override async Task<Type> Compile(TypesStore types, CancellationToken cancellationToken)
	{
		Linker.Link("AlarmPost");
		Linker.Link("AlarmRequestTotalCount");
		return typeof(List<Test>);
	}

	public override async Task<object?> Run(PipelineStore store, CancellationToken cancellationToken)
	{
		var request = store["request"]!;
		return (List<Test>)
		[
			new Test
			{
				Title = request.ToString()!,
				Type = "link",
				Icon = "settings",
				Target = "_blank"
			},
			new Test
			{
				Title = "Выход",
				Type = "button",
				Icon = "logout",
				Target = "logout"
			}
		];
	}

	public class Test
	{
		public string Title { get; init; }
		public string Type { get; init; }
		public string Icon { get; init; }
		public string Target { get; init; }
	}
}