using Newtonsoft.Json;

public static class ObjectExtensions
{
    public static string AsJson(this object obj, Formatting formatting = Formatting.Indented)
    {
        string json = JsonConvert.SerializeObject(obj, formatting);
        return json;
    }
}
