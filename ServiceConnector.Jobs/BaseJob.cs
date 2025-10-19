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
	public abstract Task<Type> Compile(TypesStore types);
	public abstract Task<object?> Run(PipelineStore store);
}

public interface IJob
{
	string Id { get; }
	ILinker Linker { set; }
	Task<Type> Compile(TypesStore types);
	Task<object?> Run(PipelineStore store);
}