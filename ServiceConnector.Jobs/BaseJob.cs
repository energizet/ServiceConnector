using ServiceConnector.Common;

namespace ServiceConnector.Jobs;

public class BaseJobConfig
{
	public required string Id { get; init; }
}

public abstract class BaseJob<T> where T : BaseJobConfig
{
	public required T Config { get; init; }
	public string Id => Config.Id;
	public abstract Task<Type> CompileAsync(TypesStore types);
	public abstract Task<object?> RunAsync(PipelineStore store);
}