namespace ServiceConnector.TypeBuilder.Interfaces;

public interface IInterfaceBuilder : ITypeBuilder
{
	Type BaseType { get; }

	IInterfaceBuilder CreateMethod(string name, Type returnType, List<string> methodParameters);

	IInterfaceBuilder CreateMethod(string name, string returnType, List<string> methodParameters);
}