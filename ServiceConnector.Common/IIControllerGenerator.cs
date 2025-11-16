using System.Reflection;

namespace ServiceConnector.Common;

public interface IIControllerGenerator
{
	Assembly Generate();
	void AddMethod(string requestId, Type requestType, Type resultType);
}