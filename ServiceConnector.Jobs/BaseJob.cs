using ServiceConnector.Common;

namespace ServiceConnector.Jobs;

public class BaseJobConfig
{
	public required string Id { get; init; }
}

public abstract class BaseJob<T>(T config) : IJob
	where T : BaseJobConfig
{
	public T Config => config;
	public string Id => Config.Id;
	public abstract Task<Type> CompileAsync(TypesStore types);
	public abstract Task<object?> RunAsync(PipelineStore store);
}

public interface IJob
{
	string Id { get; }
}