using System.Collections;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProtoBuf;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Server;
using ServiceConnector.Common;
using ServiceConnector.Jobs;
using ServiceConnector.Web.Configs;

namespace ServiceConnector.Web.Registrars;

public static class RegistrarExtensions
{
	public static IServiceCollection AddServiceConnector(this IServiceCollection services)
	{
		services.AddOptions<ServiceConnectorConfig>(nameof(ServiceConnectorConfig));

		services.AddCors(x => x.AddDefaultPolicy(policy =>
		{
			policy.AllowAnyHeader();
			policy.AllowAnyMethod();
			policy.AllowAnyOrigin();
		}));
		var controllers = services.AddControllers();

		services.AddEndpointsApiExplorer();
		services.AddSwaggerGen();

		services.AddCodeFirstGrpc(opt => opt.EnableDetailedErrors = true);
		services.AddCodeFirstGrpcReflection();

		services.AddSingleton<JobBuilder>();
		services.AddSingleton<RequestPipelineLoader>();
		services.AddSingleton<RunnersStore>();
		services.AddSingleton<IRunnerFinder>(provider => provider.GetRequiredService<RunnersStore>());
		services.AddHostedService<ServiceConnectorRegistrar>();
		services.AddSingleton(controllers);
		services.AddSingleton<GrpcRegistrar>();
		services.AddSingleton<ExpressionGeneratorFactory>();
		services.AddSingleton<TypeFinder>();
		services.TryAddTransient<HttpClient>();

		services.AddSingleton<IActionDescriptorChangeProvider>(DynamicActionDescriptorChangeProvider.Instance);

		return services;
	}

	public static WebApplication UseServiceConnector(this WebApplication app)
	{
		app.UseCors();
		app.MapControllers();

		app.UseSwagger();
		app.UseSwaggerUI();

		app.UseHttpsRedirection();

		app.Services.GetRequiredService<GrpcRegistrar>().Init(app);

		app.MapGrpcService<TestClass>();
		app.MapCodeFirstGrpcReflectionService();

		return app;
	}
}

[Service]
public interface TestInterface
{
	public TestResponse Add(TestRequest request);
}

public class TestClass : TestInterface
{
	public TestResponse Add(TestRequest request)
	{
		return new();
	}
}

[ProtoContract]
public class TestRequest
{
	[ProtoMember(1)]
	public IEnumerable<object> Number { get; set; }
}

[ProtoContract]
public class TestResponse
{
}

public class TestArray : IArray
{
	public IEnumerator GetEnumerator()
	{
		throw new NotImplementedException();
	}

	public int Count()
	{
		throw new NotImplementedException();
	}

	public object? Get(int index)
	{
		throw new NotImplementedException();
	}

	public static bool IsOnlyStatic()
	{
		throw new NotImplementedException();
	}

	public static int StaticCount()
	{
		throw new NotImplementedException();
	}
}