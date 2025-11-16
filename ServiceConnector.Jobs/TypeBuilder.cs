using System.Collections;
using System.Linq.Expressions;
using System.Text.Json;
using ServiceConnector.Common;
using ServiceConnector.TypeBuilder;

namespace ServiceConnector.Jobs;

public interface IArray : IEnumerable;

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
				var builder = factory.Create(typeName)
					.CreateClass<object>([typeof(IArray)], typeName);

				var elements = data.EnumerateArray().ToList();
				var enumeratorBody = new List<string>(elements.Count + 1);
				for (var i = 0; i < elements.Count; i++)
				{
					var type = BuildType(types, elements[i], $"{typeName}{i}");
					builder.CreateProperty($"Item_{i}", type);
					enumeratorBody.Add($"yield return Item_{i};");
				}

				enumeratorBody.Add("yield break;");

				builder.CreateMethod(nameof(IEnumerable.GetEnumerator), typeof(IEnumerator), "",
					string.Join("\n", enumeratorBody));

				return builder.AssemblyBuilder.Build().First();
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

	public Expression BuildObject(TypesStore types, JsonElement data, Type resultType, ParameterExpression store)
	{
		switch (data.ValueKind)
		{
			case JsonValueKind.String:
				var lambda = generator.Create().GetValue(data.GetString()!, types);
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
						BuildObject(types, elements[i], propertyInfo.PropertyType, store)
					));
				}

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
						BuildObject(types, child.Value, propertyInfo.PropertyType, store)
					));
				}

				return Expression.MemberInit(Expression.New(resultType), memberBindings);
			}
		}
	}
}