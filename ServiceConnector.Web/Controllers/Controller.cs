using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ServiceConnector.Common;

namespace ServiceConnector.Web.Controllers;

[Route("api")]
[ApiController]
public class Controller(
	IRunnerFinder finder
) : ControllerBase
{
	private static readonly JsonSerializerOptions Options = new()
	{
		PropertyNameCaseInsensitive = true,
	};

	[HttpPost(":requestId")]
	public async Task<object?> Call(string requestId, [FromBody] JsonElement? request, CancellationToken token)
	{
		var (runner, definition) = finder.Get(requestId);

		var store = new PipelineStore
		{
			["headers"] = HttpContext.Request.Headers
				.ToDictionary(item => item.Key, item => item.Value.ToString()),
			["request"] = request?.Deserialize(definition.RequestType, Options),
		};

		return await runner.Run(store, token);
	}
}