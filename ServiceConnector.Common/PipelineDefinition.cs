using System.Runtime.Loader;
using System.Text.Json;

namespace ServiceConnector.Common;

public class PipelineDefinition
{
	public bool IsEnable { get; init; } = true;
	public required string RequestId { get; init; }
	public required JsonElement Request { get; init; }
	public required JsonElement[] Pipeline { get; init; }

	public Type RequestType { get; set; } = null!;
	public string File { get; set; } = null!;
	public string FileHash { get; set; } = null!;
	public AssemblyLoadContext LoadContext { get; set; } = null!;
}