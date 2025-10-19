namespace ServiceConnector.Common;

public abstract class Store<T>(Store<T>? parentStore = null)
{
	private readonly Dictionary<string, T> _store = [];

	public void Set(string key, T value)
	{
		_store[key.ToLower()] = value;
	}

	public T Get(string key)
	{
		if (!TryGetValue(key, out var value))
		{
			throw new($"{key} not found in store");
		}

		return value!;
	}

	public bool TryGetValue(string key, out T? value)
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

	public T this[string key]
	{
		set => Set(key, value);
		get => Get(key);
	}
}