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
public class ObjectJob(ObjectJobConfig config, ILogger<ObjectJob> logger) : BaseJob<ObjectJobConfig>(config)
{
	private Type _resultType = typeof(object);
	public Func<PipelineStore, object?> GetData = null!;

	public override Task<Type> Compile(TypesStore types, CancellationToken cancellationToken)
	{
		_resultType = TypeBuilder.BuildType(types, Config.Data, $"{Definition.RequestId}{Id}Type");
		GetData = BuildGetData(types).Compile();
		return Task.FromResult(_resultType);
	}

	public override Task<object?> Run(PipelineStore store, CancellationToken cancellationToken)
	{
		return Task.FromResult(GetData(store));
	}

	private Expression<Func<PipelineStore, object?>> BuildGetData(TypesStore types)
	{
		var store = Expression.Parameter(typeof(PipelineStore), "store");

		var block = TypeBuilder.BuildObject(store, Config.Data, _resultType, types);
		block = Expression.Convert(block, typeof(object));

		return Expression.Lambda<Func<PipelineStore, object?>>(block, store)
			.Log($"{Definition.RequestId} {Id} {nameof(GetData)}", logger);
	}
}