using ServiceConnector.Common;

namespace ServiceConnector.Web.Registrars;

public partial class JobGraph(List<JobGraph.Node> firsts, JobGraph.Node? last) : IJobGraph
{
	public async Task<object?> Run(PipelineStore store, IServiceProvider provider, CancellationToken cancellationToken)
	{
		if (last == null)
		{
			return null;
		}

		var tasks = firsts.Select(node => Run(node, store, provider, cancellationToken)).ToList();

		var colors = new Dictionary<Node, NodeColor>();
		foreach (var node in firsts)
		{
			colors[node] = NodeColor.Grey;
		}

		var errors = new List<Exception>();

		while (tasks.Count > 0)
		{
			var task = await Task.WhenAny(tasks);
			tasks.Remove(task);

			Node node;
			object? result;
			try
			{
				(node, result) = await task;
			}
			catch (Exception ex)
			{
				errors.Add(ex);
				continue;
			}

			store[node.Job.Id] = result;
			colors[node] = NodeColor.Black;

			foreach (var nodeTo in node.To)
			{
				if (colors.ContainsKey(nodeTo))
				{
					continue;
				}

				var nodes = nodeTo.From;
				if (nodes.Any(x => !colors.TryGetValue(x, out var color) || color != NodeColor.Black))
				{
					continue;
				}

				colors[nodeTo] = NodeColor.Grey;
				tasks.Add(Run(nodeTo, store, provider, cancellationToken));
			}
		}

		if (errors.Count != 0)
		{
			throw new AggregateException(errors);
		}

		store.TryGetValue(last.Job.Id, out var res);
		return res;
	}

	private static Task<(Node Node, object? Result)> Run(Node node, PipelineStore store,
		IServiceProvider provider, CancellationToken cancellationToken)
	{
		return Task.Run(async () =>
		(
			Node: node,
			Result: await node.Job.CreateRunner(provider, new(store)).Run(cancellationToken)
		), cancellationToken);
	}

	private enum NodeColor
	{
		Grey,
		Black,
	}
}