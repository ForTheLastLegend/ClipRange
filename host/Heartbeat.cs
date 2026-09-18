namespace ClipRange.Host;

// Chrome unloads the extension service worker after 30s without port traffic,
// which would kill this process mid-job. Repeat the last progress report often enough to prevent that.
sealed class Heartbeat : IDisposable
{
    static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    readonly NativePort port;
    readonly CancellationTokenSource stop = new();
    ProgressReply last;

    public Heartbeat(NativePort port, string initialStage)
    {
        this.port = port;
        last = new ProgressReply(initialStage, null);
        _ = Loop();
    }

    public void Report(string stage, int? percent)
    {
        last = new ProgressReply(stage, percent);
        port.Write(last);
    }

    async Task Loop()
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stop.Token).ConfigureAwait(false))
            port.Write(last);
    }

    public void Dispose() => stop.Cancel();
}
