using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Everywhere.Common;

public partial class ObservableKeyValuePair<TKey, TValue> : ObservableObject
{
    [ObservableProperty] public required partial TKey Key { get; set; }

    [ObservableProperty] public required partial TValue Value { get; set; }

    public ObservableKeyValuePair() { }

    [SetsRequiredMembers]
    public ObservableKeyValuePair(TKey key, TValue value)
    {
        Key = key;
        Value = value;
    }
}