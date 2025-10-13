using ServiceConnector.TypeBuilder.Interfaces;

namespace ServiceConnector.TypeBuilder.Builders;

public sealed class InterfaceBuilder(Type baseType, string name, AssemblyBuilder assemblyBuilder)
	: BaseTypeBuilder(name, assemblyBuilder), IInterfaceBuilder
{
	public Type BaseType => baseType;
	private readonly List<string> _methods = [];

	public IInterfaceBuilder CreateMethod(string name, Type returnType, List<string> methodParameters)
	{
		return CreateMethod(name, returnType.ToDisplayString(), methodParameters);
	}

	public IInterfaceBuilder CreateMethod(string name, string returnType, List<string> methodParameters)
	{
		_methods.Add($"    {returnType} {name}({string.Join(", ", methodParameters)});");
		return this;
	}

	public override string Build()
	{
		var header = $"public interface {Name}";

		if (baseType != typeof(void))
		{
			header = $"{header} : {baseType.ToDisplayString()}";
		}

		return $$"""
		         {{header}} {
		         {{string.Join('\n', _methods)}}
		         }
		         """;
	}
}