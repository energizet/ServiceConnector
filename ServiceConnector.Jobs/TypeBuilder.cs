using System.Collections;
using System.Linq.Expressions;
using System.Text.Json;
using ServiceConnector.Common;
using ServiceConnector.Common.Interfaces;

namespace ServiceConnector.Jobs;

public class TypeBuilder(IAssemblyBuilderFactory factory, TypeFinder finder, ExpressionGeneratorFactory generator)
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

				return data.ClrType = builder.Build().Types!.First();
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
					var propertyInfo = resultType.GetProperties()
						.First(x => string.Equals(x.Name, child.Name, StringComparison.CurrentCultureIgnoreCase));
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

	public Expression BuildObject(TypesStore types, string data, ParameterExpression store, ILinker linker)
	{
		var lambda = generator.Create(linker).GetValue(data, types);
		return Expression.Invoke(lambda, store);
	}
}