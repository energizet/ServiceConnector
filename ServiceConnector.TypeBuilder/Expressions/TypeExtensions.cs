namespace ServiceConnector.TypeBuilder.Expressions;

public static class TypeExtensions
{
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