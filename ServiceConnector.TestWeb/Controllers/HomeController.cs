using Microsoft.AspNetCore.Mvc;

namespace ServiceConnector.TestWeb.Controllers;

[Route("api")]
[ApiController]
public class HomeController : ControllerBase
{
	[HttpGet("[action]")]
	public Task<TestResponse> TestGet(string request, CancellationToken token)
	{
		HttpContext.Request.Headers.TryGetValue("test-header", out var header);
		return Task.FromResult<TestResponse>(new()
		{
			Header = $"Header = {header}",
			Response = $"Request = {request}",
		});
	}

	[HttpPost("[action]")]
	public Task<TestResponse> TestPost(TestRequest request, CancellationToken token)
	{
		HttpContext.Request.Headers.TryGetValue("test-header", out var header);
		return Task.FromResult<TestResponse>(new()
		{
			Header = $"Header = {header}",
			Response = $"Request = {request.Request}",
		});
	}

	public class TestRequest
	{
		public string Request { get; set; } = null!;
	}

	public class TestResponse
	{
		public string Header { get; set; } = null!;
		public string Response { get; set; } = null!;
	}
}