namespace ServiceConnector.Web.Registrars;

public class GrpcRegistrar
{
	private WebApplication _app = null!;

	public void Init(WebApplication app)
	{
		_app = app;
	}

	public void MapGrpcService(Type type)
	{
		var mapMethod = typeof(GrpcEndpointRouteBuilderExtensions).GetMethods()
			.First(x => x is { Name: "MapGrpcService", IsGenericMethod: true });
		var generic = mapMethod.MakeGenericMethod(type);
		generic.Invoke(null, [_app]);
	}
}