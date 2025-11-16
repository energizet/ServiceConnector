using Grpc.Core;
using ServiceConnector.Common;

namespace ServiceConnector.Web.Controllers;

public abstract class BaseGrpcController(IRunnerFinder finder)
{
	protected async Task<TResponse?> Call<TRequest, TResponse>(string requestId, TRequest request,
		ServerCallContext context)
	{
		var runner = finder.Get(requestId);

		var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var header in context.GetHttpContext().Request.Headers)
		{
			headers.Add(header.Key, header.Value.ToString());
		}

		var store = new PipelineStore
		{
			["headers"] = headers,
			["request"] = request,
		};

		return (TResponse?)await runner.Run(store, context.CancellationToken);
	}
}