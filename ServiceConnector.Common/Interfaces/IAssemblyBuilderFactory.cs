namespace ServiceConnector.Common.Interfaces;

public interface IAssemblyBuilderFactory
{
	IAssemblyBuilder Create(string assemblyName);
}