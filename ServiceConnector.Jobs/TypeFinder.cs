using System.Diagnostics.CodeAnalysis;
using ServiceConnector.Common;
using ServiceConnector.TypeBuilder;

namespace ServiceConnector.Jobs;

public class TypeFinder
{
	public Type ParseType(string value, TypesStore types)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return typeof(string);
		}

		if (value[0] is not '$')
		{
			return typeof(string);
		}

		return ParseType(value.AsSpan()[1..], types);
	}

	private Type ParseType(ReadOnlySpan<char> value, TypesStore types)
	{
		var separator = value.IndexOf('.');
		if (separator == -1)
		{
			separator = value.Length;
		}

		var variableName = value[..separator];
		if (!types.TryGetValue(variableName.ToString(), out var type))
		{
			throw new ArgumentException($"Type {variableName} doesn't exist");
		}

		for (var i = separator + 1; i < value.Length; i++)
		{
			var c = value[i];

			if (c is '.')
			{
				var name = value[(separator + 1)..i];

				if (!TryGetField(type, name.ToString(), out var fieldType))
				{
					throw new ArgumentException($"Type {name} in {value[..separator]} doesn't exist");
				}

				type = fieldType;
				separator = i;

				continue;
			}
		}

		if (separator < value.Length)
		{
			var name = value[(separator + 1)..];

			if (!TryGetField(type, name.ToString(), out var fieldType))
			{
				throw new ArgumentException($"Type {name} in {value[..separator]} doesn't exist");
			}

			type = fieldType;
		}

		return type;
	}

	private static bool TryGetField(Type type, string name, [MaybeNullWhen(false)] out Type outType)
	{
		if (type.TryTo(typeof(IArray), out _))
		{
			outType = type.GetProperty($"Item_{name}")?.PropertyType;
			return outType != null;
		}

		if (type.TryTo(typeof(Dictionary<,>), out var dict))
		{
			outType = dict.GetGenericArguments()[1];
			return true;
		}

		if (type.TryTo(typeof(IEnumerable<>), out var list))
		{
			outType = list.GetGenericArguments()[0];
			return true;
		}


		foreach (var field in type.GetFields())
		{
			if (string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase))
			{
				outType = field.FieldType;
				return true;
			}
		}

		foreach (var field in type.GetProperties())
		{
			if (string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase))
			{
				outType = field.PropertyType;
				return true;
			}
		}

		outType = null;
		return false;
	}

	private int FindMatchingBracket(ReadOnlySpan<char> value, char openBracket = '{', char closeBracket = '}')
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