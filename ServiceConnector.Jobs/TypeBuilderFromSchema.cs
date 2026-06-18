using System.Text;
using System.Text.Json;
using ServiceConnector.Common.Extensions;
using ServiceConnector.Common.Interfaces;

namespace ServiceConnector.Jobs;

public class TypeBuilderFromSchema(IAssemblyBuilderFactory factory)
{
	public Type BuildType(JsonElement data, string name)
	{
		if (data.ValueKind != JsonValueKind.Object)
		{
			throw new ArgumentException("Schema must be a JSON object");
		}

		if (!data.TryGetProperty("type", out var typeProp) || typeProp.ValueKind != JsonValueKind.String)
		{
			throw new ArgumentException($"Schema must contain 'type' (schema for {name})");
		}

		var schemaType = typeProp.GetString()!;
		switch (schemaType)
		{
			case "string":
				return typeof(string);
			case "datetime":
				return typeof(DateTime);
			case "integer":
				return typeof(int);
			case "number":
				return typeof(decimal);
			case "boolean":
				return typeof(bool);
			case "array":
			{
				if (!data.TryGetProperty("items", out var itemsProp))
				{
					throw new ArgumentException($"Array schema must contain 'items' ({name})");
				}

				var type = BuildType(itemsProp, name + "Item");
				return typeof(List<>).MakeGenericType(type);
			}
			case "object":
			{
				var builder = factory.Create(name)
					.AddUsing("ProtoBuf");

				var classBuilder = builder.CreateClass(name)
					.AddAttribute("ProtoContract");

				var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				if (data.TryGetProperty("required", out var reqProp) && reqProp.ValueKind == JsonValueKind.Array)
				{
					foreach (var r in reqProp.EnumerateArray())
					{
						required.Add(r.GetString()!);
					}
				}

				if (data.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
				{
					var number = 1;
					foreach (var property in props.EnumerateObject())
					{
						var propName = property.Name;
						var propSchema = property.Value;

						var propType = BuildType(propSchema, name + "_" + propName);
						if (!required.Contains(propName))
						{
							propType = MakeNullable(propType);
						}

						classBuilder.CreateProperty(propName, propType, attributes: $"ProtoMember({number++})");
					}
				}

				GenerateValidateMethod(classBuilder, required);

				return builder.Build().First();
			}
			default:
				throw new ArgumentException($"Unsupported schema type: {schemaType} ({name})");
		}
	}

	private static Type MakeNullable(Type type)
	{
		if (
			type.TryTo(typeof(ValueType), out _) &&
			!type.TryTo(typeof(Nullable<>), out _)
		)
		{
			return typeof(Nullable<>).MakeGenericType(type);
		}

		return type;
	}

	private static void GenerateValidateMethod(IClassBuilder classBuilder, HashSet<string> required)
	{
		if (required.Count == 0)
		{
			return;
		}

		var bodyBuilder = new StringBuilder();

		foreach (var propName in required)
		{
			bodyBuilder.AppendLine($$"""
			                         if ({{propName}} == null){
			                             throw new System.ArgumentException("{{propName}} is required");
			                         }
			                         """);
		}

		classBuilder.CreateMethod("Validate", "void", "", bodyBuilder.ToString());
	}
}