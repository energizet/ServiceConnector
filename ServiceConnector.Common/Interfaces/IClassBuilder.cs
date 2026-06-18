namespace ServiceConnector.Common.Interfaces;

public interface IClassBuilder : ITypeBuilder
{
	Type BaseType { get; }

	IClassBuilder AddAttribute(string attribute);

	IClassBuilder CreateField(string name, Type type, string modifier = "public", params string[] attributes);

	IClassBuilder CreateField(string name, string type, string modifier = "public", params string[] attributes);

	IClassBuilder CreateProperty(string name, Type type, string modifier = "public", params string[] attributes);

	IClassBuilder CreateProperty(string name, string type, string modifier = "public", params string[] attributes);

	IClassBuilder CreateMethod(string name, Type type, string arguments, string body, string modifier = "public",
		params string[] attributes);

	IClassBuilder CreateMethod(string name, string type, string arguments, string body, string modifier = "public",
		params string[] attributes);
}