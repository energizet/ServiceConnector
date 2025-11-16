using System.Reflection;
using System.Text.RegularExpressions;
using ServiceConnector.TypeBuilder.Interfaces;

namespace ServiceConnector.TypeBuilder.Builders;

public abstract partial class BaseTypeBuilder(string name, AssemblyBuilder assemblyBuilder) : ITypeBuilder
{
	public string Name { get; } = NameRegex().Replace($"{name}Dynamic", "_");
	public AssemblyBuilder AssemblyBuilder => assemblyBuilder;
	public Type? BuiltType { get; private set; }

	public abstract string Build();

	public Type SaveType(Assembly assembly, string namespaceName)
	{
		return BuiltType = assembly.GetType($"{namespaceName}.{Name}")!;
	}

	[GeneratedRegex("\\W")]
	private static partial Regex NameRegex();
}