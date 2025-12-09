using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using ServiceConnector.Common;
using ServiceConnector.Common.Extensions;

namespace ServiceConnector.Jobs;

public enum UniversalType
{
	Unknown,
	String,
	Boolean,
	Number,
	DateTime,
	Array,
	Object,
}

public class SchemaNode
{
	public required string Name { get; set; }
	public UniversalType Type { get; set; }
	public SchemaNode? ArrayItemSchema { get; set; }
	public Dictionary<string, SchemaNode>? Properties { get; set; }
	public Type? ClrType { get; set; }
	public bool IsPath { get; set; }
	public object? OriginValue { get; set; }

	public SchemaNode Clone()
	{
		var clone = new SchemaNode
		{
			Name = Name,
			Type = Type,
			ClrType = ClrType,
			IsPath = IsPath,
			OriginValue = OriginValue,
		};

		if (ArrayItemSchema != null)
		{
			clone.ArrayItemSchema = ArrayItemSchema.Clone();
		}

		if (Properties != null)
		{
			clone.Properties = [];
			foreach (var (key, value) in Properties)
			{
				clone.Properties[key] = value.Clone();
			}
		}

		return clone;
	}

	public override string ToString()
	{
		var originInfo = ClrType != null ? $" [{ClrType.Name}]" : "";

		if (Type == UniversalType.Array)
		{
			return $"List<{ArrayItemSchema?.Type ?? UniversalType.Unknown}>{originInfo}";
		}

		if (Type == UniversalType.Object)
		{
			return $"Object {{ ... }}{originInfo}";
		}

		return $"{Type}{originInfo}";
	}
}

public static class SchemaBuilder
{
	public static SchemaNode InferCommonSchema(List<SchemaNode> schemas)
	{
		if (schemas.Count == 0)
		{
			return new() { Name = "Root", Type = UniversalType.Unknown };
		}

		var seed = new SchemaNode { Name = "Root", Type = UniversalType.Unknown };
		return schemas.Aggregate(seed, MergeNodes);
	}

	public static SchemaNode MergeNodes(SchemaNode a, SchemaNode b)
	{
		if (a.Type == UniversalType.Unknown)
		{
			return b.Clone();
		}

		if (b.Type == UniversalType.Unknown)
		{
			return a.Clone();
		}

		var result = new SchemaNode
		{
			Name = a.Name,
			Type = a.Type,
		};

		var matchA = true;
		var matchB = true;

		if (a.Type != b.Type)
		{
			if (a.Type.IsContainer() || b.Type.IsContainer())
			{
				result.Type = UniversalType.Object;
			}
			else
			{
				result.Type = UniversalType.String;
			}

			matchA = false;
			matchB = false;
		}
		else if (a.Type == UniversalType.Array)
		{
			if (a.ArrayItemSchema != null && b.ArrayItemSchema != null)
			{
				result.ArrayItemSchema = MergeNodes(a.ArrayItemSchema, b.ArrayItemSchema);

				if (result.ArrayItemSchema.ClrType != a.ArrayItemSchema.ClrType)
				{
					matchA = false;
				}

				if (result.ArrayItemSchema.ClrType != b.ArrayItemSchema.ClrType)
				{
					matchB = false;
				}
			}
			else if (a.ArrayItemSchema != null)
			{
				result.ArrayItemSchema = a.ArrayItemSchema.Clone();
				matchB = false;
			}
			else if (b.ArrayItemSchema != null)
			{
				result.ArrayItemSchema = b.ArrayItemSchema.Clone();
				matchA = false;
			}
		}
		else if (a.Type == UniversalType.Object)
		{
			result.Properties = [];
			var propsA = a.Properties ?? [];
			var propsB = b.Properties ?? [];

			var allKeys = propsA.Keys.Union(propsB.Keys);

			foreach (var key in allKeys)
			{
				var hasInA = propsA.TryGetValue(key, out var propA);
				var hasInB = propsB.TryGetValue(key, out var propB);

				if (hasInA && hasInB)
				{
					var mergedProp = MergeNodes(propA!, propB!);
					result.Properties[key] = mergedProp;

					if (mergedProp.ClrType == null || mergedProp.ClrType != propA!.ClrType)
					{
						matchA = false;
					}

					if (mergedProp.ClrType == null || mergedProp.ClrType != propB!.ClrType)
					{
						matchB = false;
					}
				}
				else if (hasInA)
				{
					result.Properties[key] = propA!.Clone();
					matchB = false;
				}
				else
				{
					result.Properties[key] = propB!.Clone();
					matchA = false;
				}
			}
		}

		if (matchB && b.ClrType != null)
		{
			result.ClrType = b.ClrType;
		}
		else if (matchA && a.ClrType != null)
		{
			result.ClrType = a.ClrType;
		}
		else
		{
			result.ClrType = null;
		}

		return result;
	}
}

