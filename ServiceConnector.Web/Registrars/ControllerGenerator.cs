using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using ProtoBuf.Grpc.Configuration;
using ServiceConnector.Common;
using ServiceConnector.Common.Interfaces;
using ServiceConnector.TypeBuilder;
using ServiceConnector.Common.Extensions;
using ServiceConnector.Web.Controllers;

namespace ServiceConnector.Web.Registrars;

public sealed partial class ControllerGenerator : IIControllerGenerator
{
	private readonly IInterfaceBuilder _interfaceBuilder;
	private readonly IClassBuilder _httpBuilder;
	private readonly IClassBuilder _grpcBuilder;
	public Type? HttpControllerType => _httpBuilder.BuiltType;
	public Type? GrpcControllerType => _grpcBuilder.BuiltType;

	public ControllerGenerator(LoadContextStore loadContext, string fileName)
	{
		var assemblyBuilder = new AssemblyBuilderFactory(loadContext, fileName).Create(fileName);
		assemblyBuilder.AddUsing(typeof(RouteAttribute).Namespace!);
		assemblyBuilder.AddUsing(typeof(ApiControllerAttribute).Namespace!);
		assemblyBuilder.AddUsing(typeof(ServiceAttribute).Namespace!);

		_httpBuilder = assemblyBuilder.CreateClass<BaseHttpController>(fileName + "HttpController");
		_httpBuilder
			.AddAttribute("Route(\"api\")")
			.AddAttribute("ApiController");

		_interfaceBuilder = assemblyBuilder.CreateInterface($"I{fileName}GrpcService");
		_interfaceBuilder
			.AddAttribute("Service");

		_grpcBuilder = assemblyBuilder.CreateClass(typeof(BaseGrpcController), [_interfaceBuilder.Name],
			fileName + "GrpcController");
	}

	public void AddMethod(string requestId, Type requestType, Type resultType)
	{
		_httpBuilder.AssemblyBuilder
			.AddUsing("System.Threading")
			.AddUsing("System.Threading.Tasks")
			.AddUsing("Microsoft.AspNetCore.Mvc")
			.AddUsing("Grpc.Core");

		var requestTypeStr = requestType.ToDisplayString();
		var resultTypeStr = resultType.ToDisplayString();

		var methodName = NameRegex().Replace(requestId, "_");
		var returnType = typeof(Task<>).MakeGenericType(resultType);

		_httpBuilder.CreateMethod(
			methodName,
			returnType,
			$"[FromBody] {requestTypeStr} request, CancellationToken token",
			$"return Call<{requestTypeStr}, {resultTypeStr}>(\"{requestId}\", request, HttpContext, token);",
			"public",
			$"HttpPost(\"{requestId}\")"
		);

		_interfaceBuilder.CreateMethod(
			methodName,
			returnType,
			[$"{requestTypeStr} request", "ServerCallContext context"]
		);

		_grpcBuilder.CreateMethod(
			methodName,
			returnType,
			$"{requestTypeStr} request, ServerCallContext context",
			$"return Call<{requestTypeStr}, {resultTypeStr}>(\"{requestId}\", request, context);"
		);
	}

	public Assembly Generate()
	{
		return _httpBuilder.AssemblyBuilder.Build().BuiltAssembly!;
	}

	[GeneratedRegex("\\W")]
	private static partial Regex NameRegex();
}