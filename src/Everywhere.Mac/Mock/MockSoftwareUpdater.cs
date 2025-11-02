using System.ComponentModel;
using Everywhere.Common;

namespace Everywhere.Mac.Mock;

public class MockSoftwareUpdater : ISoftwareUpdater
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public Version CurrentVersion => new Version(1, 0, 0);
    public DateTimeOffset? LastCheckTime { get; set; }
    public Version? LatestVersion { get; set; }

    public void RunAutomaticCheckInBackground(TimeSpan interval, CancellationToken cancellationToken = default) { }

    public Task CheckForUpdatesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PerformUpdateAsync(IProgress<double> progress, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task PerformUpdateAsync(IProgress<double> progress) => Task.CompletedTask;
}