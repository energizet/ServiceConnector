using System.Collections;
using System.Reflection;

namespace ServiceConnector.Jobs;

public interface IArray : IEnumerable
{
	int Count();
	object? Get(int index);
	static abstract bool IsOnlyStatic();
	static abstract int StaticCount();

	public static bool IsOnlyStatic(Type type)
	{
		var isOnlyStatic = type.GetMethod(nameof(IArray.IsOnlyStatic), BindingFlags.Public | BindingFlags.Static)!;
		var isOnlyStaticValue = (bool)isOnlyStatic.Invoke(type, [])!;
		return isOnlyStaticValue;
	}

	public static int StaticCount(Type type)
	{
		var staticCount = type.GetMethod(nameof(IArray.StaticCount), BindingFlags.Public | BindingFlags.Static)!;
		var staticCountValue = (int)staticCount.Invoke(type, [])!;
		return staticCountValue;
	}
}