using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Primitives;

namespace ServiceConnector.Web.Registrars;

public class DynamicActionDescriptorChangeProvider : IActionDescriptorChangeProvider
{
	public static DynamicActionDescriptorChangeProvider Instance { get; } = new();

	private CancellationTokenSource _tokenSource = new();

	public IChangeToken GetChangeToken()
	{
		return new CancellationChangeToken(_tokenSource.Token);
	}

	public void NotifyChanges()
	{
		var current = Interlocked.Exchange(ref _tokenSource, new());
		current.Cancel();
	}
}