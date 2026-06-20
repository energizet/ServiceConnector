using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ServiceConnector.Common;

public interface ILoadContextStore : IDisposable
{
	Dictionary<Assembly, MetadataReference> References { get; }
	void Initialize();
	Assembly Load(CSharpCompilation compilation);
}