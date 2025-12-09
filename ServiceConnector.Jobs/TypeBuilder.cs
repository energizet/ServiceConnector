using System.Collections;
using System.Linq.Expressions;
using System.Text.Json;
using ServiceConnector.Common;
using ServiceConnector.TypeBuilder;

namespace ServiceConnector.Jobs;

public class TypeBuilder(AssemblyBuilderFactory factory, TypeFinder finder, ExpressionGeneratorFactory generator)
{
	public Type BuildType(SchemaNode data, string typeName)
	{
		if (data.ClrType != null)
		{
			return data.ClrType;
		}

		switch (data.Type)
		{
			case UniversalType.Unknown:
				return typeof(object);
			case UniversalType.String:
				return typeof(string);
			case UniversalType.Boolean:
				return typeof(bool);
			case UniversalType.Number:
				return typeof(decimal);
			case UniversalType.DateTime:
				return typeof(DateTime);
			case UniversalType.Array:
			{
				if (data.ArrayItemSchema == null)
				{
					throw new NullReferenceException("Unknown type in array");
				}

				data.ArrayItemSchema.ClrType = BuildType(data.ArrayItemSchema, typeName);
				data.ClrType = typeof(List<>).MakeGenericType(data.ArrayItemSchema.ClrType);
				return data.ClrType;
			}
			case UniversalType.Object:
			default:
			{
				if (data.Properties == null)
				{
					throw new NullReferenceException("Unknown properties in object");
				}

				var builder = factory.Create(typeName)
					.AddUsing("ProtoBuf");

				var classBuilder = builder.CreateClass(typeName)
					.AddAttribute("ProtoContract");

				var i = 1;
				foreach (var child in data.Properties.Values)
				{
					child.ClrType = BuildType(child, $"{typeName}_{child.Name}");
					classBuilder.CreateProperty(child.Name, child.ClrType, attributes: $"ProtoMember({i++})");
				}

				return builder.Build().First();
			}
		}
	}

	public Expression BuildObject(TypesStore types, SchemaNode data, Type resultType, ParameterExpression store,
		ILinker linker)
	{
		if (data.IsPath)
		{
			var lambda = generator.Create(linker).GetValue((string)data.OriginValue!, types);
			return Expression.Invoke(lambda, store);
		}

		switch (data.Type)
		{
			case UniversalType.Unknown:
				return Expression.Constant(null);
			case UniversalType.String:
				return Expression.Constant((string)data.OriginValue!);
			case UniversalType.Boolean:
				return Expression.Constant((bool)data.OriginValue!);
			case UniversalType.Number:
				return Expression.Constant((decimal)data.OriginValue!);
			case UniversalType.DateTime:
				return Expression.Constant((DateTime)data.OriginValue!);
			case UniversalType.Array:
			{
				if (data.ArrayItemSchema?.ClrType == null)
				{
					throw new NullReferenceException("Unknown type in array");
				}

				var listInitializers = new List<ElementInit>();

				var schemaNodes = (List<SchemaNode>)data.OriginValue!;

				var propertyInfo = resultType.GetMethod("Add")!;
				foreach (var node in schemaNodes)
				{
					listInitializers.Add(Expression.ElementInit(
						propertyInfo,
						BuildObject(types, node, data.ArrayItemSchema.ClrType, store, linker)
					));
				}

				return Expression.ListInit(Expression.New(resultType), listInitializers);
			}
			case UniversalType.Object:
			default:
			{
				if (data.Properties == null)
				{
					throw new NullReferenceException("Unknown properties in object");
				}

				var memberBindings = new List<MemberAssignment>();

				foreach (var child in data.Properties.Values)
				{
					var propertyInfo = resultType.GetProperty(child.Name)!;
					memberBindings.Add(Expression.Bind(
						propertyInfo,
						BuildObject(types, child, propertyInfo.PropertyType, store, linker)
					));
				}

				return Expression.MemberInit(Expression.New(resultType), memberBindings);
			}
		}
	}

