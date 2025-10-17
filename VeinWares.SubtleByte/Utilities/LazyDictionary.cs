using System;
using System.Collections;
using System.Collections.Generic;

namespace VeinWares.SubtleByte.Utilities;

internal sealed class LazyDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>> where TKey : notnull
{
    private readonly Func<TValue> _factory;
    private readonly Dictionary<TKey, TValue> _inner;

    public LazyDictionary(Func<TValue> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _inner = new Dictionary<TKey, TValue>();
    }

    public TValue this[TKey key]
    {
        get
        {
            if (!_inner.TryGetValue(key, out var value))
            {
                value = _factory();
                _inner[key] = value;
            }

            return value;
        }
        set => _inner[key] = value;
    }

    public int Count => _inner.Count;

    public bool Remove(TKey key) => _inner.Remove(key);

    public bool TryGetValue(TKey key, out TValue value) => _inner.TryGetValue(key, out value);

    public void Clear() => _inner.Clear();

    public IReadOnlyDictionary<TKey, TValue> AsReadOnly() => _inner;

    public Dictionary<TKey, TValue>.Enumerator GetEnumerator() => _inner.GetEnumerator();

    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => _inner.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _inner.GetEnumerator();
}
