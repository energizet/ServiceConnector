using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using ServiceConnector.Common;
using ServiceConnector.TypeBuilder;

namespace ServiceConnector.Jobs;

public class ExpressionGeneratorFactory
{
	public ExpressionGenerator Create()
	{
		return new();
	}
}

public class ExpressionGenerator
{
	private readonly List<ParameterExpression> _variables = [];
	private readonly List<Expression> _body = [];
	private readonly LabelTarget _returnLabel = Expression.Label(typeof(object), "returnLabel");
	private readonly List<GotoExpression> _returns = [];

	public LambdaExpression GetValue(string value, TypesStore types)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return GetConstant(value);
		}

		if (value[0] is not '$')
		{
			return GetConstant(value);
		}

		return GetValue(value.AsSpan()[1..], types);
	}

	private LambdaExpression GetConstant(string value)
	{
		var store = Expression.Parameter(typeof(PipelineStore), "store");

		return Expression.Lambda(Expression.Constant(value), store);
	}

	private LambdaExpression GetValue(ReadOnlySpan<char> value, TypesStore types)
	{
		var store = Expression.Parameter(typeof(PipelineStore), "store");

		var result = MakeNullable(GetValue(value, types, store));

		FixReturns(result.Type);
		_body.Add(Expression.Label(_returnLabel, result));

		var block = Expression.Block(_variables, _body);
		return Expression.Lambda(block, store);
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
			return CreateVariable(Expression.PropertyOrField(variable, $"Item_{name}"), $"Item_{name}");
		}

		if (type.TryTo(typeof(Dictionary<,>), out var map))
		{
			var key = Expression.Constant(name);
			if (map.GenericTypeArguments[0].TryTo(typeof(int), out _))
			{
				key = Expression.Constant(int.Parse(name));
			}

			var value = CreateVariable(map.GenericTypeArguments[1], "value");

			_body.Add(Expression.IfThen(
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
			_body.Add(Expression.IfThen(
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

	private ParameterExpression CreateVariable(Expression value, string name)
	{
		return AssignVariable(CreateVariable(value.Type, name), value);
	}

	private ParameterExpression AssignVariable(ParameterExpression variable, Expression value)
	{
		_body.Add(Expression.Assign(variable, value));


		_body.Add(Expression.IfThen(
			Expression.Equal(
                MakeNullable(variable), Expression.Constant(null)
			),
			CreateReturn()
		));

		return variable;
	}

	private static Expression MakeNullable(ParameterExpression variable)
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

	private ParameterExpression CreateVariable(Type type, string name)
	{
		var variable = Expression.Variable(type, name);
		_variables.Add(variable);

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
}