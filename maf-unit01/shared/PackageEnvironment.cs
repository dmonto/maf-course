using System.Text.RegularExpressions;

namespace MafCourse.Unit01.Shared;

/// <summary>Loads the package's single root .env without replacing existing process settings.</summary>
public static class PackageEnvironment
{
    public static string Load()
    {
        string root = FindRoot();
        string path = Path.Combine(root, ".env");
        if (!File.Exists(path))
        {
            // Environment variables can also be supplied directly by the host.
            return root;
        }

        int lineNumber = 0;
        foreach (string rawLine in File.ReadLines(path))
        {
            lineNumber++;
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (line.StartsWith("export ", StringComparison.Ordinal)) line = line[7..].TrimStart();

            int separator = line.IndexOf('=');
            if (separator < 1) throw InvalidLine(lineNumber);
            string name = line[..separator].Trim();
            string value = line[(separator + 1)..].Trim();
            if (!Regex.IsMatch(name, "^[A-Za-z_][A-Za-z0-9_]*$")) throw InvalidLine(lineNumber);

            if (value.StartsWith('"') || value.StartsWith('\''))
            {
                if (value.Length < 2 || value[^1] != value[0]) throw InvalidLine(lineNumber);
                value = value[1..^1];
            }

            if (Environment.GetEnvironmentVariable(name) is null)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
        return root;
    }

    public static string RequiredSetting(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name)?.Trim();
        if (string.IsNullOrWhiteSpace(value) || value.Contains('<') || value.Contains('>'))
        {
            throw new InvalidOperationException($"Set {name} in the root .env or process environment.");
        }
        return value;
    }

    private static string FindRoot()
    {
        // The executable location is reliable even when dotnet run is called from another directory.
        foreach (string start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            for (DirectoryInfo? current = new(start); current is not null; current = current.Parent)
            {
                if (File.Exists(Path.Combine(current.FullName, "unit01-package.json")))
                    return current.FullName;
            }
        }
        throw new InvalidOperationException("Cannot locate unit01-package.json. Keep the package folders together.");
    }

    private static InvalidOperationException InvalidLine(int number) =>
        new($"Invalid root .env entry on line {number}. Use NAME=value and matching optional quotes.");
}
