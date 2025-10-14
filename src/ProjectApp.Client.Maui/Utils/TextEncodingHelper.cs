using System.Text;

namespace ProjectApp.Client.Maui.Utils;

public static class TextEncodingHelper
{
    private static readonly Encoding Cp1251 = CodePagesEncodingProvider.Instance.GetEncoding(1251)!;
    private static readonly Encoding Latin1 = Encoding.Latin1;

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        if (!LooksBroken(value))
            return value;

        try
        {
            // Attempt CP1251->UTF8 fix
            var bytes1251 = Cp1251.GetBytes(value);
            var norm1251 = Encoding.UTF8.GetString(bytes1251);

            // Attempt Latin1->UTF8 fix (common mojibake: Ð, Ñ, Â)
            var bytesLatin1 = Latin1.GetBytes(value);
            var normLatin1 = Encoding.UTF8.GetString(bytesLatin1);

            // Pick the best candidate: prefer not-broken and higher Cyrillic score
            string best = value;
            int baseScore = CyrillicScore(value);
            if (!LooksBroken(norm1251) && CyrillicScore(norm1251) >= baseScore)
                best = norm1251;
            if (!LooksBroken(normLatin1) && CyrillicScore(normLatin1) > CyrillicScore(best))
                best = normLatin1;

            return best;
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

        // Common markers: replacement char, UTF-8 BOM artifacts and typical mojibake letters
        if (value.Contains('\ufffd') || value.Contains("пї") || value.Contains('Ð') || value.Contains('Ñ') || value.Contains('Â'))
            return true;

        // Very frequent mojibake pattern in RU: 'Р' or 'С' preceding cyrillic lowercase/uppercase
        for (var i = 0; i < value.Length - 1; i++)
        {
            var c = value[i];
            var n = value[i + 1];
            if ((c == 'Р' || c == 'С') && IsCyrillic(n))
                return true;
        }

        // Fallback: presence of odd cyrillic codepoints block used by mojibake
        foreach (var ch in value)
        {
            if (IsWeirdCyrillic(ch))
                return true;
        }

        return false;
    }

    private static bool IsWeirdCyrillic(char ch)
    {
        return ch >= '\u0452' && ch <= '\u045f';
    }

    private static bool IsCyrillic(char ch)
    {
        return ch >= '\u0400' && ch <= '\u04FF';
    }

    private static int CyrillicScore(string s)
    {
        var score = 0;
        foreach (var ch in s)
            if (IsCyrillic(ch)) score++;
        return score;
    }
}
