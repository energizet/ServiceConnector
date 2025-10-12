using System.Runtime.Loader;

namespace ServiceConnector.Web.Registrars;

public class PipelineLoadContext() : AssemblyLoadContext(isCollectible: true);