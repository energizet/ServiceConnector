using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ServiceConnector.Common;

namespace ServiceConnector.Jobs.Jobs;

public class ObjectJobConfig : BaseJobConfig
{
	public required JsonElement Data { get; init; }
}

[PipelineJob]
public class ObjectJob(
	ObjectJobConfig config,
	TypeBuilder typeBuilder,
	ExpressionGeneratorFactory generator,
	PipelineDefinition definition,
	ILogger<ObjectJob> logger
) : BaseJob<ObjectJobConfig, ObjectJobRunner>(config, isAsync: false)
{
	private Type _resultType = typeof(object);
	public Func<PipelineStore, object?> GetData = null!;

	public override Task<Type> Compile(TypesStore types, CancellationToken cancellationToken)
	{
		GetData = BuildGetData(types, out _resultType).Compile();
		return Task.FromResult(_resultType);
	}

	private Expression<Func<PipelineStore, object?>> BuildGetData(TypesStore types, out Type resultType)
	{
		var builder = generator.Create(Linker);
		var store = builder.CreateParameter(typeof(PipelineStore), "store");

		resultType = typeBuilder.BuildType(types, Config.Data, $"{definition.RequestId}{Id}Type");
		var block = typeBuilder.BuildObject(types, Config.Data, resultType, store, Linker);
		block = Expression.Convert(block, typeof(object));

		return builder.CreateLambda<Func<PipelineStore, object?>>(block)
			.Log($"{definition.RequestId}.{Id} {nameof(GetData)}", logger);
	}
}

public class ObjectJobRunner(ObjectJob job, PipelineStore store) : IRunner
{
	public Task<object?> Run(CancellationToken cancellationToken)
	{
		return Task.FromResult(job.GetData(store));
	}
}