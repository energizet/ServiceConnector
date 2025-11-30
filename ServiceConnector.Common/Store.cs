using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ServiceConnector.Common;

public abstract class Store<T>(Store<T>? parentStore = null)
{
	private readonly ConcurrentDictionary<string, T> _store = new(StringComparer.OrdinalIgnoreCase);

	public void Set(string key, T value)
	{
		_store[key] = value;
	}

	public T Get(string key)
	{
		if (!TryGetValue(key, out var value))
		{
			throw new($"{key} not found in store");
		}

		return value!;
	}

	public bool TryGetValue(string key, [MaybeNullWhen(false)] out T value)
	{
		if (_store.TryGetValue(key, out value))
		{
			return true;
		}

		return parentStore?.TryGetValue(key, out value) == true;
	}

	public T this[string key]
	{
		set => Set(key, value);
		get => Get(key);
	}
}