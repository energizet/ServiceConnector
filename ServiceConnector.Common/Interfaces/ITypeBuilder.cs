namespace ServiceConnector.Common.Interfaces;

public interface ITypeBuilder
{
    string Name { get; }
    IAssemblyBuilder AssemblyBuilder { get; }
    Type? BuiltType { get; }
    
    string Build();
    Type SaveType();
}