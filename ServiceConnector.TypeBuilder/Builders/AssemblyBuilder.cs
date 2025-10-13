using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ServiceConnector.TypeBuilder.Interfaces;

namespace ServiceConnector.TypeBuilder.Builders;

public class AssemblyBuilder(string assemblyName, string ns, LoadContextStore loadContextStore) : IAssemblyBuilder
{
	public string AssemblyName => assemblyName;
	private readonly List<ITypeBuilder> _typeBuilders = [];

	public IClassBuilder CreateClass(string className)
	{
		return CreateClass<object>(className);
	}

	public IClassBuilder CreateClass<T>(string className)
	{
		return CreateClass(typeof(T), className);
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

	public IClassBuilder CreateClass(Type baseType, string className)
	{
		var classBuilder = new ClassBuilder(baseType, className, this);
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
		               namespace {{ns}};

		               {{string.Join("\n\n", _typeBuilders.Select(x => x.Build()))}}
		               """;
		var syntax = CSharpSyntaxTree.ParseText(source);

		var options = new CSharpCompilationOptions(
			OutputKind.DynamicallyLinkedLibrary,
			optimizationLevel: OptimizationLevel.Release
		);

		var compilation = CSharpCompilation.Create($"{AssemblyName}GenAssembly", [syntax], refs, options);

		var assembly = loadContextStore.Load(compilation);

		return _typeBuilders.Select(builder => builder.SaveType(assembly, ns)).ToList();
	}
}