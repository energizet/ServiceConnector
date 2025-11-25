using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ServiceConnector.Common;
using ServiceConnector.TypeBuilder;

namespace ServiceConnector.Jobs.Jobs;

public class MappingJobConfig : BaseJobConfig
{
	public string? List { get; set; }
	public List<string>? Lists { get; set; }
	public required JsonElement Map { get; init; }
}

[PipelineJob]
public class MappingJob(
	MappingJobConfig config,
	ExpressionGeneratorFactory generator,
	ILogger<MappingJob> logger
) : BaseJob<MappingJobConfig, MappingJobRunner>(config, isAsync: false)
{
	public Func<PipelineStore, object?> GetData = null!;

	public override Task<Type> Compile(TypesStore types, CancellationToken cancellationToken)
	{
		GetData = BuildGetData(types).Compile();
		return Task.FromResult(typeof(List<object>));
	}

	private Expression<Func<PipelineStore, object?>> BuildGetData(TypesStore types)
	{
		if (Config is { List: null, Lists: null } or { List: not null, Lists: not null })
		{
			throw new ArgumentException($"One of 'List' or 'Lists' required in {Definition.RequestId}.{Id}");
		}

		Config.Lists ??= [Config.List!];

		var builder = generator.Create();
		var store = builder.CreateParameter(typeof(PipelineStore), "store");

		var lists = Config.Lists.Select(x => TypeBuilder.BuildObject(types, x, store)).ToList();
		var variables = GetVariablesList(builder, lists);

		for (var i = 0; i < variables.Count; i++)
		{
			var variable = variables[i];
			if (variable.IsOneType)
			{
				types[$"item{i}"] = variable.Variables[0].Type;
				continue;
			}
		}

		if (variables.All(x => !x.IsIArray))
		{
			types["item"] = variables[0].Variables[0].Type;

			for (var i = 0; i < variables.Count; i++)
			{
				types[$"item{i}"] = variables[i].Variables[0].Type;
			}

			//var type = TypeBuilder.BuildType(types, Config.Map, $"{Definition.RequestId}_{Id}_{item.Name}");
			//var value = TypeBuilder.BuildObject(types, Config.Map, type, store);
		}

		var result = builder.CreateVariable(Expression.Constant(new List<object>
		{
			"1"
		}), "result");
		return builder.CreateLambda<Func<PipelineStore, object?>>(result)
			.Log($"{Definition.RequestId}.{Id} {nameof(GetData)}", logger);
	}

	private object SelectItem(PipelineStore store, int index, object? item0, object? item1)
	{
		store["index"] = index;
		store["item"] = item0;
		store["item0"] = item0;
		store["item1"] = item1;

		if (index == 0)
		{
		}

		return null;
	}

	private List<ListVariables> GetVariablesList(ExpressionGenerator builder, List<Expression> lists)
	{
		return lists.Select((list, i) =>
		{
			if (list.Type.TryTo(typeof(IArray), out _))
			{
				var props = list.Type.GetProperties()
					.Where(x => x.Name.StartsWith("Item_"))
					.OrderBy(x => int.Parse(x.Name["Item_".Length..]))
					.Select((prop, j) => builder.CreateVariable(prop.PropertyType, $"item_{i}_{j}"))
					.ToList();

				return new ListVariables
				{
					IsIArray = true,
					Variables = props,
				};
			}

			if (list.Type.TryTo(typeof(IEnumerable<>), out var enumerableType))
			{
				var variable = builder.CreateVariable(enumerableType.GenericTypeArguments[0], $"item_{i}");

				return new()
				{
					IsIArray = false,
					Variables = [variable],
				};
			}

			throw new ArgumentException($"{Definition.RequestId}.{Id} must have 'List' or 'Lists' of array type");
		}).ToList();
	}
}

public class ListVariables
{
	public required bool IsIArray { get; set; }
	public required List<ParameterExpression> Variables { get; set; }

	public bool IsOneType => !IsIArray;
}

public class MappingJobRunner(MappingJob job, PipelineStore store) : IRunner
{
	public Task<object?> Run(CancellationToken cancellationToken)
	{
		return Task.FromResult(job.GetData(store));
	}
}