	public SchemaNode GetSchema(TypesStore types, JsonElement data)
	{
		return data.ConvertToSchema(finder, types);
	}

	//public Type BuildArray(TypesStore types, JsonElement data, string typeName)
	//{
	//	var elements = data.EnumerateArray().ToList();
	//	var newTypes = elements.Select((x, i) => BuildType(types, x, $"{typeName}{i}")).ToList();

	//	return BuildArray(typeName, newTypes);
	//}

	public Type BuildArray(string name, List<Type> types, Type? otherType = null)
	{
		if (otherType == null && types.Count == 1)
		{
			return typeof(List<>).MakeGenericType(types[0]);
		}

		if (otherType != null && types.Count == 0)
		{
			return typeof(List<>).MakeGenericType(otherType);
		}

		return BuildArrayInternal(name, types, otherType);
	}

	private Type BuildArrayInternal(string name, List<Type> types, Type? otherType = null)
	{
		var builder = factory.Create(name)
			.CreateClass<object>([typeof(IArray)], name);

		var enumeratorBody = new List<string>(types.Count + 1);
		var getBody = new List<string>(types.Count + 2)
		{
			"""
			if (index < 0 || index >= Count())
			{
			    return null;
			}

			return index switch
			{
			"""
		};

		for (var i = 0; i < types.Count; i++)
		{
			var type = types[i];
			builder.CreateProperty($"Item_{i}", type, "public required");
			enumeratorBody.Add($"yield return Item_{i};");
			getBody.Add($"    {i} => Item_{i},");
		}

		var otherListType = otherType == null ? typeof(List<object>) : typeof(List<>).MakeGenericType(otherType);
		builder.CreateProperty("Item_Others", otherListType, "public required");

		enumeratorBody.Add("""
		                   foreach (var other in (Item_Others ?? []))
		                   {
		                       yield return other;
		                   }
		                   """);
		getBody.Add("""
		                _ => (Item_Others ?? [])[index - StaticCount()],
		            };
		            """);

		builder.CreateMethod(nameof(IArray.GetEnumerator), typeof(IEnumerator), "",
			string.Join("\n", enumeratorBody));

		builder.CreateMethod(nameof(IArray.Count), typeof(int), "",
			"return StaticCount() + (Item_Others ?? []).Count;");
		builder.CreateMethod(nameof(IArray.Get), typeof(object), "int index",
			string.Join("\n", getBody));
		builder.CreateMethod(nameof(IArray.IsOnlyStatic), typeof(bool), "",
			$"return {(otherType == null ? "true" : "false")};", "public static");
		builder.CreateMethod(nameof(IArray.StaticCount), typeof(int), "",
			$"return {types.Count};", "public static");

		return builder.AssemblyBuilder.Build().First();
	}

	public Expression BuildObject(TypesStore types, string data, ParameterExpression store, ILinker linker)
	{
		var lambda = generator.Create(linker).GetValue(data, types);
		return Expression.Invoke(lambda, store);
	}

	//public Expression BuildArray(TypesStore types, JsonElement data, Type resultType, ParameterExpression store,
	//	ILinker linker)
	//{
	//	var memberBindings = new List<MemberAssignment>();

	//	var elements = data.EnumerateArray().ToList();
	//	for (var i = 0; i < elements.Count; i++)
	//	{
	//		var propertyInfo = resultType.GetProperty($"Item_{i}")!;
	//		memberBindings.Add(Expression.Bind(
	//			propertyInfo,
	//			BuildObject(types, elements[i], propertyInfo.PropertyType, store, linker)
	//		));
	//	}

	//	memberBindings.Add(Expression.Bind(
	//		resultType.GetProperty("Item_Others")!,
	//		Expression.New(typeof(List<object>))
	//	));

	//	return Expression.MemberInit(Expression.New(resultType), memberBindings);
	//}
}