using System.Collections.Concurrent;

namespace Infrastructure.Scheduler;

public record JobContext(string JobId, string Name, JobSettings Settings)
{
    private readonly ConcurrentDictionary<Type, object> _items = new();

    public void Set<T>(T value)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value);

        _items[typeof(T)] = value;
    }

    public T? Get<T>()
        where T : class
    {
        return _items.TryGetValue(typeof(T), out var value) ? (T)value : null;
    }

    public T GetRequired<T>()
        where T : class
    {
        return Get<T>()
            ?? throw new InvalidOperationException($"Context value of type {typeof(T).Name} was not found.");
    }

    public bool TryGet<T>(out T? value)
        where T : class
    {
        if (_items.TryGetValue(typeof(T), out var existing))
        {
            value = (T)existing;
            return true;
        }

        value = null;
        return false;
    }

    public bool Remove<T>()
        where T : class
    {
        return _items.TryRemove(typeof(T), out _);
    }
}
