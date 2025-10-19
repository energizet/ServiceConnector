using ServiceConnector.Jobs;

namespace ServiceConnector.Web.Registrars;

public class JobGraph(List<JobGraph.Node> firsts, JobGraph.Node? last)
{
	public class Builder
	{
		private readonly Dictionary<string, Node> _nodes = new(StringComparer.OrdinalIgnoreCase);
		private Node? _last;

		public EdgeLinker AddNode(IJob job)
		{
			var node = new Node
			{
				Job = job,
			};
			_nodes.Add(job.Id, node);
			_last = node;

			return new(this, job);
		}

		public void AddEdge(string fromId, IJob to)
		{
			var toId = to.Id;

			if (!_nodes.TryGetValue(fromId, out var fromNode) ||
			    !_nodes.TryGetValue(toId, out var toNode))
			{
				return;
			}

			fromNode.To.Add(toNode);
			toNode.From.Add(fromNode);
		}

		public JobGraph Build()
		{
			var firsts = _nodes.Values
				.Where(x => x.From.Count == 0)
				.ToList();

			return new(firsts, _last);
		}
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