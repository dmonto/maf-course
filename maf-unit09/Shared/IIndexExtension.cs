internal interface IIndexExtension
{
    IEnumerable<object> Fields();
    void AddValues(KnowledgeDocument document, Dictionary<string, object> values);
    string FilterSuffix(DateTimeOffset now);
}
