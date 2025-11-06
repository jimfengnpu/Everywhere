namespace Everywhere.Utilities;

/// <summary>
/// A CancellationTokenSource that can be reused after being canceled.
/// Thread-safe.
/// </summary>
public sealed class ReusableCancellationTokenSource : IDisposable
{
    private readonly Lock _lockObject = new();
    private CancellationTokenSource? _cancellationTokenSource;

    public CancellationToken Token
    {
        get
        {
            using var _ = _lockObject.EnterScope();
            _cancellationTokenSource ??= new CancellationTokenSource();
            return _cancellationTokenSource.Token;
        }
    }

    public void Cancel()
    {
        using var _ = _lockObject.EnterScope();
        if (_cancellationTokenSource == null) return;
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = null;
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Dispose();
    }
}