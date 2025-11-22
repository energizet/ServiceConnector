using Microsoft.Extensions.DependencyInjection;
using ServiceConnector.Common;

namespace ServiceConnector.Jobs;

public abstract class BaseJobConfig
{
	public required string Id { get; init; }
}

public abstract class BaseJob<T, TRunner>(T config, bool isAsync) : IJob
	where T : BaseJobConfig
	where TRunner : IRunner
{
	public T Config => config;
	public string Id => Config.Id;
	public bool IsAsync => isAsync;
	public PipelineDefinition Definition { get; set; } = null!;
	public TypeBuilder TypeBuilder { get; set; } = null!;
	public TypeBuilderFromSchema TypeBuilderFromSchema { get; set; } = null!;
	public abstract Task<Type> Compile(TypesStore types, CancellationToken cancellationToken);

	public IRunner CreateRunner(IServiceProvider provider, PipelineStore store)
	{
		return ActivatorUtilities.CreateInstance<TRunner>(provider, this, store);
	}
}

public interface IJob
{
	string Id { get; }
	public bool IsAsync { get; }
	PipelineDefinition Definition { set; }
	TypeBuilder TypeBuilder { set; }
	TypeBuilderFromSchema TypeBuilderFromSchema { set; }
	Task<Type> Compile(TypesStore types, CancellationToken cancellationToken);
	IRunner CreateRunner(IServiceProvider provider, PipelineStore store);
}