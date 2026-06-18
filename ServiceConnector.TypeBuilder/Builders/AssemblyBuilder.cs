using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ServiceConnector.Common;
using ServiceConnector.Common.Extensions;
using ServiceConnector.Common.Interfaces;

namespace ServiceConnector.TypeBuilder.Builders;

public partial class AssemblyBuilder(string assemblyName, string ns, ILoadContextStore loadContextStore)
	: IAssemblyBuilder
{
	public string AssemblyName { get; } = IgnoredSymbolsRegex().Replace(assemblyName, "");
	public Assembly? BuiltAssembly { get; private set; }
	private readonly string _namespace = IgnoredSymbolsRegex().Replace(ns, "");
	private readonly List<ITypeBuilder> _typeBuilders = [];
	private readonly HashSet<string> _usings = [];

	public IAssemblyBuilder AddUsing(string @using)
	{
		if (!string.IsNullOrWhiteSpace(@using))
		{
			_usings.Add(@using);
		}

		return this;
	}

	public IClassBuilder CreateClass(string className)
	{
		return CreateClass<object>([], className);
	}

	public IClassBuilder CreateClass<T>(string className)
	{
		return CreateClass<T>([], className);
	}

	public IClassBuilder CreateClass<T>(List<Type> interfaces, string className)
	{
		return CreateClass(typeof(T), interfaces, className);
	}

	public IClassBuilder CreateClass(Type baseType, string className)
	{
		return CreateClass(baseType, new List<string>(), className);
	}

	public IInterfaceBuilder CreateInterface(string interfaceName)
	{
		return CreateInterface(typeof(void), interfaceName);
	}

	public IInterfaceBuilder CreateInterface<T>(string interfaceName)
	{
		return CreateInterface(typeof(T), interfaceName);
	}

	public IEnumBuilder CreateEnum(string interfaceName)
	{
		return CreateEnum("", interfaceName);
	}

	public IClassBuilder CreateClass(Type baseType, List<Type> interfaces, string className)
	{
		return CreateClass(baseType, interfaces.Select(x => x.ToDisplayString()).ToList(), className);
	}

	public IClassBuilder CreateClass(Type baseType, List<string> interfaces, string className)
	{
		var classBuilder = new ClassBuilder(baseType, interfaces, className, this);
		_typeBuilders.Add(classBuilder);
		return classBuilder;
	}

	public IInterfaceBuilder CreateInterface(Type baseType, string interfaceName)
	{
		var interfaceBuilder = new InterfaceBuilder(baseType, interfaceName, this);
		_typeBuilders.Add(interfaceBuilder);
		return interfaceBuilder;
	}

	public IEnumBuilder CreateEnum(string baseType, string interfaceName)
	{
		var enumBuilder = new EnumBuilder(baseType, interfaceName, this);
		_typeBuilders.Add(enumBuilder);
		return enumBuilder;
	}

	public List<Type> Build()
	{
		loadContextStore.Initialize();

		var refs = loadContextStore.References.Values;

		var source = $$"""
		               {{string.Join("\n", _usings.OrderBy(x => x).Select(x => $"using {x};"))}}

		               namespace {{_namespace}};

		               {{string.Join("\n\n", _typeBuilders.Select(x => x.Build()))}}
		               """;
		var syntax = CSharpSyntaxTree.ParseText(source);

		var options = new CSharpCompilationOptions(
			OutputKind.DynamicallyLinkedLibrary,
			optimizationLevel: OptimizationLevel.Release
		);

		var compilation = CSharpCompilation.Create($"{AssemblyName}GenAssembly", [syntax], refs, options);

		BuiltAssembly = loadContextStore.Load(compilation);

		return _typeBuilders.Select(builder => builder.SaveType(BuiltAssembly, _namespace)).ToList();
	}

	[GeneratedRegex(@"\W")]
	private static partial Regex IgnoredSymbolsRegex();
}