public static class SchemaBuilderExtensions
{
	public static bool IsContainer(this UniversalType type)
	{
		return type is UniversalType.Object or UniversalType.Array;
	}

	extension(Type type)
	{
		private bool IsNumber()
		{
			return type == typeof(int) ||
				type == typeof(double) ||
				type == typeof(decimal) ||
				type == typeof(long) ||
				type == typeof(float);
		}

		private bool IsList([MaybeNullWhen(false)] out Type itemType)
		{
			if (type.TryTo(typeof(IEnumerable<>), out var list))
			{
				itemType = list.GetGenericArguments()[0];
				return true;
			}

			itemType = null;
			return false;
		}

		public SchemaNode ConvertToSchema(string name = "Root", HashSet<Type>? visited = null)
		{
			var node = new SchemaNode
			{
				Name = name,
				ClrType = type,
			};

			var underlying = Nullable.GetUnderlyingType(type) ?? type;

			if (underlying == typeof(string))
			{
				node.Type = UniversalType.String;
			}
			else if (underlying == typeof(bool))
			{
				node.Type = UniversalType.Boolean;
			}
			else if (underlying.IsNumber())
			{
				node.Type = UniversalType.Number;
			}
			else if (underlying == typeof(DateTime))
			{
				node.Type = UniversalType.DateTime;
			}
			else if (underlying.IsList(out var itemType))
			{
				node.Type = UniversalType.Array;
				visited ??= [];
				if (!visited.Contains(itemType))
				{
					node.ArrayItemSchema = itemType.ConvertToSchema("Item", visited);
				}
			}
			else
			{
				node.Type = UniversalType.Object;
				visited ??= [];
				if (!visited.Add(underlying))
				{
					return node;
				}

				node.Properties ??= [];
				foreach (var prop in underlying.GetProperties())
				{
					node.Properties[prop.Name] = prop.PropertyType.ConvertToSchema(prop.Name, [..visited]);
				}
			}

			return node;
		}
	}

	public static SchemaNode ConvertToSchema(this JsonElement data, TypeFinder finder, TypesStore types,
		string name = "Root")
	{
		var node = new SchemaNode { Name = name };

		switch (data.ValueKind)
		{
			case JsonValueKind.String:
				var value = data.GetString()!;
				node = finder.ParseType(value, types).ConvertToSchema(name);
				node.IsPath = true;
				node.OriginValue = value;
				break;
			case JsonValueKind.True or JsonValueKind.False:
				node.Type = UniversalType.Boolean;
				node.OriginValue = data.ValueKind == JsonValueKind.True;
				break;
			case JsonValueKind.Number:
				node.Type = UniversalType.Number;
				node.OriginValue = data.GetDecimal();
				break;
			case JsonValueKind.Null or JsonValueKind.Undefined:
				node.Type = UniversalType.Unknown;
				break;
			case JsonValueKind.Array:
				node.Type = UniversalType.Array;
				var itemNode = new SchemaNode { Name = "Item", Type = UniversalType.Unknown };

				var schemaNodes = data.EnumerateArray()
					.Select(item => item.ConvertToSchema(finder, types, "Item"))
					.ToList();

				node.ArrayItemSchema = schemaNodes.Aggregate(itemNode, SchemaBuilder.MergeNodes);
				node.OriginValue = schemaNodes;
				break;
			case JsonValueKind.Object:
				node.Type = UniversalType.Object;
				node.Properties ??= [];

				foreach (var prop in data.EnumerateObject())
				{
					node.Properties[prop.Name] = prop.Value.ConvertToSchema(finder, types, prop.Name);
				}
				break;
		}

		return node;
	}
}