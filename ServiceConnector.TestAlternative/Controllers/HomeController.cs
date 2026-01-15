using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace ServiceConnector.TestAlternative.Controllers;

[Route("api/[action]")]
[ApiController]
public class HomeController(HttpClient client) : ControllerBase
{
	// Адрес сервиса, куда пересылаем запрос
	private const string TargetUrl = "http://localhost:5158/api/GetList";
	private const string TargetOneUrl = "http://localhost:5158/api/GetOne"; // Для примера

	[HttpPost]
	public async Task<Res> GetList([FromBody] Req req, CancellationToken token)
	{
		// 1. Проксируем POST запрос: сериализуем Req в JSON и отправляем
		using var response = await client.PostAsJsonAsync(TargetUrl, req, token);

		// 2. Если сервис ответил ошибкой (например, 500 или 404), прерываемся
		response.EnsureSuccessStatusCode();

		// 3. Читаем ответ. 
		// ПРЕДПОЛОЖЕНИЕ: Целевой сервис (5158) возвращает JSON-массив (List<TestResponse>),
		// а не объект Res. Если он возвращает Res, замените тип ниже.
		var remoteCollection = await response.Content.ReadFromJsonAsync<Res>(new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
		}, token);

		// 4. Оборачиваем результат в ваш формат Res
		return remoteCollection;
	}

	[HttpGet]
	public async Task<TestResponse?> GetOne(CancellationToken token)
	{
		// Проксирование GET запроса еще проще
		return await client.GetFromJsonAsync<TestResponse>(TargetOneUrl, token);
	}

	// --- Ваши DTO классы ---
	public class Req
	{
		public int Count { get; set; }
	}

	public class Res
	{
		public List<TestResponse> Collection { get; set; } = new();
	}

	public class TestResponse
	{
		public string Header { get; set; } = null!;
		public string Response { get; set; } = null!;
	}
}