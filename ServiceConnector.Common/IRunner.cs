namespace ServiceConnector.Common;

public interface IRunner
{
	Task<object?> Run(PipelineStore store, CancellationToken cancellationToken);
}