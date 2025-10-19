using Microsoft.Extensions.Options;
using ServiceConnector.Common;
using ServiceConnector.Web.Configs;

namespace ServiceConnector.Web.Registrars;

public class ServiceConnectorRegistrar(
	ILogger<ServiceConnectorRegistrar> logger,
	RequestPipelineLoader loader,
	JobBuilder jobBuilder,
	IOptions<ServiceConnectorConfig> config
) : IHostedService
{
	private ServiceConnectorConfig _config = config.Value;

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		var files = Directory.GetFiles(_config.PipelinesPath, ServiceConnectorConfig.Filter,
			SearchOption.AllDirectories);
		await Create(files, cancellationToken);
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}

	private async Task Create(string[] files, CancellationToken cancellationToken)
	{
		var pipelines = await loader.Read(files, cancellationToken);
		await Load(pipelines, cancellationToken);
	}

	private async Task Load(List<PipelineDefinition> definitions, CancellationToken cancellationToken)
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
				await Compile(definition, cancellationToken);
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

	private async Task Compile(PipelineDefinition definition, CancellationToken cancellationToken)
	{
		var types = new TypesStore
		{
			["headers"] = typeof(IDictionary<string, string>),
			["request"] = new { Name = "", }.GetType(),
		};

		var graphBuilder = new JobGraph.Builder();
		foreach (var element in definition.Pipeline)
		{
			var job = jobBuilder.Create(definition.RequestId, element);
			job.Linker = graphBuilder.AddNode(job);

			try
			{
				types[job.Id] = await job.Compile(types, cancellationToken);
			}
			catch (Exception ex)
			{
				throw new ArgumentException(
					$"{job.GetType().Name} {job.Id} incorrect:{Environment.NewLine}{ex.Message}", ex);
			}
		}

		var graph = graphBuilder.Build();
	}
}