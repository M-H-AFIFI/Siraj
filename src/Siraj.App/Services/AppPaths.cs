namespace Siraj.App.Services;

internal static class AppPaths
{
    public static readonly string Data = Environment.GetEnvironmentVariable("SIRAJ_DATA_DIR") is { Length: > 0 } dir ? dir
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Siraj");

    public static bool IsSandboxed => Environment.GetEnvironmentVariable("SIRAJ_DATA_DIR") is { Length: > 0 };

    public static readonly string Logs = Path.Combine(Data, "logs");

    public static bool Muted => Environment.GetEnvironmentVariable("SIRAJ_MUTE") == "1";

    public static readonly string Install = AppContext.BaseDirectory;

    public static string Asset(params string[] parts) => Path.Combine([Install, "Assets", .. parts]);

    public static string Exe => Environment.ProcessPath ?? Path.Combine(Install, "Siraj.exe");
}
