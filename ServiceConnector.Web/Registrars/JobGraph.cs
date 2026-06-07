using System.Diagnostics;
using ServiceConnector.Common;
using ServiceConnector.Jobs;

namespace ServiceConnector.Web.Registrars;

public partial class JobGraph(List<JobGraph.Node> firsts, JobGraph.Node? last) : IJobGraph
{
	public Task<object?> Run(PipelineStore store, IServiceProvider provider, CancellationToken cancellationToken)
	{
		var runner = new Runner(firsts, last, store, provider, cancellationToken);
		return runner.Run();
	}

	[DebuggerDisplay("Node = {DebuggerDisplay()}")]
	public class Node
	{
		public HashSet<Node> From { get; } = [];
		public HashSet<Node> To { get; } = [];
		public required IJob Job { get; init; }
		public bool IsAsync => Job.IsAsync;
		
		private string DebuggerDisplay()
		{
			return $"{Job.GetType().Name}({Job.Id})";
		}
	}
}