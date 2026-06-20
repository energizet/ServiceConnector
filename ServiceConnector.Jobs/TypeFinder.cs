using System.Diagnostics.CodeAnalysis;
using ServiceConnector.Common;
using ServiceConnector.Common.Extensions;

namespace ServiceConnector.Jobs;

public class TypeFinder
{
	public Type ParseType(string value, TypesStore types)
	{
		if (string.IsNullOrWhiteSpace(value) || value[0] is not '$' || value.StartsWith("${"))
		{
			return typeof(string);
		}

		return MakeNullable(ParseType(value.AsSpan()[1..], types));
	}

	private Type ParseType(ReadOnlySpan<char> value, TypesStore types)
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

		for (var i = separator + 1; i < value.Length; i++)
		{
			var c = value[i];

			if (c is '.')
			{
				type = GetField(value, separator, i, type);
				separator = i;

				continue;
			}
		}

		if (separator < value.Length)
		{
			type = GetField(value, separator, value.Length, type);
		}

		return type;
	}

	private static Type GetField(ReadOnlySpan<char> value, int separator, int i, Type type)
	{
		var name = value[(separator + 1)..i].ToString();

		if (!TryGetField(type, name, out var fieldType))
		{
			throw new ArgumentException($"Type {name} in {value[..separator]} doesn't exist");
		}

		type = fieldType;
		return type;
	}

	private static bool TryGetField(Type type, string name, [MaybeNullWhen(false)] out Type outType)
	{
		if (type.TryTo(typeof(IDictionary<,>), out var map))
		{
			if (type.GenericTypeArguments[0].CanTo(typeof(int)) && !int.TryParse(name, out _))
			{
				outType = null;
				return false;
			}

			outType = map.GetGenericArguments()[1];
			return true;
		}

		if (type.TryTo(typeof(IReadOnlyList<>), out var list))
		{
			if (!int.TryParse(name, out _))
			{
				outType = null;
				return false;
			}

			outType = list.GetGenericArguments()[0];
			return true;
		}

		var fields = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

		foreach (var field in type.GetFields())
		{
			fields.Add(field.Name, field.FieldType);
		}

		foreach (var field in type.GetProperties())
		{
			fields.Add(field.Name, field.PropertyType);
		}

		return fields.TryGetValue(name, out outType);
	}

	private static Type MakeNullable(Type type)
	{
		if (
			type.CanTo(typeof(ValueType)) &&
			!type.CanTo(typeof(Nullable<>))
		)
		{
			return typeof(Nullable<>).MakeGenericType(type);
		}

		return type;
	}
}