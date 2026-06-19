extern alias protobuf;
using System.Reflection;
using protobuf::Google.Protobuf.Reflection;

namespace ServiceConnector.Jobs.Extensions;

internal static class ProtoExtensions
{
	public static void ApplyImports(this FileDescriptorProto file)
	{
		var fdType = typeof(FileDescriptorProto);
		var addImport = fdType.GetMethod("AddImport", BindingFlags.NonPublic | BindingFlags.Instance)!;

		foreach (var dep in file.Dependencies)
		{
			var res = (bool)addImport.Invoke(file, [dep, true, null])!;
			if (!res)
			{
				throw new Exception("Error on add import");
			}
		}
	}
}