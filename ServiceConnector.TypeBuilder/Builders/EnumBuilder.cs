using ServiceConnector.TypeBuilder.Interfaces;

namespace ServiceConnector.TypeBuilder.Builders;

public class EnumBuilder(string baseType, string name, AssemblyBuilder assemblyBuilder)
	: BaseTypeBuilder(name, assemblyBuilder), IEnumBuilder
{
	public string BaseType => baseType;
	private readonly List<string> _elements = [];

	public IEnumBuilder CreateElement(string name, int value)
	{
		_elements.Add($"    {name} = {value},");

		return this;
	}

	public override string Build()
	{
		var header = $"public enum {Name}";

		if (baseType != "")
		{
			header = $"{header} : {baseType}";
		}

		return $$"""
		         {{header}} {
		         {{string.Join('\n', _elements)}}
		         }
		         """;
	}
}