using ServiceConnector.Common;
using ServiceConnector.Jobs;

namespace ServiceConnector.Web.Registrars;

public partial class JobGraph(List<JobGraph.Node> firsts, JobGraph.Node? last) : IJobGraph
{
	public Task<object?> Run(PipelineStore store, IServiceProvider provider,
		CancellationToken cancellationToken)
	{
		var runner = new RecursionRunner(firsts, last, store, provider, cancellationToken);
		return runner.Run();
	}

	public class Node
	{
		public HashSet<Node> From { get; } = [];
		public HashSet<Node> To { get; } = [];
		public required IJob Job { get; init; }
	}

	public class EdgeLinker(Builder builder, IJob to) : ILinker
	{
		public void Link(string from)
		{
			builder.AddEdge(from, to);
		}
	}
}