using System.Collections.Specialized;
using System.Diagnostics;
using Avalonia.Data.Converters;
using Avalonia.Threading;
using ObservableCollections;

namespace Everywhere.ValueConverters;

public static class ObservableListConverters
{
    public static IValueConverter ObservableOnDispatcher { get; } = new FuncValueConverter<dynamic?, dynamic?>(list =>
    {
        if (list is null) return null;

        var dispatcher = SynchronizationContextCollectionEventDispatcher.Current;
        var result = list.ToNotifyCollectionChangedSlim(dispatcher); // result is INotifyCollectionChanged
        ((INotifyCollectionChanged)result).CollectionChanged += (s, e) =>
        {
            // Let's check that the event is raised on the correct context
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Debugger.Break();
            }

            if (e.NewItems is { Count: > 1 } || e.OldItems is { Count: > 1 })
            {
                // This should never happen, as NotifyCollectionChangedSlim raises events one item at a time
                Debugger.Break();
            }
        };
        return result;
    });
}