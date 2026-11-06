using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;

internal static class Output
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = false };
    public static void Result(object value) => Console.WriteLine("RESULT " + JsonSerializer.Serialize(value, Json));
    public static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

internal sealed class Options(string[] args)
{
    public bool Has(string name) => args.Contains(name, StringComparer.Ordinal);
    public string Get(string name, string fallback)
    {
        int index = Array.IndexOf(args, name);
        if (index < 0) return fallback;
        if (index + 1 == args.Length || args[index + 1].StartsWith("--")) throw new ArgumentException($"Missing value for {name}.");
        return args[index + 1];
    }
}

internal static class Files
{
    public static string FindRoot()
    {
        foreach (string start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        for (DirectoryInfo? p = new(start); p is not null; p = p.Parent)
            if (File.Exists(Path.Combine(p.FullName, "manifest.json")) && Directory.Exists(Path.Combine(p.FullName, "Knowledge")))
                return p.FullName;
        throw new DirectoryNotFoundException("Cannot locate the maf-unit09 root.");
    }
    public static void LoadEnv(string file)
    {
        if (!File.Exists(file)) return;
        foreach (string original in File.ReadAllLines(file))
        {
            string line = original.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            int split = line.IndexOf('=');
            if (split < 1) throw new FormatException("Invalid .env assignment.");
            string key = line[..split].Trim(), value = line[(split + 1)..].Trim();
            if (!Regex.IsMatch(key, "^[A-Z][A-Z0-9_]*$")) throw new FormatException("Invalid .env variable name.");
            if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\''))) value = value[1..^1];
            // Explicit shell settings take precedence. Empty template values never erase them.
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)) && value.Length > 0)
                Environment.SetEnvironmentVariable(key, value);
        }
    }
    public static async Task<KnowledgeDocument[]> LoadKnowledgeAsync(string directory, CancellationToken ct)
    {
        string manifest = await File.ReadAllTextAsync(Path.Combine(directory, "knowledge.json"), ct);
        var documents = JsonSerializer.Deserialize<KnowledgeDocument[]>(manifest, Output.Json)
            ?? throw new InvalidOperationException("Knowledge manifest is empty.");
        for (int i = 0; i < documents.Length; i++)
        {
            var doc = documents[i];
            if (!Regex.IsMatch(doc.SourceFile ?? "", "^[a-z0-9-]+\\.(md|txt)$")) throw new ArgumentException("Invalid local source filename.");
            string content = await File.ReadAllTextAsync(Path.Combine(directory, doc.SourceFile!), ct);
            var chunks = Regex.Split(content.Trim(), @"\r?\n\s*\r?\n").Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray();
            if (chunks.Length is 0 or > 20 || chunks.Any(x => x.Length > 1500)) throw new ArgumentException("Invalid paragraph size or count.");
            documents[i] = doc with { Chunks = chunks };
        }
        return documents;
    }
    public static string ChunkKey(KnowledgeDocument doc, int ordinal) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { doc.TenantId, doc.DocumentId, doc.CatalogVersion, ordinal })))).ToLowerInvariant();
}
