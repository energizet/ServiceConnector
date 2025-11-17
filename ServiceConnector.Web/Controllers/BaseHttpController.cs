using Microsoft.AspNetCore.Mvc;
using ServiceConnector.Common;

namespace ServiceConnector.Web.Controllers;

public abstract class BaseHttpController(IRunnerFinder finder, IServiceProvider provider) : ControllerBase
{
	protected async Task<TResponse?> Call<TRequest, TResponse>(string requestId, TRequest request, HttpContext context,
		CancellationToken token)
	{
		var runner = finder.Get(requestId);

		var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var header in context.Request.Headers)
		{
			headers.Add(header.Key, header.Value.ToString());
		}

		var store = new PipelineStore
		{
			["headers"] = headers,
			["request"] = request,
		};

		return (TResponse?)await runner.Run(store, provider, token);
	}
}