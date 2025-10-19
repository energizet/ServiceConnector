namespace ServiceConnector.Common;

public class RunnersStore : Store<(IRunner runner, PipelineDefinition definition)>, IRunnerFinder;