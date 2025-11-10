using Microsoft.Extensions.Options;
using ServiceConnector.Common;
using ServiceConnector.TypeBuilder;
using ServiceConnector.Web.Configs;

namespace ServiceConnector.Web.Registrars;

public class ServiceConnectorRegistrar(
	ILogger<ServiceConnectorRegistrar> logger,
	RequestPipelineLoader loader,
	JobBuilder jobBuilder,
	RunnersStore runnersStore,
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
		var successCount = 0;
		var errorCount = 0;

		foreach (var definition in definitions)
		{
			var requestId = definition.RequestId;
			if (runnersStore.TryGetValue(requestId, out _))
			{
				logger.LogError("[{RequestId}] Request already exist", requestId);
				continue;
			}

			try
			{
				var runner = await Compile(definition, cancellationToken);
				runnersStore[requestId] = (runner, definition);
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

	private async Task<IRunner> Compile(PipelineDefinition definition, CancellationToken cancellationToken)
	{
		var types = new TypesStore
		{
			["headers"] = typeof(IDictionary<string, string>),
			["request"] = definition.RequestType,
		};

		var factory = new AssemblyBuilderFactory(definition.LoadContext, definition.RequestId);
		var graphBuilder = new JobGraph.Builder();
		foreach (var element in definition.Pipeline)
		{
			var job = jobBuilder.Create(definition.RequestId, element);
			var linker = graphBuilder.AddNode(job);
			job.Definition = definition;
			job.TypeBuilder = new(factory, new(linker), new());

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

		return graphBuilder.Build();
	}
}