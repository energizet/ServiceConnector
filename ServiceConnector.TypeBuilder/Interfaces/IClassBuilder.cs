namespace ServiceConnector.TypeBuilder.Interfaces;

public interface IClassBuilder : ITypeBuilder
{
	Type BaseType { get; }

	IClassBuilder CreateField(string name, Type type, string modifier = "public");

	IClassBuilder CreateField(string name, string type, string modifier = "public");

	IClassBuilder CreateProperty(string name, Type type, string modifier = "public");

	IClassBuilder CreateProperty(string name, string type, string modifier = "public");
}