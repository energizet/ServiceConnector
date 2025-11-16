using System.Reflection;

namespace ServiceConnector.Common;

public interface IIControllerGenerator
{
	Type? HttpControllerType { get; }
	Type? GrpcControllerType { get; }
	Assembly Generate();
	void AddMethod(string requestId, Type requestType, Type resultType);
}