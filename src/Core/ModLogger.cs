using System;
using System.IO;
using System.Text;
using Godot;

namespace CatastropheContract.Core;

internal static class ModLogger
{
    private const string Prefix = "[CatastropheContract]";
    public const string BuildMarker = "2026-05-16-plating-bloodthirsty-v5";
    private static readonly string LogPath = Path.Combine(
        AppContext.BaseDirectory,
        "mods",
        "CatastropheContract",
        "debug.log");

    public static void Info(string message)
    {
        WriteFile("INFO", message);
        GD.Print($"{Prefix} {message}");
    }

    public static void Warn(string message)
    {
        WriteFile("WARN", message);
        GD.PushWarning($"{Prefix} {message}");
    }

    public static void Error(string message)
    {
        WriteFile("ERROR", message);
        GD.PushError($"{Prefix} {message}");
    }

    public static void Debug(string message)
    {
        WriteFile("DEBUG", message);
        GD.Print($"{Prefix} {message}");
    }

    private static void WriteFile(string level, string message)
    {
        try
        {
            string? directory = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{System.Environment.NewLine}";
            File.AppendAllText(LogPath, line, Encoding.UTF8);
        }
        catch
        {
            // Keep logging side effects from breaking mod startup.
        }
    }
}
