using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using ServiceConnector.Common;
using ServiceConnector.Common.Extensions;

namespace ServiceConnector.Jobs;

public class ExpressionGeneratorFactory
{
	public ExpressionGenerator Create(ILinker linker)
	{
		return new(linker);
	}
}

public class ExpressionGenerator(ILinker linker)
{
	public List<ParameterExpression> Variables { get; } = [];
	public List<Expression> Body { get; } = [];
	private readonly List<ParameterExpression> _parameters = [];
	private readonly LabelTarget _returnLabel = Expression.Label(typeof(object), "returnLabel");
	private readonly List<GotoExpression> _returns = [];

	public LambdaExpression GetValue(string value, TypesStore types)
	{
		if (string.IsNullOrWhiteSpace(value) || value[0] is not '$')
		{
			return GetConstant(value);
		}

		// TODO interpolation ${}
		return GetValue(value.AsSpan()[1..], types);
	}

	private LambdaExpression GetConstant(string value)
	{
		CreateParameter(typeof(PipelineStore), "store");

		var constant = CreateVariable(Expression.Constant(value), "constant");

		return CreateLambda(constant);
	}

	private LambdaExpression GetValue(ReadOnlySpan<char> value, TypesStore types)
	{
		var store = CreateParameter(typeof(PipelineStore), "store");

		var result = MakeNullable(GetValue(value, types, store));

		return CreateLambda(result);
	}

	private ParameterExpression GetValue(ReadOnlySpan<char> value, TypesStore types, Expression store)
	{
		var separator = value.IndexOf('.');
		if (separator == -1)
		{
			separator = value.Length;
		}

		var variableName = value[..separator].ToString();
		var type = types.Get(variableName);

		var expression = Expression.Call(store, "Get", [type], Expression.Constant(variableName));
		var variable = CreateVariable(expression, variableName);

		linker.Link(variableName);

		for (var i = separator + 1; i < value.Length; i++)
		{
			var c = value[i];

			if (c is '.')
			{
				variable = GetField(value, separator, i, variable);
				separator = i;

				continue;
			}
		}

		if (separator < value.Length)
		{
			variable = GetField(value, separator, value.Length, variable);
		}

		return variable;
	}

	private ParameterExpression GetField(ReadOnlySpan<char> value, int separator, int i, Expression variable)
	{
		var name = value[(separator + 1)..i].ToString();
		var field = GetField(variable, name);

		return field;
	}

	private ParameterExpression GetField(Expression variable, string name)
	{
		var type = variable.Type;
		if (type.TryTo(typeof(IArray), out _))
		{
			var index = int.Parse(name);
			var indexConst = Expression.Constant(index);

			var count = CreateVariable(Expression.Call(variable, nameof(IArray.Count), null), "count");

			Body.Add(Expression.IfThen(
				Expression.IsFalse(Expression.AndAlso(
					Expression.GreaterThanOrEqual(indexConst, Expression.Constant(0)),
					Expression.LessThan(indexConst, count)
				)),
				CreateReturn()
			));

			var staticCount = IArray.StaticCount(type);
			Expression value = index < staticCount
				? Expression.PropertyOrField(variable, $"Item_{name}")
				: Expression.Property(
					Expression.PropertyOrField(variable, "Item_Others"),
					"Item",
					Expression.Subtract(indexConst, Expression.Constant(staticCount))
				);

			return CreateVariable(value, $"Item_{index}");
		}

		if (type.TryTo(typeof(IDictionary<,>), out var map))
		{
			var key = Expression.Constant(name);
			if (map.GenericTypeArguments[0].TryTo(typeof(int), out _))
			{
				key = Expression.Constant(int.Parse(name));
			}

			var value = CreateVariable(map.GenericTypeArguments[1], "value");

			Body.Add(Expression.IfThen(
				Expression.IsFalse(Expression.Call(
					variable, "TryGetValue", null, key, value
				)),
				CreateReturn()
			));

			return value;
		}

		if (type.TryTo(typeof(IReadOnlyList<>), out var list))
		{
			variable = Expression.Convert(variable, list);
			var index = Expression.Constant(int.Parse(name));
			var collection = Expression.Convert(variable, list.To(typeof(IReadOnlyCollection<>))!);
			var count = CreateVariable(Expression.PropertyOrField(collection, nameof(ICollection.Count)), "count");
			Body.Add(Expression.IfThen(
				Expression.IsFalse(Expression.AndAlso(
					Expression.GreaterThanOrEqual(index, Expression.Constant(0)),
					Expression.LessThan(index, count)
				)),
				CreateReturn()
			));

			return CreateVariable(Expression.Property(variable, "Item", index), $"Item_{name}");
		}

		var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		foreach (var field in type.GetFields())
		{
			fields.Add(field.Name, field.Name);
		}

		foreach (var field in type.GetProperties())
		{
			fields.Add(field.Name, field.Name);
		}

		return CreateVariable(Expression.PropertyOrField(variable, fields[name]), name);
	}

