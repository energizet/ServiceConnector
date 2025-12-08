using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ServiceConnector.Common.Extensions;

public static class TypeExtensions
{
	extension(Type type)
	{
		private IEnumerable<Type> BaseClassesAndInterfaces()
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

		private IEnumerable<Type> MeAndBaseClassesAndInterfaces()
		{
			yield return type;

			foreach (var baseClassesAndInterface in type.BaseClassesAndInterfaces())
			{
				yield return baseClassesAndInterface;
			}
		}

		public MethodInfo? FindMethod(string name, params Type[] arguments)
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

		public Type? To(Type findType)
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

		public bool TryTo(Type findType, [MaybeNullWhen(false)] out Type result)
		{
			result = type.To(findType);
			return result != null;
		}
	}
}