using System.Collections;
using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ServiceConnector.Common;
using ServiceConnector.Common.Extensions;

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
		var variableTypes = GetVariablesList(lists).ToList();
		var objectCreator = GetObjectCreator(types, variableTypes, store);

		returnType = typeof(List<>).MakeGenericType(objectCreator.Type);

		var result = builder.CreateVariable(Expression.New(returnType), "result", checkNull: false);

		var selector = CreateSelector(result, variableTypes.Count, objectCreator);

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

	private IEnumerable<Type> GetVariablesList(List<ParameterExpression> lists)
	{
		foreach (var list in lists)
		{
			if (list.Type.TryTo(typeof(IEnumerable<>), out var enumerableType))
			{
				yield return enumerableType.GenericTypeArguments[0];
				continue;
			}

			throw new ArgumentException($"{definition.RequestId}.{Id} must have 'List' or 'Lists' of array type");
		}
	}

	private Expression GetObjectCreator(TypesStore types, List<Type> variableTypes, ParameterExpression store)
	{
		types["index"] = typeof(int);

		types["item"] = variableTypes[0];
		for (var j = 0; j < variableTypes.Count; j++)
		{
			types[$"item{j}"] = variableTypes[j];
		}

		var schema = typeBuilder.GetSchema(types, Config.Map);
		var type = typeBuilder.BuildType(schema, $"{definition.RequestId}_{Id}");
		var value = typeBuilder.BuildObject(types, schema, type, store, Linker);

		return value;
	}

	private LambdaExpression CreateSelector(ParameterExpression result, int variablesCount, Expression objectCreator)
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

		builder.Body.Add(Expression.Call(
			result,
			nameof(IList.Add),
			null,
			objectCreator
		));

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
			MethodCallExpression? dispose;
			try
			{
				dispose = Expression.Call(enumerator, typeof(IDisposable).GetMethod(nameof(IDisposable.Dispose))!);
			}
			catch
			{
				dispose = null;
			}

			if (dispose != null)
			{
				disposes.Add(dispose);
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

public class MappingJobRunner(MappingJob job, PipelineStore store) : IRunner
{
	public Task<object?> Run(CancellationToken cancellationToken)
	{
		return Task.FromResult(job.GetData(store));
	}
}