using ServiceConnector.Common;
using ServiceConnector.Common.Interfaces;
using ServiceConnector.TypeBuilder.Builders;

namespace ServiceConnector.TypeBuilder;

public class AssemblyBuilderFactory(ILoadContextStore loadContextStore, string? ns = null) : IAssemblyBuilderFactory
{
	public IAssemblyBuilder Create(string assemblyName)
	{
		return new AssemblyBuilder(assemblyName, ns ?? GetType().Namespace!, loadContextStore);
	}
}