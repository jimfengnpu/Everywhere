namespace Everywhere.Common;

/// <summary>
/// Represents a timer that can be started and stopped, and triggers a callback after a specified interval.
/// It should be stopped when initialized and started/stopped as needed.
/// </summary>
public interface ITimer : IDisposable
{
    event Action Callback;

    TimeSpan Interval { get; set; }

    void Start();

    void Stop();
}

/// <summary>
/// Wraps a System.Threading.Timer to implement the ITimer interface.
/// </summary>
public sealed class ThreadingTimerImpl : ITimer
{
    private readonly Timer _timer;

    public event Action? Callback;

    public TimeSpan Interval { get; set; }

    public ThreadingTimerImpl()
    {
        _timer = new Timer(TimerCallback, this, Timeout.Infinite, Timeout.Infinite);
    }

    private static void TimerCallback(object? state)
    {
        if (state is ThreadingTimerImpl wrapper)
        {
            wrapper.Callback?.Invoke();
        }
    }

    public void Start()
    {
        _timer.Change(Interval, Timeout.InfiniteTimeSpan);
    }

    public void Stop()
    {
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}