using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ServiceConnector.Common;
using ServiceConnector.Jobs;
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

		var generator = new ControllerGenerator(loadContext, Path.GetFileName(file));

		foreach (var element in pipelines!)
		{
			if (!TryDeserialize(element, out var definition))
			{
				continue;
			}

			if (definition == null)
			{
				throw new ArgumentException($"Invalid pipeline definition in file {file}");
			}

			definition.File = file;
			definition.FileHash = hash;
			definition.LoadContext = loadContext;
			definition.ControllerGenerator = generator;

			yield return definition;
		}
	}

	private bool TryDeserialize(JsonElement node, out PipelineDefinition? res)
	{
		try
		{
			res = node.Deserialize(AppJsonSerializerContext.Default.PipelineDefinition);
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
			res = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.JsonElementArray);
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

[JsonSerializable(typeof(JsonElement[]))]
[JsonSerializable(typeof(PipelineDefinition))]
internal partial class AppJsonSerializerContext : JsonSerializerContext;