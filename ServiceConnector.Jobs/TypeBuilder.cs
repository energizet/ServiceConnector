using System.Collections;
using System.Linq.Expressions;
using System.Text.Json;
using ServiceConnector.Common;
using ServiceConnector.TypeBuilder;

namespace ServiceConnector.Jobs;

public class TypeBuilder(AssemblyBuilderFactory factory, TypeFinder finder, ExpressionGeneratorFactory generator)
{
	public Type BuildType(TypesStore types, JsonElement data, string typeName)
	{
		switch (data.ValueKind)
		{
			case JsonValueKind.String:
				return finder.ParseType(data.GetString()!, types);
			case JsonValueKind.True or JsonValueKind.False:
				return typeof(bool);
			case JsonValueKind.Number:
				return typeof(decimal);
			case JsonValueKind.Null or JsonValueKind.Undefined:
				return typeof(object);
			case JsonValueKind.Array:
			{
				var elements = data.EnumerateArray().ToList();
				var newTypes = elements.Select((x, i) => BuildType(types, x, $"{typeName}{i}")).ToList();

				return BuildArray(typeName, newTypes);
			}
			case JsonValueKind.Object:
			default:
			{
				var builder = factory.Create(typeName)
					.AddUsing("ProtoBuf");

				var classBuilder = builder.CreateClass(typeName)
					.AddAttribute("ProtoContract");

				var number = 1;
				foreach (var child in data.EnumerateObject())
				{
					var type = BuildType(types, child.Value, typeName + child.Name);
					classBuilder.CreateProperty(child.Name, type, attributes: $"ProtoMember({number++})");
				}

				return builder.Build().First();
			}
		}
	}

	public Type BuildArray(string name, List<Type> types, Type? otherType = null)
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

	public Expression BuildObject(TypesStore types, JsonElement data, Type resultType, ParameterExpression store,
		ILinker linker)
	{
		switch (data.ValueKind)
		{
			case JsonValueKind.String:
				var lambda = generator.Create(linker).GetValue(data.GetString()!, types);
				return Expression.Invoke(lambda, store);
			case JsonValueKind.True:
				return Expression.Constant(true);
			case JsonValueKind.False:
				return Expression.Constant(false);
			case JsonValueKind.Number:
				return Expression.Constant(data.GetDecimal());
			case JsonValueKind.Null or JsonValueKind.Undefined:
				return Expression.Constant(null);
			case JsonValueKind.Array:
			{
				var memberBindings = new List<MemberAssignment>();

				var elements = data.EnumerateArray().ToList();
				for (var i = 0; i < elements.Count; i++)
				{
					var propertyInfo = resultType.GetProperty($"Item_{i}")!;
					memberBindings.Add(Expression.Bind(
						propertyInfo,
						BuildObject(types, elements[i], propertyInfo.PropertyType, store, linker)
					));
				}

				memberBindings.Add(Expression.Bind(
					resultType.GetProperty("Item_Others")!,
					Expression.New(typeof(List<object>))
				));

				return Expression.MemberInit(Expression.New(resultType), memberBindings);
			}
			case JsonValueKind.Object:
			default:
			{
				var memberBindings = new List<MemberAssignment>();

				foreach (var child in data.EnumerateObject())
				{
					var propertyInfo = resultType.GetProperty(child.Name)!;
					memberBindings.Add(Expression.Bind(
						propertyInfo,
						BuildObject(types, child.Value, propertyInfo.PropertyType, store, linker)
					));
				}

				return Expression.MemberInit(Expression.New(resultType), memberBindings);
			}
		}
	}
}