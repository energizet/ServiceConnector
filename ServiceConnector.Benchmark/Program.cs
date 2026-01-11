using System.Text;
using System.Text.Json.Serialization;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

BenchmarkRunner.Run<JsonDeserializationBenchmark>();

// Атрибут для генератора кода System.Text.Json (Шаг оптимизации №1)
[JsonSerializable(typeof(UserDto))]
[JsonSerializable(typeof(List<UserDto>))]
internal partial class MyJsonContext : JsonSerializerContext
{
}

// Конфигурация теста (выделяем память, смотрим среднее время)
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class JsonDeserializationBenchmark
{
	private string _jsonString;
	private byte[] _jsonBytes;
	private MemoryStream _memoryStream;

	[GlobalSetup]
	public void Setup()
	{
		// Создаем тестовые данные
		var user = new UserDto
		{
			Id = 12345,
			FirstName = "Ivan",
			LastName = "Petrov",
			Email = "ivan.petrov@example.com",
			IsActive = true,
			RegistrationDate = DateTime.UtcNow,
			Tags = ["admin", "editor", "contributor"],
			Rating = 9.8
		};

		// Сериализуем один раз, чтобы получить строку для тестов
		_jsonString = System.Text.Json.JsonSerializer.Serialize(user);
		_jsonBytes = Encoding.UTF8.GetBytes(_jsonString);
		_memoryStream = new MemoryStream(_jsonBytes);
	}

	// Вспомогательный метод для сброса потока
	private Stream GetStream()
	{
		_memoryStream.Position = 0;
		return _memoryStream;
	}

	// --- 1. System.Text.Json (Стандартный) ---
	[Benchmark(Baseline = true, Description = "Sys.Text.Json (Reflection)")]
	[BenchmarkCategory("Microsoft")]
	public UserDto SystemTextJson_Reflection()
	{
		return System.Text.Json.JsonSerializer.Deserialize<UserDto>(_jsonString)!;
	}
	
	[Benchmark(Description = "Sys.Text.Json Stream (Reflection)")]
	[BenchmarkCategory("Microsoft")]
	public async Task<UserDto> SystemTextJson_Stream_Reflection()
	{
		return (await System.Text.Json.JsonSerializer.DeserializeAsync<UserDto>(GetStream()))!;
	}
	
	[Benchmark(Description = "Sys.Text.Json Stream (SourceGen)")]
	[BenchmarkCategory("Microsoft")]
	public async Task<UserDto> SystemTextJson_Stream_SourceGen()
	{
		return (await System.Text.Json.JsonSerializer.DeserializeAsync(GetStream(), MyJsonContext.Default.UserDto))!;
	}

	// --- 2. System.Text.Json (Source Generator) - РЕКОМЕНДУЕМЫЙ ---
	// Работает быстрее, так как код парсинга создан при компиляции
	[Benchmark(Description = "Sys.Text.Json (SourceGen)")]
	[BenchmarkCategory("Microsoft")]
	public UserDto SystemTextJson_SourceGen()
	{
		return System.Text.Json.JsonSerializer.Deserialize(_jsonString, MyJsonContext.Default.UserDto)!;
	}

	// --- 3. Newtonsoft.Json (Json.NET) ---
	[Benchmark(Description = "Newtonsoft.Json")]
	[BenchmarkCategory("Legacy")]
	public UserDto NewtonsoftJson()
	{
		return Newtonsoft.Json.JsonConvert.DeserializeObject<UserDto>(_jsonString)!;
	}

	// --- 4. SpanJson ---
	// Обычно очень быстр за счет использования Span<T>
	[Benchmark(Description = "SpanJson")]
	[BenchmarkCategory("Alternative")]
	public UserDto SpanJson_Deserialize()
	{
		return SpanJson.JsonSerializer.Generic.Utf16.Deserialize<UserDto>(_jsonString);
	}
	
	[Benchmark(Description = "SpanJson Stream")]
	[BenchmarkCategory("Alternative")]
	public async Task<UserDto> SpanJson_Stream()
	{
		return await SpanJson.JsonSerializer.Generic.Utf8.DeserializeAsync<UserDto>(GetStream());
	}

	// --- 5. Utf8Json ---
	// Работает лучше с байтами, но тестируем строку для честности сравнения API
	[Benchmark(Description = "Utf8Json")]
	[BenchmarkCategory("Alternative")]
	public UserDto Utf8Json_Deserialize()
	{
		return Utf8Json.JsonSerializer.Deserialize<UserDto>(_jsonString);
	}
}

// Простой класс данных (POCO)
public class UserDto
{
	public int Id { get; set; }
	public string FirstName { get; set; }
	public string LastName { get; set; }
	public string Email { get; set; }
	public bool IsActive { get; set; }
	public DateTime RegistrationDate { get; set; }
	public List<string> Tags { get; set; }
	public double Rating { get; set; }
}

//[MemoryDiagnoser]
public class ApiBenchmark
{
	// Теперь это обычный HTTP, поэтому handler для SSL не нужен
	private static readonly HttpClient client = new HttpClient();

	private const string DirectGetUrl = "http://localhost:5158/api/GetList";
	private const string PostUrl = "http://localhost:5000/api/get-list";

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