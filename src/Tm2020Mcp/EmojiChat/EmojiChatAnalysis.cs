namespace Tm2020Mcp.EmojiChat;

public sealed record EmojiChatAnalysis(
    string Original,
    string PlainText,
    IReadOnlyList<string> EmojiTokens,
    IReadOnlyList<string> UnknownEmoji,
    IReadOnlyList<string> TrackmaniaFormatCodes,
    string ManialinkSafeText);
