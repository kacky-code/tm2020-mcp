using System.Text;
using System.Text.RegularExpressions;

namespace Tm2020Mcp.EmojiChat;

public sealed partial class EmojiChatAnalyzer
{
    private static readonly HashSet<string> DefaultEmojiNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bonk",
        "clueless",
        "copium",
        "despair",
        "gigachad",
        "kacky",
        "kekw",
        "monka",
        "pepega",
        "pog",
        "pray",
        "xdd"
    };

    public EmojiChatAnalysis Analyze(string message, string? knownEmojiNames = null)
    {
        var known = ParseKnownEmojiNames(knownEmojiNames);
        var emoji = EmojiTokenRegex()
            .Matches(message)
            .Select(match => match.Groups["name"].Value)
            .ToArray();
        var unknown = emoji
            .Where(name => !known.Contains(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var formatCodes = TrackmaniaFormatCodeRegex()
            .Matches(message)
            .Select(match => match.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new EmojiChatAnalysis(
            message,
            StripTrackmaniaFormatCodes(message),
            emoji,
            unknown,
            formatCodes,
            ToManialinkSafeText(message));
    }

    public string BuildLabelPreviewXml(string message, string? knownEmojiNames = null)
    {
        var analysis = Analyze(message, knownEmojiNames);
        var status = analysis.UnknownEmoji.Count == 0
            ? "all emoji known"
            : $"unknown: {string.Join(", ", analysis.UnknownEmoji)}";

        return $"""
            <frame id="emoji-chat.lab.preview" pos="-80 40" z-index="1">
              <quad id="emoji-chat.lab.bg" pos="0 0" z-index="0" size="160 14" bgcolor="0d1014d" />
              <label id="emoji-chat.lab.message" pos="3 -3" z-index="1" size="154 5" text="{analysis.ManialinkSafeText}" textfont="RobotoCondensed" textsize="1.6" maxline="1" />
              <label id="emoji-chat.lab.status" pos="3 -9" z-index="1" size="154 4" text="{ToManialinkSafeText(status)}" textfont="RobotoCondensed" textsize="1" textcolor="999" maxline="1" />
            </frame>
            """;
    }

    private static HashSet<string> ParseKnownEmojiNames(string? knownEmojiNames)
    {
        var known = new HashSet<string>(DefaultEmojiNames, StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(knownEmojiNames))
            return known;

        foreach (var name in knownEmojiNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            known.Add(name.Trim(':'));

        return known;
    }

    private static string StripTrackmaniaFormatCodes(string value)
    {
        return TrackmaniaFormatCodeRegex().Replace(value, string.Empty);
    }

    private static string ToManialinkSafeText(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(ch switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                '\'' => "&apos;",
                _ => ch
            });
        }

        return builder.ToString();
    }

    [GeneratedRegex(@":(?<name>[A-Za-z0-9_+\-]{2,32}):", RegexOptions.Compiled)]
    private static partial Regex EmojiTokenRegex();

    [GeneratedRegex(@"\$[0-9A-Fa-f]{3}|\$[A-Za-z$<>]", RegexOptions.Compiled)]
    private static partial Regex TrackmaniaFormatCodeRegex();
}
