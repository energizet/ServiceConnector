using System.Reflection;

namespace ServiceConnector.TypeBuilder.Interfaces;

public interface IAssemblyBuilder
{
	string AssemblyName { get; }
	Assembly? BuiltAssembly { get; }

	IAssemblyBuilder AddUsing(string @using);
	IClassBuilder CreateClass(string name);
	IClassBuilder CreateClass<T>(string name);
	IClassBuilder CreateClass<T>(List<Type> interfaces, string name);
	IClassBuilder CreateClass(Type baseType, string name);
	IClassBuilder CreateClass(Type baseType, List<Type> interfaces, string name);
	IClassBuilder CreateClass(Type baseType, List<string> interfaces, string name);
	IInterfaceBuilder CreateInterface(string name);
	IInterfaceBuilder CreateInterface<T>(string name);
	IInterfaceBuilder CreateInterface(Type baseType, string name);
	IEnumBuilder CreateEnum(string name);

	IEnumBuilder CreateEnum(string baseType, string name);

	List<Type> Build();
}