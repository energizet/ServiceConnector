namespace ServiceConnector.Common;

public interface IRunner
{
	Task<object?> Run(CancellationToken cancellationToken);
}