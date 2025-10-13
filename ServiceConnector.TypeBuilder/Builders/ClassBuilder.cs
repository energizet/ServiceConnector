using System.Reflection;
using ServiceConnector.TypeBuilder.Interfaces;

namespace ServiceConnector.TypeBuilder.Builders;

public class ClassBuilder(Type baseType, string name, AssemblyBuilder assemblyBuilder)
	: BaseTypeBuilder(name, assemblyBuilder), IClassBuilder
{
	public Type BaseType => baseType;
	private readonly List<string> _fields = [];
	private readonly List<string> _properties = [];

	public IClassBuilder CreateField(string name, Type type, string modifier = "public")
	{
		return CreateField(name, type.ToDisplayString(), modifier);
	}

	public IClassBuilder CreateField(string name, string type, string modifier = "public")
	{
		_fields.Add(
			$$"""
			      {{modifier}} {{type}} {{name}}
			  """
		);
		return this;
	}

	public IClassBuilder CreateProperty(string name, Type type, string modifier = "public")
	{
		return CreateProperty(name, type.ToDisplayString(), modifier);
	}

	public IClassBuilder CreateProperty(string name, string type, string modifier = "public")
	{
		_properties.Add(
			$$"""
			      {{modifier}} {{type}} {{name}} { get; set; }
			  """
		);
		return this;
	}

	public override string Build()
	{
		var header = $"public sealed class {Name}";

		if (BaseType != typeof(void))
		{
			header = $"{header} : {BaseType.ToDisplayString()}";
		}

		var constructors = new List<string>();
		var baseConstructors = BaseType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
		foreach (var constructor in baseConstructors)
		{
			var parameters = new List<string>();
			var initializer = new List<string>();

			var constructorParameters = constructor.GetParameters();
			foreach (var parameter in constructorParameters)
			{
				parameters.Add($"{parameter.ParameterType.ToDisplayString()} {parameter.Name}");
				initializer.Add($"{parameter.Name}");
			}

			constructors.Add(
				$$"""
				      public {{Name}}({{string.Join(", ", parameters)}}) : base({{string.Join(", ", initializer)}}){}
				  """
			);
		}

		return $$"""
		         {{header}} {
		         {{string.Join('\n', _fields)}}
		         {{string.Join('\n', _properties)}}
		         {{string.Join('\n', constructors)}}
		         }
		         """;
	}
}