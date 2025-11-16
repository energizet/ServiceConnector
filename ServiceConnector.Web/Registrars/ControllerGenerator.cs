using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using ServiceConnector.Common;
using ServiceConnector.TypeBuilder;
using ServiceConnector.TypeBuilder.Interfaces;
using ServiceConnector.Web.Controllers;

namespace ServiceConnector.Web.Registrars;

public sealed partial class ControllerGenerator : IIControllerGenerator
{
	private readonly IClassBuilder _builder;

	public ControllerGenerator(LoadContextStore loadContext, string fileName)
	{
		var assemblyBuilder = new AssemblyBuilderFactory(loadContext, fileName).Create(fileName);
		assemblyBuilder.AddUsing(typeof(RouteAttribute).Namespace ?? "");
		assemblyBuilder.AddUsing(typeof(ApiControllerAttribute).Namespace ?? "");

		_builder = assemblyBuilder.CreateClass<BaseHttpController>(fileName + "Controller");
		_builder
			.AddAttribute("Route(\"api\")")
			.AddAttribute("ApiController");
	}

	public void AddMethod(string requestId, Type requestType, Type resultType)
	{
		_builder.AssemblyBuilder
			.AddUsing("System.Threading")
			.AddUsing("System.Threading.Tasks")
			.AddUsing("Microsoft.AspNetCore.Mvc");

		var requestTypeStr = requestType.ToDisplayString();
		var resultTypeStr = resultType.ToDisplayString();
		_builder.CreateMethod(
			NameRegex().Replace(requestId, "_"),
			typeof(Task<>).MakeGenericType(resultType),
			$"[FromBody] {requestTypeStr} request, CancellationToken token",
			$"return Call<{requestTypeStr}, {resultTypeStr}>(\"{requestId}\", request, HttpContext, token);",
			"public",
			$"HttpPost(\"{requestId}\")"
		);
	}

	public Assembly Generate()
	{
		_builder.AssemblyBuilder.Build();
		return _builder.AssemblyBuilder.BuiltAssembly!;
	}

	[GeneratedRegex("\\W")]
	private static partial Regex NameRegex();
}