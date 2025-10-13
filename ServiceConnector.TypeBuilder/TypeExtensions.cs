using System.Reflection;

namespace ServiceConnector.TypeBuilder;

public static class TypeExtensions
{
	private static IEnumerable<Type> BaseClassesAndInterfaces(this Type type)
	{
		var currentType = type;
		while ((currentType = currentType.BaseType) != null)
		{
			yield return currentType;
		}

		foreach (var interfaceType in type.GetInterfaces())
		{
			yield return interfaceType;
		}
	}

	private static IEnumerable<Type> MeAndBaseClassesAndInterfaces(this Type type)
	{
		yield return type;

		foreach (var baseClassesAndInterface in type.BaseClassesAndInterfaces())
		{
			yield return baseClassesAndInterface;
		}
	}

	public static MethodInfo? FindMethod(this Type type, string name, params Type[] arguments)
	{
		return type.GetMethods()
			.Where(item => item.Name == name)
			.FirstOrDefault(item =>
			{
				var parameters = item.GetParameters();
				var res = parameters.Length == arguments.Length;

				for (var i = 0; i < arguments.Length && res; i++)
				{
					var parameter = parameters[i];
					var argument = arguments[i];

					res = res && parameter.ParameterType == argument;
				}

				return res;
			});
	}

	public static Type? To(this Type type, Type findType)
	{
		if (!findType.IsGenericType || !findType.IsGenericTypeDefinition)
		{
			return type
				.MeAndBaseClassesAndInterfaces()
				.FirstOrDefault(item => item == findType);
		}

		return type
			.MeAndBaseClassesAndInterfaces()
			.FirstOrDefault(item =>
				item.IsGenericType && item.GetGenericTypeDefinition() == findType);
	}

	public static string ToDisplayString(this Type type)
	{
		var nameSpace = type.Namespace;

		var typeName = type.Name.Split('`')[0];

		if (type.IsGenericType)
		{
			typeName = $"{typeName}<{string.Join(", ",
				type.GetGenericArguments().Select(t => t.ToDisplayString()))}>";
		}

		return !string.IsNullOrWhiteSpace(nameSpace)
			? $"{nameSpace}.{typeName}"
			: typeName;
	}
}