using System.Globalization;
using System.Text.RegularExpressions;

namespace Siraj.Core.Text;

public static partial class HtmlText
{
    [GeneratedRegex("<[^>]*>")]
    private static partial Regex Tag();

    // Preserve line breaks at block element boundaries.
    [GeneratedRegex(@"<br\s*/?>|</(p|div|h[1-6]|li)>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockEnd();

    [GeneratedRegex(@" ?\n ?")]
    private static partial Regex LineEdges();

    [GeneratedRegex("&#(\\d+);")]
    private static partial Regex Decimal();

    [GeneratedRegex("&#x([0-9a-fA-F]+);")]
    private static partial Regex Hex();

    [GeneratedRegex("[ \\t]+")]
    private static partial Regex Spaces();

    [GeneratedRegex("\\n{3,}")]
    private static partial Regex BlankLines();

    private static readonly (string Entity, string Text)[] Entities =
    [
        ("&quot;", "\""), ("&apos;", "'"), ("&#39;", "'"), ("&lt;", "<"), ("&gt;", ">"), ("&nbsp;", " "),
        ("&rlm;", ""), ("&lrm;", ""), ("&bull;", "•"), ("&mdash;", "\u2014"), ("&ndash;", "–"), ("&hellip;", "…"),
        // Decode &amp; last to avoid double-unescaping entities like &amp;lt;.
        ("&amp;", "&"),
    ];

    public static string Plain(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        var text = Tag().Replace(BlockEnd().Replace(html, "\n"), "");
        foreach (var (entity, plain) in Entities) text = text.Replace(entity, plain);
        text = Decimal().Replace(text, m => CodePoint(m, NumberStyles.None));
        text = Hex().Replace(text, m => CodePoint(m, NumberStyles.HexNumber));
        return BlankLines().Replace(LineEdges().Replace(Spaces().Replace(text, " "), "\n"), "\n\n").Trim();
    }

    private static string CodePoint(Match m, NumberStyles style) =>
        int.TryParse(m.Groups[1].Value, style, CultureInfo.InvariantCulture, out int code) && code is > 0 and <= 0x10FFFF and not (>= 0xD800 and <= 0xDFFF)
            ? char.ConvertFromUtf32(code)
            : m.Value;
}
