using ServiceConnector.Common;

namespace ServiceConnector.Web.Registrars;

public partial class JobGraph
{
	private class Runner(
		List<Node> firsts,
		Node? last,
		PipelineStore store,
		IServiceProvider provider,
		CancellationToken cancellationToken
	)
	{
		private readonly CancellationTokenSource _cancellationSource = CancellationTokenSource
			.CreateLinkedTokenSource(cancellationToken);

		private HashSet<Task<(Node Node, object? Result)>> Tasks { get; set; } = [];
		private Dictionary<Node, DepsVisit> RemainingDeps { get; } = [];
		private List<Exception> Errors { get; } = [];
		private CancellationToken CancellationToken => _cancellationSource.Token;

		public async Task<object?> Run()
		{
			if (last == null)
			{
				return null;
			}

			RunNodes(firsts);

			while (!CancellationToken.IsCancellationRequested && Tasks.Count > 0)
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
					await _cancellationSource.CancelAsync();
					Errors.Add(ex);
					continue;
				}

				store[node.Job.Id] = result;

				if (Errors.Count == 0)
				{
					RunNodes(node.To);
				}
			}

			if (Errors.Count != 0)
			{
				throw new AggregateException(Errors);
			}

			store.TryGetValue(last.Job.Id, out var res);
			return res;
		}

		private void RunNodes(IEnumerable<Node> nodes)
		{
			foreach (var node in nodes)
			{
				RemainingDeps.TryAdd(node, new(node.From.Count));
				var deps = RemainingDeps[node];

				if (deps.Visited)
				{
					continue;
				}

				if (node.From.Count != 0)
				{
					RemainingDeps[node] = deps = deps.Decrement();
				}

				if (deps.Dependencies != 0)
				{
					continue;
				}

				RemainingDeps[node] = deps.Visit();
				Tasks.Add(node.IsAsync ? RunNode(node) : Task.Run(() => RunNode(node), CancellationToken));
			}
		}

		private async Task<(Node Node, object? Result)> RunNode(Node node)
		{
			return (
				Node: node,
				Result: await node.Job.CreateRunner(provider, new(store)).Run(CancellationToken)
			);
		}

		private readonly record struct DepsVisit(int Dependencies, bool Visited = false)
		{
			public DepsVisit Decrement()
			{
				return this with { Dependencies = Dependencies - 1 };
			}

			public DepsVisit Visit()
			{
				return this with { Visited = true };
			}
		}
	}
}