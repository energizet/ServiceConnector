using ServiceConnector.TypeBuilder.Builders;
using ServiceConnector.TypeBuilder.Interfaces;

namespace ServiceConnector.TypeBuilder;

public class AssemblyBuilderFactory(LoadContextStore loadContextStore, string? ns = null)
{
	public IAssemblyBuilder Create(string assemblyName)
	{
		return new AssemblyBuilder(assemblyName, ns ?? GetType().Namespace!, loadContextStore);
	}
}