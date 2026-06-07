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