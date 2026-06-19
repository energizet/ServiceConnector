using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ServiceConnector.Common;
using ServiceConnector.Jobs.Extensions;

namespace ServiceConnector.Jobs;

public abstract class BaseJobConfig
{
	public required string Id { get; init; }
}

/// <param name="isAsync">
/// if true - run like Runner.Run()
/// if false - run like Task.Run(() => Runner.Run())
/// </param>
[DebuggerDisplay("{DebuggerDisplay()}")]
public abstract class BaseJob<T, TRunner>(T config, bool isAsync) : IJob
	where T : BaseJobConfig
	where TRunner : IRunner
{
	public T Config => config;
	public string Id => Config.Id;
	public bool IsAsync => isAsync;
	public required ILinker Linker { get; set; }
	public abstract Task<Type> Compile(TypesStore types, CancellationToken cancellationToken);

	private string DebuggerDisplay()
	{
		return $"{GetType().Name}({Id})";
	}

	private Func<IJob, PipelineStore, object[]> Factory { get; } = typeof(TRunner).CreateFactory().Compile();

	public IRunner CreateRunner(IServiceProvider provider, PipelineStore store)
	{
		return ActivatorUtilities.CreateInstance<TRunner>(provider, Factory(this, store));
	}
}

public interface IJob
{
	string Id { get; }
	public bool IsAsync { get; }
	ILinker Linker { set; }
	Task<Type> Compile(TypesStore types, CancellationToken cancellationToken);
	IRunner CreateRunner(IServiceProvider provider, PipelineStore store);
}