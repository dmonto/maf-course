using System.Text.RegularExpressions;

namespace Maf.Unit15.Infrastructure;

public static class EnvironmentFile
{
    public static string Root()
    {
        foreach (string start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "examples.json")) &&
                    File.Exists(Path.Combine(directory.FullName, "Directory.Build.props"))) return directory.FullName;
        throw new DirectoryNotFoundException("Keep the complete maf-unit15 directory together.");
    }
    public static string Load()
    {
        string root = Root(), path = Path.Combine(root, ".env");
        if (!File.Exists(path)) return root;
        int number = 0;
        foreach (string source in File.ReadLines(path))
        {
            number++;
            string line = source.Trim().TrimStart('\uFEFF');
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (line.StartsWith("export ", StringComparison.Ordinal)) line = line[7..];
            int separator = line.IndexOf('=');
            if (separator <= 0) throw new InvalidDataException($"Invalid .env line {number}.");
            string key = line[..separator].Trim(), value = line[(separator+1)..].Trim();
            if (!Regex.IsMatch(key, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new InvalidDataException("Invalid environment key.");
            if (value.Length >= 2 && value[0] == value[^1] && value[0] is '\'' or '"') value = value[1..^1];
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key))) Environment.SetEnvironmentVariable(key, value);
        }
        return root;
    }
}
