using System.Net;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using Azure.Core;

// In-memory HTTP simulator. Hash vectors and lexical overlap are teaching fixtures,
// not Azure ANN, BM25, semantic embeddings or a measurement of production relevance.
internal sealed class DemoSearchService : HttpMessageHandler
{
    private readonly Dictionary<string, Dictionary<string, JsonElement>> _indexes = new();
    public List<JsonElement> SearchRequests { get; } = [];
    public List<JsonElement> Schemas { get; } = [];
    public HttpStatusCode? FailNextRequest { get; set; }
    public bool FailNextIndexItem { get; set; }
    private static string[] Tokens(string text)
    {
        return Regex.Matches(text.ToLowerInvariant(), "[a-z0-9]+")
            .Select(m => m.Value switch { "tunnel" or "connection" or "network" => "vpn", "windows" => "win", "retrying" => "retry", _ => m.Value })
            .ToArray();
    }
    public static float[] Embed(string text, int dimensions)
    {
        float[] vector = new float[dimensions];
        foreach (string token in Tokens(text))
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            int index = (int)(System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(hash) % (uint)dimensions);
            vector[index]++;
        }
        double norm = Math.Sqrt(vector.Sum(x => (double)x * x));
        if (norm > 0) for (int i = 0; i < vector.Length; i++) vector[i] /= (float)norm;
        return vector;
    }
    private static double Dot(float[] a, float[] b) => a.Zip(b).Sum(x => (double)x.First * x.Second);
    private static string Literal(string filter, string field)
    {
        var match = Regex.Match(filter, @"\b" + field + @" eq '((?:[^']|'')*)'");
        if (!match.Success) throw new ArgumentException("Missing trusted filter field: " + field);
        return match.Groups[1].Value.Replace("''", "'");
    }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (FailNextRequest is { } fail) { FailNextRequest = null; return Reply(fail, new { error = "Injected dependency failure" }); }
        string path = request.RequestUri!.AbsolutePath;
        string index = path.Split('/', StringSplitOptions.RemoveEmptyEntries)[1];
        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
        var root = body.RootElement;
        if (request.Method == HttpMethod.Put)
        {
            Schemas.Add(root.Clone()); _indexes.TryAdd(index, new());
            return Reply(HttpStatusCode.OK, new { name = index });
        }
        if (!_indexes.TryGetValue(index, out var documents)) return Reply(HttpStatusCode.NotFound, new { error = "Index not seeded" });
        if (path.EndsWith("/docs/index"))
        {
            var outcomes = new List<object>();
            foreach (var doc in root.GetProperty("value").EnumerateArray())
            {
                string key = doc.GetProperty("id").GetString()!;
                bool success = !FailNextIndexItem; FailNextIndexItem = false;
                if (success) documents[key] = doc.Clone();
                outcomes.Add(new { key, status = success, statusCode = success ? 201 : 503 });
            }
            return Reply(HttpStatusCode.OK, new { value = outcomes });
        }
        if (!path.EndsWith("/docs/search")) return Reply(HttpStatusCode.BadRequest, new { error = "Unsupported route" });
        SearchRequests.Add(root.Clone());
        string filter = root.GetProperty("filter").GetString()!;
        if (filter == "false") return Reply(HttpStatusCode.OK, new { value = Array.Empty<object>() });
        string tenant = Literal(filter, "tenant_id"), version = Literal(filter, "catalog_version");
        string[] groups = Regex.Matches(filter, @"\bg eq '((?:[^']|'')*)'").Select(m => m.Groups[1].Value.Replace("''", "'")).ToArray();
        var permitted = documents.Values.Where(d => d.GetProperty("tenant_id").GetString() == tenant &&
            d.GetProperty("catalog_version").GetString() == version &&
            d.GetProperty("allowed_groups").EnumerateArray().Any(g => groups.Contains(g.GetString()))).ToArray();
        var expiry = Regex.Match(filter, @"valid_until gt ([^\s)]+)");
        if (expiry.Success)
        {
            var now = DateTimeOffset.Parse(expiry.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            permitted = permitted.Where(d => d.TryGetProperty("valid_until", out var value) && value.ValueKind == JsonValueKind.String &&
                DateTimeOffset.Parse(value.GetString()!) > now).ToArray();
        }
        bool lexical = root.TryGetProperty("search", out var search), vectorMode = root.TryGetProperty("vectorQueries", out var vectors);
        string[] terms = lexical ? Tokens(search.GetString()!) : [];
        var keywordRanks = permitted.Select(d => (Doc: d, Score: Tokens(d.GetProperty("title").GetString()! + " " + d.GetProperty("content").GetString()!).Intersect(terms).Count()))
            .Where(x => x.Score > 0).OrderByDescending(x => x.Score).ThenBy(x => x.Doc.GetProperty("id").GetString()).ToArray();
        float[] queryVector = vectorMode ? vectors[0].GetProperty("vector").EnumerateArray().Select(v => v.GetSingle()).ToArray() : [];
        var vectorRanks = permitted.Select(d => (Doc: d, Score: vectorMode ? Dot(queryVector, d.GetProperty("content_vector").EnumerateArray().Select(v => v.GetSingle()).ToArray()) : 0))
            .OrderByDescending(x => x.Score).ThenBy(x => x.Doc.GetProperty("id").GetString()).Take(10).ToArray();
        var scores = new Dictionary<string, double>();
        void Add(string id, double score) => scores[id] = scores.GetValueOrDefault(id) + score;
        if (lexical) for (int i = 0; i < keywordRanks.Length; i++) Add(keywordRanks[i].Doc.GetProperty("id").GetString()!, vectorMode ? 1.0 / (61 + i) : keywordRanks[i].Score);
        if (vectorMode) for (int i = 0; i < vectorRanks.Length; i++) Add(vectorRanks[i].Doc.GetProperty("id").GetString()!, lexical ? 1.0 / (61 + i) : vectorRanks[i].Score);
        string[] select = root.GetProperty("select").GetString()!.Split(',');
        var rows = scores.OrderByDescending(x => x.Value).ThenBy(x => x.Key).Take(root.GetProperty("top").GetInt32()).Select(x =>
        {
            var row = select.ToDictionary(k => k, k => (object)documents[x.Key].GetProperty(k).Clone());
            row["@search.score"] = x.Value; return row;
        }).ToArray();
        return Reply(HttpStatusCode.OK, new { value = rows });
    }
    private static HttpResponseMessage Reply(HttpStatusCode status, object body) => new(status)
    { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
}
internal sealed class DemoCredential : TokenCredential
{
    public override AccessToken GetToken(TokenRequestContext context, CancellationToken ct) => new("synthetic-token", DateTimeOffset.UtcNow.AddHours(1));
    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct) => ValueTask.FromResult(GetToken(context, ct));
}
