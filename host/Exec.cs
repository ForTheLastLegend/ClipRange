using System.Diagnostics;

namespace ClipRange.Host;

sealed record ExecResult(int ExitCode, string StderrTail);

static class Exec
{
    public static async Task<ExecResult> Run(string file, IEnumerable<string> args, Action<string> onStdoutLine)
    {
        var info = new ProcessStartInfo(file)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args)
            info.ArgumentList.Add(arg);

        Log.Write($"> {file} {string.Join(' ', info.ArgumentList)}");

        using var process = Process.Start(info)!;
        var stderr = process.StandardError.ReadToEndAsync();

        while (await process.StandardOutput.ReadLineAsync() is { } line)
            onStdoutLine(line);

        await process.WaitForExitAsync();

        var errorText = await stderr;
        if (errorText.Length > 0)
            Log.Write(errorText.TrimEnd());

        var lastLine = errorText.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim() ?? "";
        return new ExecResult(process.ExitCode, lastLine);
    }

    public static async Task<string?> FirstOutputLine(string file, params string[] args)
    {
        string? first = null;
        await Run(file, args, line => first ??= line);
        return first;
    }
}
