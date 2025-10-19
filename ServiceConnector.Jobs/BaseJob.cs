using ServiceConnector.Common;

namespace ServiceConnector.Jobs;

public abstract class BaseJobConfig
{
	public required string Id { get; init; }
}

public abstract class BaseJob<T>(T config) : IJob
	where T : BaseJobConfig
{
	protected T Config => config;
	public string Id => Config.Id;
	public ILinker Linker { get; set; } = null!;
	public abstract Task<Type> Compile(TypesStore types, CancellationToken cancellationToken);
	public abstract Task<object?> Run(PipelineStore store, CancellationToken cancellationToken);
}

public interface IJob : IRunner
{
	string Id { get; }
	ILinker Linker { set; }
	Task<Type> Compile(TypesStore types, CancellationToken cancellationToken);
}