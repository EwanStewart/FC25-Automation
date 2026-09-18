namespace Automation.Trading;

public static class XPath
{
    public static string Literal(string text)
    {
        string result;

        if (!text.Contains('\'')) result = $"'{text}'";
        else if (!text.Contains('"')) result = $"\"{text}\"";
        else result = $"concat({string.Join(", \"'\", ", text.Split('\'').Select(part => $"'{part}'"))})";

        return result;
    }
}
