using Grpc.Core;
using ServiceConnector.TestWeb.Protos;

namespace ServiceConnector.TestWeb.Services;

public class GreeterService : Protos.GreeterService.GreeterServiceBase
{
	private readonly ILogger<GreeterService> _logger;
	public GreeterService(ILogger<GreeterService> logger)
	{
		_logger = logger;
	}

	public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
	{
		_logger.LogInformation("Saying hello to {Name}", request.Name);
		return Task.FromResult(new HelloReply
		{
			Message = "Hello " + request.Name
		});
	}
}
