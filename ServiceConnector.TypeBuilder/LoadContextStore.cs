using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace ServiceConnector.TypeBuilder;

public sealed class LoadContextStore(AssemblyLoadContext loadContext) : IDisposable
{
	public HashSet<Assembly> StoredAssemblies = new();
	public Dictionary<Assembly, MetadataReference> References { get; } = new();

	private readonly Lock _lock = new();

	public void Initialize()
	{
		lock (_lock)
		{
			var allAssemblies = AssemblyLoadContext.All
				.SelectMany(x => x.Assemblies)
				.Where(x => !StoredAssemblies.Contains(x))
				.ToList();

			foreach (var assembly in allAssemblies)
			{
				StoredAssemblies.Add(assembly);
			}

			var assemblies = allAssemblies
				.Where(x => !References.ContainsKey(x))
				.Where(x => !string.IsNullOrWhiteSpace(x.Location))
				.Select(x => (assembly: x, stream: new MemoryStream(File.ReadAllBytes(x.Location))))
				.ToList();

			foreach (var (assembly, stream) in assemblies)
			{
				References[assembly] = MetadataReference.CreateFromStream(stream);
				stream.Dispose();
			}
		}
	}

	public Assembly Load(CSharpCompilation compilation)
	{
		using var memoryStream = new MemoryStream();
		var result = compilation.Emit(memoryStream);
		ThrowIfError(result);

		memoryStream.Position = 0;
		var assembly = loadContext.LoadFromStream(memoryStream);
		References[assembly] = compilation.ToMetadataReference();

		return assembly;
	}

	public void Unload()
	{
		lock (_lock)
		{
			foreach (var (_, reference) in References)
			{
				if (reference is PortableExecutableReference portableExecutableReference)
				{
					portableExecutableReference.GetMetadata().Dispose();
				}
			}
			
			References.Clear();
			StoredAssemblies.Clear();
			
			loadContext.Unload();
		}
	}

	private static void ThrowIfError(EmitResult result)
	{
		if (result.Success)
		{
			return;
		}

		var compilationErrors = result.Diagnostics
			.Where(diagnostic =>
				diagnostic.IsWarningAsError ||
				diagnostic.Severity == DiagnosticSeverity.Error
			).ToList();

		if (compilationErrors.Count == 0)
		{
			return;
		}

		var firstError = compilationErrors.First();
		var errorNumber = firstError.Id;
		var errorDescription = firstError.GetMessage();
		var firstErrorMessage = $"{errorNumber}: {errorDescription};";
		var exception = new Exception($"Compilation failed, first error is: {firstErrorMessage}");

		foreach (var error in compilationErrors.Where(error => !exception.Data.Contains(error.Id)))
		{
			exception.Data.Add(error.Id, error.GetMessage());
		}

		throw exception;
	}

	public void Dispose()
	{
		References.Clear();
	}
}