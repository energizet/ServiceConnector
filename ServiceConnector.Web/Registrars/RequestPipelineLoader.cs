using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ServiceConnector.Common;
using ServiceConnector.TypeBuilder;

namespace ServiceConnector.Web.Registrars;

public class RequestPipelineLoader(
	ILogger<RequestPipelineLoader> logger
)
{
	public async Task<List<PipelineDefinition>> Read(string[] files, CancellationToken cancellationToken)
	{
		var definitions = new List<PipelineDefinition>();

		foreach (var file in files.Select(x => Deserialize(x, cancellationToken)))
		{
			await foreach (var definition in file.WithCancellation(cancellationToken))
			{
				definitions.Add(definition);
			}
		}

		return definitions;
	}

	private async IAsyncEnumerable<PipelineDefinition> Deserialize(string file,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var (json, hash) = await GetFile(file, cancellationToken);

		if (!TryDeserialize(file, json, out var pipelines))
		{
			yield break;
		}

		var loadContext = new LoadContextStore(new PipelineLoadContext());
		foreach (var element in pipelines!)
		{
			if (!TryDeserialize<PipelineDefinition>(element, out var definition))
			{
				continue;
			}

			if (definition == null)
			{
				throw new ArgumentException($"Invalid pipeline definition in file {file}");
			}

			definition.RequestType = typeof(string); //new { Name = "" }.GetType(); // TODO definition.Request
			definition.File = file;
			definition.FileHash = hash;
			definition.LoadContext = loadContext;

			yield return definition;
		}
	}

	private bool TryDeserialize<T>(JsonElement node, out T? res)
		where T : class
	{
		try
		{
			res = node.Deserialize<T>();
			return true;
		}
		catch (JsonException ex)
		{
			logger.LogError(ex, "Pipeline not load {Message}{NL}{Pipeline}",
				ex.Message, Environment.NewLine, JsonSerializer.Serialize(node)
			);
			res = null;
			return false;
		}
	}

	private bool TryDeserialize(string file, string json, out JsonElement[]? res)
	{
		try
		{
			res = JsonSerializer.Deserialize<JsonElement[]>(json);
			return true;
		}
		catch (JsonException ex)
		{
			logger.LogError(ex, "{File} not load {Message}", file, ex.Message);
			res = null;
			return false;
		}
	}

	private async Task<(string json, string hash)> GetFile(string file, CancellationToken cancellationToken)
	{
		while (true)
		{
			try
			{
				var bytes = await File.ReadAllBytesAsync(file, cancellationToken);

				var json = Encoding.UTF8.GetString(bytes);
				var hash = string.Join("", SHA1.HashData(bytes).Select(x => x.ToString("x2")));

				return (json, hash);
			}
			catch (IOException ex)
			{
				logger.LogError(ex, "Try read locked file");
				await Task.Delay(1000, cancellationToken);
			}
		}
	}
}