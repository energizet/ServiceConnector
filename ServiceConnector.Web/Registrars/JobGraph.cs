using ServiceConnector.Common;
using ServiceConnector.Jobs;

namespace ServiceConnector.Web.Registrars;

public partial class JobGraph(List<JobGraph.Node> firsts, JobGraph.Node? last) : IRunner
{
	public async Task<object?> Run(PipelineStore store, CancellationToken cancellationToken)
	{
		if (last == null)
		{
			return null;
		}

		var tasks = firsts
			.Select(node => Task.Run(async () =>
				(
					Node: node,
					Result: await node.Job.Run(new(store), cancellationToken)
				), cancellationToken)
			).ToList();

		var colors = new Dictionary<Node, NodeColor>();
		foreach (var node in firsts)
		{
			colors[node] = NodeColor.Grey;
		}

		while (tasks.Count > 0)
		{
			var task = await Task.WhenAny(tasks);
			tasks.Remove(task);

			Node node = null!;
			object? result;
			try
			{
				(node, result) = await task;
			}
			catch
			{
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
				tasks.Add(Task.Run(async () =>
					(
						Node: nodeTo,
						Result: await nodeTo.Job.Run(new(store), cancellationToken)
					), cancellationToken)
				);
			}
		}

		store.TryGetValue(last.Job.Id, out var res);
		return res;
	}

	private enum NodeColor
	{
		Grey,
		Black,
	}
}