namespace ServiceConnector.Common;

public interface IRunnerFinder
{
	IRunner Get(string id);
}