using System.Collections;
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
	TypeBuilder typeBuilder,
	ExpressionGeneratorFactory generator,
	PipelineDefinition definition,
	ILogger<MappingJob> logger
) : BaseJob<MappingJobConfig, MappingJobRunner>(config, isAsync: false)
{
	public Func<PipelineStore, object?> GetData = null!;

	public override Task<Type> Compile(TypesStore types, CancellationToken cancellationToken)
	{
		GetData = BuildGetData(types, out var returnType).Compile();
		return Task.FromResult(returnType);
	}

	private Expression<Func<PipelineStore, object?>> BuildGetData(TypesStore types, out Type returnType)
	{
		var builder = generator.Create(Linker);
		var store = builder.CreateParameter(typeof(PipelineStore), "store");

		var lists = GetLists(types, store, builder).ToList();
		var variables = GetVariablesList(lists).ToList();
		var objects = GetObjectCreators(types, variables, store).ToList();

		var arrayTypes = objects.Select(x => x.Type).ToList();
		returnType = variables.All(x => x.IsOnlyStatic)
			? typeBuilder.BuildArray($"{definition.RequestId}_{Id}", arrayTypes)
			: typeBuilder.BuildArray($"{definition.RequestId}_{Id}", arrayTypes[..^1], arrayTypes[^1]);

		var result = builder.CreateVariable(Expression.New(returnType), "result", checkNull: false);

		var selector = CreateSelector(result, variables.Count, objects);

		builder.Body.Add(CreateLoop(store, builder, lists, selector));

		return builder.CreateLambda<Func<PipelineStore, object?>>(result)
			.Log($"{definition.RequestId}.{Id} {nameof(GetData)}", logger);
	}

	private IEnumerable<ParameterExpression> GetLists(TypesStore types, ParameterExpression store,
		ExpressionGenerator builder)
	{
		if (Config is { List: null, Lists: null } or { List: not null, Lists: not null })
		{
			throw new ArgumentException($"One of 'List' or 'Lists' required in {definition.RequestId}.{Id}");
		}

		Config.Lists ??= [Config.List!];

		for (var i = 0; i < Config.Lists.Count; i++)
		{
			var list = Config.Lists[i];
			var obj = typeBuilder.BuildObject(types, list, store, Linker);
			var variable = builder.CreateVariable(obj, $"list_{i}", checkNull: false);
			builder.Body.Add(Expression.Condition(
				Expression.NotEqual(variable, Expression.Constant(null)),
				variable,
				Expression.New(variable.Type)
			));
			yield return variable;
		}
	}

	private IEnumerable<ListVariables> GetVariablesList(List<ParameterExpression> lists)
	{
		for (var i = 0; i < lists.Count; i++)
		{
			var list = lists[i];
			if (list.Type.TryTo(typeof(IArray), out _))
			{
				var staticCount = IArray.StaticCount(list.Type);
				var isOnlyStatic = IArray.IsOnlyStatic(list.Type);

				var props = new List<Type>(staticCount + 1);
				for (var j = 0; j < staticCount; j++)
				{
					props.Add(list.Type.GetProperty($"Item_{j}")!.PropertyType);
				}

				var typeOther = list.Type.GetProperty("Item_Others")!.PropertyType.GenericTypeArguments[0];

				yield return new()
				{
					Variables = props,
					IsOnlyStatic = isOnlyStatic,
					LastVariable = typeOther,
				};
				continue;
			}

			if (list.Type.TryTo(typeof(IEnumerable<>), out var enumerableType))
			{
				yield return new()
				{
					IsOnlyStatic = false,
					LastVariable = enumerableType.GenericTypeArguments[0],
				};
				continue;
			}

			throw new ArgumentException($"{definition.RequestId}.{Id} must have 'List' or 'Lists' of array type");
		}
	}

	private IEnumerable<Expression> GetObjectCreators(TypesStore types, List<ListVariables> listsVariables,
		ParameterExpression store)
	{
		var variables = listsVariables.Select(x => x.GetEnumerator()).ToList();
		variables.ForEach(x => x.MoveNext());

		types["index"] = typeof(int);
		for (var i = 0;; i++)
		{
			var variablesTypes = variables.Select(x => x.Current).ToList();

			types["item"] = variablesTypes[0].Variable;
			for (var j = 0; j < variablesTypes.Count; j++)
			{
				types[$"item{j}"] = variablesTypes[j].Variable;
			}

			var type = typeBuilder.BuildType(types, Config.Map, $"{definition.RequestId}_{Id}_{i}");
			var value = typeBuilder.BuildObject(types, Config.Map, type, store, Linker);
			yield return value;

			if (variablesTypes.All(x => x.IsLast))
			{
				break;
			}

			variables.ForEach(x => x.MoveNext());
		}
	}

	private LambdaExpression CreateSelector(ParameterExpression result, int variablesCount,
		List<Expression> objects)
	{
		var builder = generator.Create(Linker);
		var store = builder.CreateParameter(typeof(PipelineStore), "store");

		var index = builder.CreateParameter(typeof(int), "index");
		builder.Assign(
			Expression.Property(store, "Item", Expression.Constant("index")),
			Expression.Convert(index, typeof(object))
		);

		for (var i = 0; i < variablesCount; i++)
		{
			var item = builder.CreateParameter(typeof(object), $"item{i}");
			if (i == 0)
			{
				builder.Assign(
					Expression.Property(store, "Item", Expression.Constant("item")),
					item
				);
			}

			builder.Assign(
				Expression.Property(store, "Item", Expression.Constant($"item{i}")),
				item
			);
		}

		var getOtherBuilder = generator.Create(Linker);
		var others = getOtherBuilder.CreateVariable(Expression.PropertyOrField(result, "Item_Others"), "others",
			checkNull: false);
		getOtherBuilder.Body.Add(Expression.IfThen(
			Expression.Equal(others, Expression.Constant(null)),
			Expression.Assign(
				Expression.PropertyOrField(result, "Item_Others"),
				Expression.Assign(
					others,
					Expression.New(others.Type)
				)
			)
		));
		getOtherBuilder.Body.Add(Expression.Call(
			others,
			nameof(IList.Add),
			null,
			objects[^1]
		));

		Expression res = IArray.IsOnlyStatic(result.Type)
			? Expression.Assign(
				Expression.PropertyOrField(result, $"Item_{objects.Count - 1}"),
				objects[^1]
			)
			: getOtherBuilder.CreateBlock();
		for (var i = objects.Count - 2; i >= 0; i--)
		{
			res = Expression.IfThenElse(
				Expression.Equal(index, Expression.Constant(i)),
				Expression.Assign(
					Expression.PropertyOrField(result, $"Item_{i}"),
					objects[i]
				),
				res
			);
		}

		builder.Body.Add(res);

		return builder.CreateLambda();
	}

	private Expression CreateLoop(ParameterExpression store, ExpressionGenerator builder,
		List<ParameterExpression> lists, LambdaExpression selector)
	{
		var loopBuilder = generator.Create(Linker);

		var enumerators = lists.Select((list, i) => builder.CreateVariable(
			Expression.Call(
				list,
				list.Type.GetMethod(nameof(IEnumerable.GetEnumerator))!
			),
			$"enumerator_{i}",
			checkNull: false
		)).ToList();

		var index = builder.CreateVariable(Expression.Constant(0), "index");

		var moveNextBuilder = generator.Create(Linker);
		Expression isMoveNext = Expression.Constant(false);
		var arguments = new List<ParameterExpression>
		{
			store,
			index,
		};
		for (var i = 0; i < enumerators.Count; i++)
		{
			var enumerator = enumerators[i];
			var isMoveNextVariable = builder.CreateVariable(typeof(bool), $"isMoveNext_{i}");
			var current = loopBuilder.CreateVariable(
				Expression.Condition(
					isMoveNextVariable,
					Expression.Convert(Expression.Property(enumerator, nameof(IEnumerator.Current)), typeof(object)),
					Expression.Constant(null)
				),
				$"item_{i}",
				checkNull: false
			);

			moveNextBuilder.AssignVariable(isMoveNextVariable, Expression.Call(
				enumerator,
				typeof(IEnumerator).GetMethod(nameof(IEnumerator.MoveNext))!
			), checkNull: false);
			isMoveNext = Expression.OrElse(isMoveNext, isMoveNextVariable);
			arguments.Add(current);
		}

		var moveNextBlock = moveNextBuilder.CreateBlock();

		builder.Body.Add(moveNextBlock);

		loopBuilder.Body.Add(Expression.Invoke(selector, arguments));
		loopBuilder.Assign(index, Expression.Increment(index));
		loopBuilder.Body.Add(moveNextBlock);

		var breakLabel = Expression.Label("LoopBreak");
		var loop = Expression.Loop(
			Expression.IfThenElse(
				Expression.IsTrue(isMoveNext),
				loopBuilder.CreateBlock(),
				Expression.Break(breakLabel)
			),
			breakLabel
		);

		var disposes = new List<MethodCallExpression>();

		foreach (var enumerator in enumerators)
		{
			MethodCallExpression? disponse;
			try
			{
				disponse = Expression.Call(enumerator, typeof(IDisposable).GetMethod(nameof(IDisposable.Dispose))!);
			}
			catch
			{
				disponse = null;
			}

			if (disponse != null)
			{
				disposes.Add(disponse);
			}
		}

		return disposes.Count == 0
			? loop
			: Expression.TryFinally(
				loop,
				Expression.Block(disposes)
			);
	}
}

public class ListVariables : IEnumerable<(bool IsLast, Type Variable)>
{
	public List<Type> Variables { get; init; } = [];
	public required bool IsOnlyStatic { get; init; }
	public required Type LastVariable { get; init; }

	public IEnumerator<(bool IsLast, Type Variable)> GetEnumerator()
	{
		for (var i = 0; i < Variables.Count; i++)
		{
			var variable = Variables[i];
			yield return (IsLast(i), variable);
		}

		if (!IsOnlyStatic)
		{
			yield return (true, LastVariable);
		}
	}

	private bool IsLast(int index)
	{
		if (!IsOnlyStatic)
		{
			return false;
		}

		return index == Variables.Count - 1;
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}

public class MappingJobRunner(MappingJob job, PipelineStore store) : IRunner
{
	public Task<object?> Run(CancellationToken cancellationToken)
	{
		return Task.FromResult(job.GetData(store));
	}
}