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
		services.AddControllers();

		services.AddEndpointsApiExplorer();
		services.AddSwaggerGen();

		//services.AddGrpc(opt =>
		//{
		//	opt.EnableDetailedErrors = true;
		//});

		services.AddSingleton(JobBuilder.Create);
		services.AddSingleton<RequestPipelineLoader>();
		services.AddHostedService<ServiceConnectorRegistrar>();

		return services;
	}

	public static WebApplication UseServiceConnector(this WebApplication app)
	{
		app.UseCors();
		app.MapControllers();

		app.UseSwagger();
		app.UseSwaggerUI();

		app.UseHttpsRedirection();

		//app.MapGrpcService<ServiceConnectorGrpc>();
		//app.MapGrpcReflectionService();

		return app;
	}
}