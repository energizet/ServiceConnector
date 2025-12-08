using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using ServiceConnector.Common;
using ServiceConnector.Common.Extensions;
using ServiceConnector.Jobs;
using ServiceConnector.TypeBuilder;

namespace ServiceConnector.Web.Registrars;

public class JobBuilder(IServiceProvider provider)
{
	private static readonly JsonSerializerOptions Options = new()
	{
		Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
		PropertyNameCaseInsensitive = true,
	};

	private static readonly Dictionary<string, Type> Types = typeof(IJob).Assembly.GetTypes()
		.Where(x => x.GetCustomAttributes<PipelineJobAttribute>(inherit: false).Any())
		.ToDictionary(x => x.Name.Replace("Job", ""), x => x);

	public IJob Create(string requestId, JsonElement element, params object[] parameters)
	{
		var properties = new Dictionary<string, JsonProperty>(
			element.EnumerateObject().ToDictionary(item => item.Name),
			StringComparer.OrdinalIgnoreCase
		);

		var typeName = GetString(properties, "Type");
		var id = GetString(properties, "Id");

		if (!Types.TryGetValue(typeName, out var type))
		{
			throw new ArgumentException($"Unknown job type {typeName}");
		}

		var configType = type.To(typeof(BaseJob<,>))!.GenericTypeArguments.First();

		CheckType($"{requestId}.{id}", element, configType);
		var config = DeserializeConfig(configType, element);

		return (IJob)CreateInstance(provider, type, [config, ..parameters]);
	}

	private static string GetString(Dictionary<string, JsonProperty> properties, string key)
	{
		if (!properties.TryGetValue(key, out var element))
		{
			throw new NullReferenceException($"{key} is null");
		}

		var str = element.Value.GetString();

		if (str == null)
		{
			throw new NullReferenceException($"{key} is null");
		}

		return str;
	}

	private static object DeserializeConfig(Type configType, JsonElement element)
	{
		try
		{
			return element.Deserialize(configType, Options)!;
		}
		catch (JsonException ex)
		{
			const int startTypeIndex = 41;
			var endTypeIndex = ex.Message.IndexOf(' ', startTypeIndex) - 1;
			var requiredType = ex.Message[startTypeIndex..endTypeIndex];
			throw new JsonException($"Path {ex.Path} has wrong type. Required type: {requiredType}");
		}
	}

	private static void CheckType(string name, JsonElement element, Type type)
	{
		if (type.To(typeof(JsonElement)) != null)
		{
			return;
		}

		if (element.ValueKind == JsonValueKind.Array)
		{
			var enumerable = type.To(typeof(IEnumerable<>)) ??
				throw new ArgumentException($"{name} must be {type.Name} type");

			var itemType = enumerable.GenericTypeArguments.First();
			var list = element.EnumerateArray().ToList();
			for (var i = 0; i < list.Count; i++)
			{
				CheckType($"{name}[{i}]", list[i], itemType);
			}

			return;
		}

		if (element.ValueKind != JsonValueKind.Object)
		{
			return;
		}

		var properties = new Dictionary<string, JsonProperty>(
			element.EnumerateObject().ToDictionary(item => item.Name),
			StringComparer.OrdinalIgnoreCase
		);

		foreach (var fieldInfo in type.GetProperties())
		{
			if (!properties.TryGetValue(fieldInfo.Name, out var property))
			{
				if (fieldInfo.GetCustomAttribute<RequiredMemberAttribute>() != null)
				{
					throw new ArgumentException($"Required parameter {name}.{fieldInfo.Name} not found");
				}

				continue;
			}

			CheckType($"{name}.{property.Name}", property.Value, fieldInfo.PropertyType);
		}
	}


	private static LambdaExpression CreateFactory(Type type, params object[] arguments)
	{
		var types = type
			.GetConstructors()
			.Single()
			.GetParameters()
			.Select(x => x.ParameterType)
			.ToHashSet();

		var parameters = new List<ParameterExpression>();
		var expressions = new List<Expression>();

		for (var i = 0; i < arguments.Length; i++)
		{
			var argument = arguments[i].GetType();
			var param = Expression.Parameter(argument, $"argument_{i}");
			parameters.Add(param);
			if (types.Any(x => x.TryTo(argument, out _)))
			{
				expressions.Add(Expression.Convert(param, typeof(object)));
			}
		}

		var newArray = Expression.NewArrayInit(typeof(object), expressions);

		return Expression.Lambda(newArray, parameters);
	}

	private static object CreateInstance(IServiceProvider provider, Type type, params object[] arguments)
	{
		var factory = CreateFactory(type, arguments).Compile();

		return ActivatorUtilities.CreateInstance(provider, type, factory.DynamicInvoke(arguments) as object[] ?? []);
	}
}