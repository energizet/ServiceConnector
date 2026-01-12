using System.Text.Json;
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
	public Task<Res> GetList(Req req, CancellationToken token)
	{
		var list = Enumerable.Range(0, req.Count).Select(i => new TestResponse
		{
			Response = $"{i}",
		}).ToList();
		var a = JsonSerializer.Serialize(list);
		return Task.FromResult(new Res
		{
			Collection = JsonSerializer.Deserialize<List<TestResponse>>(a)!.Select(i => new TestResponse
			{
				Header = i.Response,
				Response = i.Response,
			}).ToList(),
		});
	}

	[HttpGet("[action]")]
	public Task<TestResponse> GetOne(CancellationToken token)
	{
		return Task.FromResult(new TestResponse
		{
			Header = $"1",
			Response = $"1",
		});
	}
	
	public class Req
	{
		public int Count { get; set; }
	}

	public class Res
	{
		public List<TestResponse> Collection { get; set; }
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