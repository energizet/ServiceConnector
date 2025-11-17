namespace ServiceConnector.Common;

public interface IRunnerFinder
{
	IJobGraph Get(string id);
}