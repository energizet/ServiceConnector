using System.Collections;
using System.Diagnostics.CodeAnalysis;
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
		var store = CreateParameter(typeof(PipelineStore), "store");

		Expression result;
		if (string.IsNullOrWhiteSpace(value) || value[0] is not '$')
		{
			result = CreateVariable(Expression.Constant(value), "constant");
		}
		else
		{
			result = MakeNullable(GetValue(value.AsSpan()[1..], types, store));
		}

		// TODO interpolation ${}
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
		if (!types.TryGetValue(variableName, out var type))
		{
			throw new ArgumentException($"Type {variableName} doesn't exist");
		}

		var call = Call(store, nameof(PipelineStore.GetOrNull), null, Expression.Constant(variableName));
		var variable = CreateVariable(Expression.Convert(call, type), variableName);

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

		if (!TryGetField(variable, name, out var field))
		{
			throw new ArgumentException($"Type {name} in {value[..separator]} doesn't exist");
		}

		return field;
	}

	private bool TryGetField(Expression variable, string name, [MaybeNullWhen(false)] out ParameterExpression outValue)
	{
		var type = variable.Type;

		if (type.TryTo(typeof(IDictionary<,>), out var map))
		{
			var key = Expression.Constant(name);
			if (map.GenericTypeArguments[0].CanTo(typeof(int)))
			{
				if (!int.TryParse(name, out var keyParsed))
				{
					outValue = null;
					return false;
				}

				key = Expression.Constant(keyParsed);
			}

			var value = CreateVariable(map.GenericTypeArguments[1], "value");

			Body.Add(Expression.IfThen(
				Expression.IsFalse(Expression.Call(
					variable, "TryGetValue", null, key, value
				)),
				CreateReturn()
			));

			outValue = value;
			return true;
		}

		if (type.TryTo(typeof(IReadOnlyList<>), out var list))
		{
			if (!int.TryParse(name, out var indexParsed))
			{
				outValue = null;
				return false;
			}

			variable = Expression.Convert(variable, list);
			var index = Expression.Constant(indexParsed);
			var collection = Expression.Convert(variable, list.To(typeof(IReadOnlyCollection<>))!);
			var count = CreateVariable(Expression.PropertyOrField(collection, nameof(ICollection.Count)), "count");
			Body.Add(Expression.IfThen(
				Expression.IsFalse(Expression.AndAlso(
					Expression.GreaterThanOrEqual(index, Expression.Constant(0)),
					Expression.LessThan(index, count)
				)),
				CreateReturn()
			));

			outValue = CreateVariable(Expression.Property(variable, "Item", index), $"Item_{name}");
			return true;
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

		outValue = CreateVariable(Expression.PropertyOrField(variable, fields[name]), name);
		return true;
	}

	public ParameterExpression CreateVariable(Expression value, string name, bool checkNull = true)
	{
		return AssignVariable(CreateVariable(value.Type, name), value, checkNull);
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

	private static MethodCallExpression Call(
		Expression instance, string methodName,
		Type[]? generics,
		params Expression[]? arguments
	)
	{
		var methods = instance.Type.GetMethods();

		foreach (var m in methods)
		{
			var method = m;
			if (method.Name != methodName)
			{
				continue;
			}

			var parameters = method.GetParameters();

			var argumentsCount = arguments?.Length ?? 0;
			if (argumentsCount != parameters.Length)
			{
				continue;
			}

			if (argumentsCount > 0 &&
			    parameters.Select((p, i) => arguments![i].Type.CanTo(p.ParameterType)).Any(x => !x))
			{
				continue;
			}

			var genericArguments = method.GetGenericArguments();

			var genericsCount = generics?.Length ?? 0;
			if (genericsCount != genericArguments.Length)
			{
				continue;
			}

			if (genericsCount > 0)
			{
				method = method.MakeGenericMethod(generics!);
			}

			return Expression.Call(instance, method, arguments);
		}

		return Expression.Call(instance, methodName, generics, arguments);
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
			variable.Type.CanTo(typeof(ValueType)) &&
			!variable.Type.CanTo(typeof(Nullable<>))
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