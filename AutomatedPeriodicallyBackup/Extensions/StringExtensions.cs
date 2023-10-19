using Newtonsoft.Json;
using System.Text;

public static class StringExtensions
{
    public static string IndentLines(this string str, string indent)
    {
        List<string> lines = str.Split(Environment.NewLine).ToList();
        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < lines.Count; i++)
        {
            sb.AppendLine($"{indent}{lines[i]}");
        }

        return sb.ToString();
    }

    public static string UpperCaseFirstLetter(this string input)
    {
        if (!string.IsNullOrEmpty(input))
        {
            return char.ToUpper(input[0]) + input.Substring(1);
        }

        return input;
    }

    public static string BeautifyJson(this string str)
    {
        var obj = JsonConvert.DeserializeObject(str);
        string json = JsonConvert.SerializeObject(obj, Formatting.Indented);
        return json;
    }

    public static string TrimStart(this string target, string trimString)
    {
        if (string.IsNullOrEmpty(trimString)) return target;

        string result = target;
        while (result.StartsWith(trimString))
        {
            result = result.Substring(trimString.Length);
        }

        return result;
    }

    public static string TrimEnd(this string target, string trimString)
    {
        if (string.IsNullOrEmpty(trimString)) return target;

        string result = target;
        while (result.EndsWith(trimString))
        {
            result = result.Substring(0, result.Length - trimString.Length);
        }

        return result;
    }
}
