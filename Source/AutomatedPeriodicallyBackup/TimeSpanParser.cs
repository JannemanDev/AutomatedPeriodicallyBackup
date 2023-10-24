public class TimeSpanParser
{
    public static TimeSpan Parse(string durationString)
    {
        string[] parts = durationString.Split(':');

        int days = 0;
        int hours = 0;
        int minutes = 0;
        int seconds = 0;

        for (int i = 0; i < parts.Length; i++)
        {
            int value = string.IsNullOrEmpty(parts[i]) ? 0 : int.Parse(parts[i]);

            if (i == parts.Length - 1)
            {
                seconds = value;
            }
            else if (i == parts.Length - 2)
            {
                minutes = value;
            }
            else if (i == parts.Length - 3)
            {
                hours = value;
            }
            else if (i == parts.Length - 4)
            {
                days = value;
            }
        }

        TimeSpan timeSpan = new TimeSpan(days, hours, minutes, seconds);
        return timeSpan;
    }

    public static string Format(TimeSpan timeSpan)
    {
        string formattedDuration = $"{timeSpan.Days:D2}:{timeSpan.Hours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        return formattedDuration;
    }
}
