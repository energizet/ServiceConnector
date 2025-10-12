using Microsoft.Extensions.Options;
using ServiceConnector.Web.Configs;

namespace ServiceConnector.Web.Registrars;

public class ServiceConnectorRegistrar(
	ILogger<ServiceConnectorRegistrar> logger,
	RequestPipelineLoader loader,
	IOptions<ServiceConnectorConfig> config
) : IHostedService
{
	private ServiceConnectorConfig _config = config.Value;

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		var files = Directory.GetFiles(_config.PipelinesPath, ServiceConnectorConfig.Filter,
			SearchOption.AllDirectories);
		await Create(files);
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
	}

	private async Task Create(params string[] files)
	{
		var pipelines = await loader.Read(files);
		await Load(pipelines);
	}

	private async Task Load(List<PipelineDefinition> definitions)
	{
		var requestIds = new HashSet<string>();

		var successCount = 0;
		var errorCount = 0;

		foreach (var definition in definitions)
		{
			var requestId = definition.RequestId;
			if (requestIds.Contains(requestId))
			{
				logger.LogError("[{RequestId}] Request already exist", requestId);
				continue;
			}

			try
			{
				await Compile(definition);
				requestIds.Add(requestId);
				logger.LogInformation("[{RequestId}] Request success load", requestId);
				successCount++;
			}
			catch (Exception ex)
			{
				logger.LogError("[{RequestId}] Request error load ({Message})", requestId, ex.Message);
				errorCount++;
			}
		}

		logger.LogInformation("Success loaded {SuccessLoadCount}{NL}Load errors {ErrorLoadCount}",
			successCount, Environment.NewLine, errorCount);
	}

	private async Task Compile(PipelineDefinition definition)
	{
		var types = new TypesStore
		{
			["headers"] = typeof(IDictionary<string, string>),
			["request"] = typeof(object),
		};

		foreach (var element in definition.Pipeline)
		{
		}
	}
}