using System.Collections.Concurrent;
using ServiceConnector.Common;

namespace ServiceConnector.Web.Registrars;

public partial class JobGraph
{
	private class RecursionRunner(
		List<Node> firsts,
		Node? last,
		PipelineStore store,
		IServiceProvider provider,
		CancellationToken cancellationToken
	)
	{
		private ConcurrentDictionary<Node, NodeColor> Colors { get; } = [];
		private ConcurrentBag<Exception> Errors { get; } = [];

		public async Task<object?> Run()
		{
			if (last == null)
			{
				return null;
			}

			await Task.WhenAll(RunNodes(firsts));

			if (!Errors.IsEmpty)
			{
				throw new AggregateException(Errors);
			}

			store.TryGetValue(last.Job.Id, out var res);
			return res;
		}

		private IEnumerable<Task> RunNodes(IEnumerable<Node> nodes)
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

				yield return Task.Run(() => RunNode(node));
			}
		}

		private async Task RunNode(Node node)
		{
			Colors[node] = NodeColor.Grey;

			object? result;
			try
			{
				result = await node.Job.CreateRunner(provider, new(store)).Run(cancellationToken);
			}
			catch (Exception ex)
			{
				Errors.Add(ex);
				return;
			}

			store[node.Job.Id] = result;
			Colors[node] = NodeColor.Black;

			await Task.WhenAll(RunNodes(node.To));
		}

		private enum NodeColor
		{
			Grey,
			Black,
		}
	}
}