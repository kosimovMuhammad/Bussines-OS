using System.Collections.Concurrent;
using System.Text;

namespace BusinessOS.WebApi.Logging;

// Development-only: mirrors every log line to logs/dev-yyyyMMdd.log (gitignored) so the log can be
// read from disk while the API runs. Opens with FileShare.ReadWrite so tailing never blocks writes.
// Not registered outside Development (see AddDevFileLogging).
public sealed class DevFileLoggerProvider : ILoggerProvider
{
    private const int KeepDays = 7;
    private readonly string _directory;
    private readonly object _gate = new();

    public DevFileLoggerProvider(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
        PruneOldFiles();
    }

    public string CurrentPath => Path.Combine(_directory, $"dev-{DateTime.Now:yyyyMMdd}.log");

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose() { }

    private void Write(string line)
    {
        try
        {
            lock (_gate)
            {
                using var stream = new FileStream(CurrentPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                var bytes = Encoding.UTF8.GetBytes(line + Environment.NewLine);
                stream.Write(bytes, 0, bytes.Length);
            }
        }
        catch (IOException)
        {
            // Diagnostics must never break the request that produced the log line.
        }
    }

    private void PruneOldFiles()
    {
        try
        {
            foreach (var file in Directory.GetFiles(_directory, "dev-*.log"))
            {
                if (File.GetLastWriteTime(file) < DateTime.Now.AddDays(-KeepDays)) File.Delete(file);
            }
        }
        catch (IOException) { }
    }

    private sealed class FileLogger(DevFileLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var sb = new StringBuilder()
                .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                .Append(" [").Append(Short(logLevel)).Append("] ")
                .Append(category).Append(": ")
                .Append(formatter(state, exception));
            if (exception is not null) sb.AppendLine().Append(exception);
            owner.Write(sb.ToString());
        }

        private static string Short(LogLevel l) => l switch
        {
            LogLevel.Trace => "TRC", LogLevel.Debug => "DBG", LogLevel.Information => "INF",
            LogLevel.Warning => "WRN", LogLevel.Error => "ERR", LogLevel.Critical => "CRT", _ => "???",
        };
    }
}

public static class DevFileLoggingExtensions
{
    // Returns the log path when enabled (Development only), otherwise null.
    public static string? AddDevFileLogging(this WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsDevelopment()) return null;
        var provider = new DevFileLoggerProvider(Path.Combine(builder.Environment.ContentRootPath, "logs"));
        builder.Logging.AddProvider(provider);
        return provider.CurrentPath;
    }
}
