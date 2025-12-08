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
	public Dictionary<string, SchemaNode>? Properties { get; set; }
	public SchemaNode? ArrayItemSchema { get; set; }
	public Type? OriginType { get; set; }

	public override string ToString()
	{
		var originInfo = OriginType != null ? $" [{OriginType.Name}]" : "";

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
		var root = new SchemaNode { Name = "Root", Type = UniversalType.Object };

		foreach (var schema in schemas)
		{
			MergeNodes(root, schema);
		}

		return root;
	}

	public static bool MergeNodes(SchemaNode target, SchemaNode source)
	{
		var isModified = false;

		if (target.Type == UniversalType.Unknown)
		{
			target.Type = source.Type;
			target.ArrayItemSchema = source.ArrayItemSchema;
			target.OriginType = source.OriginType;
			isModified = true;
		}

		if (target.Type != source.Type && source.Type != UniversalType.Unknown)
		{
			if (!target.Type.IsContainer() && !source.Type.IsContainer())
			{
				if (target.Type != UniversalType.String)
				{
					target.Type = UniversalType.String;
					isModified = true;
				}
			}
			else
			{
				isModified = true;
			}
		}

		if (target.Type == UniversalType.Array && source.Type == UniversalType.Array)
		{
			if (target.ArrayItemSchema == null)
			{
				if (source.ArrayItemSchema != null)
				{
					target.ArrayItemSchema = source.ArrayItemSchema;
				}
			}
			else if (source.ArrayItemSchema != null)
			{
				var itemChanged = MergeNodes(target.ArrayItemSchema, source.ArrayItemSchema);
				if (itemChanged)
				{
					isModified = true;
				}
			}
		}

		if (target.Type == UniversalType.Object && source.Type == UniversalType.Object)
		{
			target.Properties ??= [];
			source.Properties ??= [];
			
			foreach (var prop in source.Properties)
			{
				if (!target.Properties.TryGetValue(prop.Key, out var property))
				{
					target.Properties[prop.Key] = prop.Value;
					isModified = true;
				}
				else
				{
					var propChanged = MergeNodes(property, prop.Value);
					if (propChanged)
					{
						isModified = true;
					}
				}
			}
		}
		
		if (isModified)
		{
			target.OriginType = null;
		}

		return isModified;
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
				OriginType = type,
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
				return finder.ParseType(data.GetString()!, types).ConvertToSchema(name);
			case JsonValueKind.True or JsonValueKind.False:
				node.Type = UniversalType.Boolean;
				break;
			case JsonValueKind.Number:
				node.Type = UniversalType.Number;
				break;
			case JsonValueKind.Null or JsonValueKind.Undefined:
				node.Type = UniversalType.Unknown;
				break;
			case JsonValueKind.Array:
				node.Type = UniversalType.Array;
				var itemNode = new SchemaNode { Name = "Item", Type = UniversalType.Unknown };
				foreach (var item in data.EnumerateArray())
				{
					var tempNode = item.ConvertToSchema(finder, types, "Item");
					SchemaBuilder.MergeNodes(itemNode, tempNode);
				}

				node.ArrayItemSchema = itemNode;
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