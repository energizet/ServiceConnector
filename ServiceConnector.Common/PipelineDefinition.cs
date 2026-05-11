using System.Text.Json;
using ServiceConnector.TypeBuilder;

namespace ServiceConnector.Common;

public class PipelineDefinition
{
	public bool IsEnable { get; init; } = true;
	public required string RequestId { get; init; }
	public JsonElement? Request { get; init; }
	public required JsonElement[] Pipeline { get; init; }

	public Type RequestType { get; set; } = null!;
	public string File { get; set; } = null!;
	public string FileHash { get; set; } = null!;
	public LoadContextStore LoadContext { get; set; } = null!;
	public IIControllerGenerator ControllerGenerator { get; set; } = null!;
}