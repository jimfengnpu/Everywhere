using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using Everywhere.Common;

namespace Everywhere.Collections;

public class ObservableDictionary<TKey, TValue> :
    IDictionary<TKey, TValue>,
    ICollection<ObservableKeyValuePair<TKey, TValue>>
    where TKey : notnull
{
    public Lock SyncRoot { get; } = new();

    private readonly Dictionary<TKey, TValue> _dictionary;

    public ObservableDictionary()
    {
        _dictionary = new Dictionary<TKey, TValue>();
    }

    public ObservableDictionary(IEqualityComparer<TKey>? comparer)
    {
        _dictionary = new Dictionary<TKey, TValue>(comparer: comparer);
    }

    public ObservableDictionary(int capacity, IEqualityComparer<TKey>? comparer)
    {
        _dictionary = new Dictionary<TKey, TValue>(capacity, comparer: comparer);
    }

    public ObservableDictionary(IEnumerable<KeyValuePair<TKey, TValue>> collection, IEqualityComparer<TKey>? comparer = null)
    {
        _dictionary = new Dictionary<TKey, TValue>(collection: collection, comparer: comparer);
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public TValue this[TKey key]
    {
        get
        {
            lock (SyncRoot)
            {
                return _dictionary[key];
            }
        }
        set
        {
            lock (SyncRoot)
            {
                if (_dictionary.TryGetValue(key, out var oldValue))
                {
                    _dictionary[key] = value;
                    CollectionChanged?.Invoke(
                        this,
                        new NotifyCollectionChangedEventArgs(
                            NotifyCollectionChangedAction.Replace,
                            new KeyValuePair<TKey, TValue>(key, value),
                            new KeyValuePair<TKey, TValue>(key, oldValue)));
                }
                else
                {
                    Add(key, value);
                }
            }
        }
    }

    // for lock synchronization, hide keys and values.
    ICollection<TKey> IDictionary<TKey, TValue>.Keys
    {
        get
        {
            lock (SyncRoot)
            {
                return _dictionary.Keys;
            }
        }
    }

    ICollection<TValue> IDictionary<TKey, TValue>.Values
    {
        get
        {
            lock (SyncRoot)
            {
                return _dictionary.Values;
            }
        }
    }

    public bool Remove(ObservableKeyValuePair<TKey, TValue> item)
    {
        lock (SyncRoot)
        {
            if (!_dictionary.TryGetValue(item.Key, out var value) ||
                !EqualityComparer<TValue>.Default.Equals(value, item.Value) ||
                !_dictionary.Remove(item.Key, out var value2)) return false;

            CollectionChanged?.Invoke(
                this,
                new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Remove,
                    new KeyValuePair<TKey, TValue>(item.Key, value2)));
            return true;
        }
    }

    public int Count
    {
        get
        {
            lock (SyncRoot)
            {
                return _dictionary.Count;
            }
        }
    }

    public bool IsReadOnly => false;

    public void Add(TKey key, TValue value)
    {
        lock (SyncRoot)
        {
            _dictionary.Add(key, value);
            CollectionChanged?.Invoke(
                this,
                new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Add,
                    new KeyValuePair<TKey, TValue>(key, value)));
        }
    }

    public void Add(KeyValuePair<TKey, TValue> item)
    {
        Add(item.Key, item.Value);
    }

    public void Add(ObservableKeyValuePair<TKey, TValue> item)
    {
        lock (SyncRoot)
        {
            _dictionary[item.Key] = item.Value; // use indexer to allow update
            CollectionChanged?.Invoke(
                this,
                new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Add,
                    new KeyValuePair<TKey, TValue>(item.Key, item.Value)));
        }
    }

    public void Clear()
    {
        lock (SyncRoot)
        {
            _dictionary.Clear();
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    public bool Contains(ObservableKeyValuePair<TKey, TValue> item)
    {
        lock (SyncRoot)
        {
            return _dictionary.TryGetValue(item.Key, out var value) && EqualityComparer<TValue>.Default.Equals(value, item.Value);
        }
    }

    public void CopyTo(ObservableKeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        lock (SyncRoot)
        {
            var i = arrayIndex;
            foreach (var kvp in _dictionary)
            {
                array[i++] = new ObservableKeyValuePair<TKey, TValue>(kvp.Key, kvp.Value);
            }
        }
    }

    public bool Contains(KeyValuePair<TKey, TValue> item)
    {
        lock (SyncRoot)
        {
            return ((ICollection<KeyValuePair<TKey, TValue>>)_dictionary).Contains(item);
        }
    }

    public bool ContainsKey(TKey key)
    {
        lock (SyncRoot)
        {
            return ((IDictionary<TKey, TValue>)_dictionary).ContainsKey(key);
        }
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        lock (SyncRoot)
        {
            ((ICollection<KeyValuePair<TKey, TValue>>)_dictionary).CopyTo(array, arrayIndex);
        }
    }

    public bool Remove(TKey key)
    {
        lock (SyncRoot)
        {
            if (_dictionary.Remove(key, out var value))
            {
                CollectionChanged?.Invoke(
                    this,
                    new NotifyCollectionChangedEventArgs(
                        NotifyCollectionChangedAction.Remove,
                        new KeyValuePair<TKey, TValue>(key, value)));
                return true;
            }
            return false;
        }
    }

    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        lock (SyncRoot)
        {
            if (_dictionary.TryGetValue(item.Key, out var value) &&
                EqualityComparer<TValue>.Default.Equals(value, item.Value) &&
                _dictionary.Remove(item.Key, out var value2))
            {
                CollectionChanged?.Invoke(
                    this,
                    new NotifyCollectionChangedEventArgs(
                        NotifyCollectionChangedAction.Remove,
                        new KeyValuePair<TKey, TValue>(item.Key, value2)));
                return true;
            }
            return false;
        }
    }

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        lock (SyncRoot)
        {
            return _dictionary.TryGetValue(key, out value);
        }
    }

    IEnumerator<ObservableKeyValuePair<TKey, TValue>> IEnumerable<ObservableKeyValuePair<TKey, TValue>>.GetEnumerator()
    {
        List<KeyValuePair<TKey, TValue>> snapshot;

        lock (SyncRoot)
        {
            snapshot = _dictionary.ToList();
        }

        foreach (var kvp in snapshot)
        {
            yield return new ObservableKeyValuePair<TKey, TValue>(kvp.Key, kvp.Value);
        }
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        lock (SyncRoot)
        {
            return _dictionary.ToList().GetEnumerator();
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public IEqualityComparer<TKey> Comparer
    {
        get
        {
            lock (SyncRoot)
            {
                return _dictionary.Comparer;
            }
        }
    }
}