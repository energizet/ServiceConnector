using ServiceConnector.Jobs;

namespace ServiceConnector.Web.Registrars;

public partial class JobGraph
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
}