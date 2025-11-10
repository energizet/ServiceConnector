namespace ServiceConnector.Common;

public class PipelineStore(PipelineStore? parentStore = null) : Store<object?>(parentStore)
{
	public T? Get<T>(string key)
	{
		return (T?)base.Get(key);
	}
}