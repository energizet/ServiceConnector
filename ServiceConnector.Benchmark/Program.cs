using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

BenchmarkRunner.Run<ApiBenchmark>();

[MemoryDiagnoser]
public class ApiBenchmark
{
	// Теперь это обычный HTTP, поэтому handler для SSL не нужен
	private static readonly HttpClient client = new HttpClient();

	private const string DirectGetUrl = "http://localhost:5158/api/GetOne";
	private const string PostUrl = "http://localhost:5077/api/service-one";

	private static StringContent Content = new("{}", Encoding.UTF8, "application/json");

	// 1. GET (Direct)
	[Benchmark(Baseline = true)]
	public async Task<string> DirectGet()
	{
		return await client.GetStringAsync(DirectGetUrl);
	}

	// 3. POST (Service List)
	[Benchmark]
	public async Task<string> PostService()
	{
		// Отправляем запрос
		using var response = await client.PostAsync(PostUrl, Content);

		// Читаем тело ответа как строку (чтобы тип возврата совпадал с другими тестами)
		return await response.Content.ReadAsStringAsync();
	}
}