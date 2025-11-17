namespace ServiceConnector.Common;

public interface IJobGraph
{
	Task<object?> Run(PipelineStore store, IServiceProvider provider, CancellationToken cancellationToken);
}