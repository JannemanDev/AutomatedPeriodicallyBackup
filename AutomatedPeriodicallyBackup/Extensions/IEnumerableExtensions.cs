using Newtonsoft.Json;

public static class IEnumerableExtensions
{
    public static string AsJson<T>(this IEnumerable<T> objects, Formatting formatting = Formatting.Indented)
    {
        string json = JsonConvert.SerializeObject(objects, formatting);
        return json;
    }
}
