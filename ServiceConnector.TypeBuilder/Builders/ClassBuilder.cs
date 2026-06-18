using System.Reflection;
using ServiceConnector.Common.Interfaces;
using ServiceConnector.Common.Extensions;

namespace ServiceConnector.TypeBuilder.Builders;

public class ClassBuilder(Type baseType, List<string> interfaces, string name, AssemblyBuilder assemblyBuilder)
	: BaseTypeBuilder(name, assemblyBuilder), IClassBuilder
{
	public Type BaseType => baseType;
	private readonly List<string> _attributes = [];
	private readonly List<string> _fields = [];
	private readonly List<string> _properties = [];
	private readonly List<string> _methods = [];

	public IClassBuilder AddAttribute(string attribute)
	{
		_attributes.Add($"[{attribute}]");
		return this;
	}

	public IClassBuilder CreateField(string name, Type type, string modifier = "public", params string[] attributes)
	{
		var underlying = Nullable.GetUnderlyingType(type) ?? type;
		return CreateField(name, underlying.ToDisplayString(), modifier, attributes);
	}

	public IClassBuilder CreateField(string name, string type, string modifier = "public", params string[] attributes)
	{
		var str = $$"""
		                {{modifier}} {{type}} {{name}};
		            """;
		if (attributes.Length > 0)
		{
			str = string.Join('\n', attributes.Select(x => $"    [{x}]")) + '\n' + str;
		}

		_fields.Add(str);
		return this;
	}

	public IClassBuilder CreateProperty(string name, Type type, string modifier = "public", params string[] attributes)
	{
		var underlying = Nullable.GetUnderlyingType(type) ?? type;
		return CreateProperty(name, underlying.ToDisplayString(), modifier, attributes);
	}

	public IClassBuilder CreateProperty(string name, string type, string modifier = "public",
		params string[] attributes)
	{
		var str = $$"""
		                {{modifier}} {{type}}? {{name}} { get; set; }
		            """;
		if (attributes.Length > 0)
		{
			str = string.Join('\n', attributes.Select(x => $"    [{x}]")) + '\n' + str;
		}

		_properties.Add(str);
		return this;
	}

	public IClassBuilder CreateMethod(string name, Type type, string arguments, string body, string modifier = "public",
		params string[] attributes)
	{
		return CreateMethod(name, type.ToDisplayString(), arguments, body, modifier, attributes);
	}

	public IClassBuilder CreateMethod(string name, string type, string arguments, string body,
		string modifier = "public", params string[] attributes)
	{
		var str = $$"""
		                {{modifier}} {{type}} {{name}}({{arguments}})
		                {
		                    {{string.Join("\n        ", body.Replace("\r\n", "\n").Split("\n"))}}
		                }
		            """;
		if (attributes.Length > 0)
		{
			str = string.Join('\n', attributes.Select(x => $"    [{x}]")) + '\n' + str;
		}

		_methods.Add(str);
		return this;
	}

	public override string Build()
	{
		var header = $"public sealed class {Name} : {BaseType.ToDisplayString()}";

		if (interfaces.Count > 0)
		{
			header = $"{header}, {string.Join(", ", interfaces)}";
		}

		var constructors = new List<string>();
		var publicConstructors = BaseType.GetConstructors();
		var privateConstructors = BaseType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance);
		var baseConstructors = publicConstructors.Union(privateConstructors).Where(x => !x.IsPrivate);
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
		         {{string.Join('\n', _attributes)}}
		         {{header}} {
		         {{string.Join('\n', _fields)}}
		         {{string.Join('\n', _properties)}}
		         {{string.Join('\n', constructors)}}
		         {{string.Join('\n', _methods)}}
		         }
		         """;
	}
}