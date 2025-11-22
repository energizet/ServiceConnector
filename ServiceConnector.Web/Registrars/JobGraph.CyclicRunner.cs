using ServiceConnector.Common;

namespace ServiceConnector.Web.Registrars;

public partial class JobGraph
{
	private class CyclicRunner(
		List<Node> firsts,
		Node? last,
		PipelineStore store,
		IServiceProvider provider,
		CancellationToken cancellationToken
	)
	{
		private List<Task<(Node Node, object? Result)>> Tasks { get; set; } = [];
		private Dictionary<Node, NodeColor> Colors { get; } = [];
		private List<Exception> Errors { get; } = [];

		public async Task<object?> Run()
		{
			if (last == null)
			{
				return null;
			}

			Tasks.AddRange(RunNodes(firsts));

			while (Tasks.Count > 0)
			{
				var task = await Task.WhenAny(Tasks);
				Tasks.Remove(task);

				Node node;
				object? result;
				try
				{
					(node, result) = await task;
				}
				catch (Exception ex)
				{
					Errors.Add(ex);
					continue;
				}

				store[node.Job.Id] = result;
				Colors[node] = NodeColor.Black;

				Tasks.AddRange(RunNodes(node.To));
			}

			if (Errors.Count != 0)
			{
				throw new AggregateException(Errors);
			}

			store.TryGetValue(last.Job.Id, out var res);
			return res;
		}

		private IEnumerable<Task<(Node Node, object? Result)>> RunNodes(IEnumerable<Node> nodes)
		{
			foreach (var node in nodes)
			{
				if (Colors.ContainsKey(node))
				{
					continue;
				}

				if (node.From.Any(x => !Colors.TryGetValue(x, out var from) || from != NodeColor.Black))
				{
					continue;
				}

				Colors[node] = NodeColor.Grey;
				yield return Task.Run(() => RunNode(node));
			}
		}

		private async Task<(Node Node, object? Result)> RunNode(Node node)
		{
			return (
				Node: node,
				Result: await node.Job.CreateRunner(provider, new(store)).Run(cancellationToken)
			);
		}

		private enum NodeColor
		{
			Grey,
			Black,
		}
	}
}