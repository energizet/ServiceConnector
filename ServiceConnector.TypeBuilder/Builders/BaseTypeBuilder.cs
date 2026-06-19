using System.Text.RegularExpressions;
using ServiceConnector.Common.Interfaces;

namespace ServiceConnector.TypeBuilder.Builders;

public abstract partial class BaseTypeBuilder(string name, AssemblyBuilder assemblyBuilder) : ITypeBuilder
{
	public string Name { get; } = NameRegex().Replace($"{name}Dynamic", "_");
	public IAssemblyBuilder AssemblyBuilder => assemblyBuilder;
	public Type? BuiltType { get; private set; }

	public abstract string Build();

	public Type SaveType()
	{
		return BuiltType ??= assemblyBuilder.GetType($"{assemblyBuilder.Namespace}.{Name}")!;
	}

	[GeneratedRegex("\\W")]
	private static partial Regex NameRegex();
}