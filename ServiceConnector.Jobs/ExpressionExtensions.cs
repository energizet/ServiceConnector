using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace ServiceConnector.Jobs;

public static class ExpressionExtensions
{
	public static TExpression Log<TExpression>(this TExpression expression, string name, ILogger logger)
		where TExpression : Expression
	{
		var debugView = typeof(Expression).GetProperty("DebugView", BindingFlags.NonPublic | BindingFlags.Instance);
		if (debugView == null)
		{
			return expression;
		}

		var value = debugView.GetValue(expression);
		logger.LogTrace("{Name}{NL}{Value}", name, Environment.NewLine, value);

		return expression;
	}
}