	public ParameterExpression CreateVariable(Expression value, string name, bool checkNull = true)
	{
		return AssignVariable(CreateVariable(value.Type, name), value,checkNull);
	}

	public void Assign(Expression left, Expression right)
	{
		Body.Add(Expression.Assign(left, right));
	}

	public ParameterExpression AssignVariable(ParameterExpression variable, Expression value, bool checkNull = true)
	{
		Assign(variable, value);
		if (value is ConstantExpression)
		{
			return variable;
		}

		if (checkNull)
		{
			Body.Add(Expression.IfThen(
				Expression.Equal(
					MakeNullable(variable), Expression.Constant(null)
				),
				CreateReturn()
			));
		}

		return variable;
	}

	public ParameterExpression CreateVariable(Type type, string name)
	{
		var variable = Expression.Variable(type, name);
		Variables.Add(variable);

		return variable;
	}

	public BlockExpression CreateBlock()
	{
		return Expression.Block(Variables, Body);
	}

	public LambdaExpression CreateLambda(Expression? result = null)
	{
		if (result != null)
		{
			FixReturns(result.Type);
			Body.Add(Expression.Label(_returnLabel, result));
		}

		return Expression.Lambda(CreateBlock(), _parameters);
	}

	public Expression<T> CreateLambda<T>(Expression? result = null)
	{
		if (result != null)
		{
			FixReturns(result.Type);
			Body.Add(Expression.Label(_returnLabel, result));
		}

		return Expression.Lambda<T>(CreateBlock(), _parameters);
	}

	public ParameterExpression CreateParameter(Type type, string name)
	{
		var store = Expression.Parameter(type, name);
		_parameters.Add(store);
		return store;
	}

	public static Expression MakeNullable(ParameterExpression variable)
	{
		if (
			variable.Type.TryTo(typeof(ValueType), out _) &&
			!variable.Type.TryTo(typeof(Nullable<>), out _)
		)
		{
			var nullableType = typeof(Nullable<>).MakeGenericType(variable.Type);
			return Expression.Convert(variable, nullableType);
		}

		return variable;
	}

	private GotoExpression CreateReturn()
	{
		var ret = Expression.Return(_returnLabel, Expression.Default(_returnLabel.Type));
		_returns.Add(ret);

		return ret;
	}

	private void FixReturns(Type type)
	{
		var prop = _returnLabel.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
			.First(item => item.Name.Contains("Type"));
		prop.SetValue(_returnLabel, type);

		foreach (var ret in _returns)
		{
			typeof(GotoExpression).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
				.First(item => item.Name.Contains("Value"))
				.SetValue(ret, Expression.Default(type));
		}
	}

	private static int FindMatchingBracket(ReadOnlySpan<char> value, char openBracket = '{', char closeBracket = '}')
	{
		var depth = 0;
		for (var i = 0; i < value.Length; i++)
		{
			if (value[i] == openBracket)
			{
				depth++;
				continue;
			}

			if (value[i] == closeBracket)
			{
				depth--;
				if (depth == 0)
				{
					return i;
				}
			}
		}

		return -1;
	}
}