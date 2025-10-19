using System.Text.Json;

namespace ServiceConnector.Common;

public interface IRunnerFinder
{
	(IRunner runner, PipelineDefinition definition) Get(string id);
}