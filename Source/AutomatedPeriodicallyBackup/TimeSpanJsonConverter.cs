using Newtonsoft.Json;

public class TimeSpanJsonConverter : JsonConverter<TimeSpan>
{
    public override TimeSpan ReadJson(JsonReader reader, Type objectType, TimeSpan existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        string durationString = (string)reader.Value;
        return TimeSpanParser.Parse(durationString);
    }

    public override void WriteJson(JsonWriter writer, TimeSpan value, JsonSerializer serializer)
    {
        string durationString = TimeSpanParser.Format(value);
        writer.WriteValue(durationString);
    }
}
