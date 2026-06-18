using System.Linq.Expressions;
using ServiceConnector.Common;
using ServiceConnector.Common.Extensions;

namespace ServiceConnector.Jobs.Expressions;

internal static class TypeExpression
{
	public static Expression<Func<IJob, PipelineStore, object[]>> CreateFactory(this Type type)
	{
		var types = type
			.GetConstructors()
			.Single()
			.GetParameters()
			.Select(x => x.ParameterType)
			.ToHashSet();

		var jobParam = Expression.Parameter(typeof(IJob), "job");
		var storeParam = Expression.Parameter(typeof(PipelineStore), "store");

		var expressions = new List<Expression>();

		if (types.Any(x => x.TryTo(typeof(IJob), out _)))
		{
			expressions.Add(Expression.Convert(jobParam, typeof(object)));
		}

		if (types.Any(x => x.TryTo(typeof(PipelineStore), out _)))
		{
			expressions.Add(Expression.Convert(storeParam, typeof(object)));
		}

		var newArray = Expression.NewArrayInit(typeof(object), expressions);

		return Expression.Lambda<Func<IJob, PipelineStore, object[]>>(newArray, jobParam, storeParam);
	}
}