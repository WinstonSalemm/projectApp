using System.Text;

namespace ProjectApp.Client.Maui.Utils;

public static class TextEncodingHelper
{
    private static readonly Encoding Cp1251 = CodePagesEncodingProvider.Instance.GetEncoding(1251)!;

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        if (!LooksBroken(value))
            return value;

        try
        {
            var bytes = Cp1251.GetBytes(value);
            var normalized = Encoding.UTF8.GetString(bytes);
            return LooksBroken(normalized) ? value : normalized;
        }
        catch
        {
            return value;
        }
    }

    private static bool LooksBroken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (value.Contains('\ufffd') || value.Contains("пї"))
            return true;

        foreach (var ch in value)
        {
            if (IsWeirdCyrillic(ch))
                return true;
        }

        for (var i = 0; i < value.Length - 1; i++)
        {
            var current = value[i];
            if ((current == 'Р' || current == 'С') && IsWeirdCyrillic(value[i + 1]))
                return true;
        }

        return false;
    }

    private static bool IsWeirdCyrillic(char ch)
    {
        return ch >= '\u0452' && ch <= '\u045f';
    }
}
