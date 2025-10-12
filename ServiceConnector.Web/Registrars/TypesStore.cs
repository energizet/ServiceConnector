namespace ServiceConnector.Web.Registrars;

public class TypesStore(TypesStore? parentStore = null)
{
	private readonly Dictionary<string, Type> _store = [];

	public void Set(string key, Type value)
	{
		_store[key.ToLower()] = value;
	}

	public Type Get(string key)
	{
		if (!TryGetValue(key, out var value))
		{
			throw new($"{key} not found in types store");
		}

		return value!;
	}

	public bool TryGetValue(string key, out Type? value)
	{
		key = key.ToLower();
		if (_store.TryGetValue(key, out value))
		{
			return true;
		}

		if (parentStore?.TryGetValue(key, out value) == true)
		{
			return true;
		}

		return false;
	}

	public Type this[string key]
	{
		set => Set(key, value);
		get => Get(key);
	}
}