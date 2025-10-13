using System.Reflection;
using ServiceConnector.TypeBuilder.Builders;

namespace ServiceConnector.TypeBuilder.Interfaces;

public interface ITypeBuilder
{
    string Name { get; }
    AssemblyBuilder AssemblyBuilder { get; }
    Type? BuiltType { get; }
    
    string Build();
    Type SaveType(Assembly assembly, string namespaceName);
}