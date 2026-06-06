using Tm2020Mcp.EmojiChat;

namespace Tm2020Mcp.Tests.EmojiChat;

public sealed class EmojiChatAnalyzerTests
{
    [Fact]
    public void Analyze_ReturnsEmojiFormatCodesAndSafeText()
    {
        var analyzer = new EmojiChatAnalyzer();

        var result = analyzer.Analyze("$f00Bompi: :kekw: <gps> :missing:", knownEmojiNames: "kekw");

        Assert.Equal("Bompi: :kekw: <gps> :missing:", result.PlainText);
        Assert.Equal(["kekw", "missing"], result.EmojiTokens);
        Assert.Equal(["missing"], result.UnknownEmoji);
        Assert.Equal(["$f00"], result.TrackmaniaFormatCodes);
        Assert.Equal("$f00Bompi: :kekw: &lt;gps&gt; :missing:", result.ManialinkSafeText);
    }

    [Fact]
    public void BuildLabelPreviewXml_ReturnsPasteSafeFragment()
    {
        var analyzer = new EmojiChatAnalyzer();

        var xml = analyzer.BuildLabelPreviewXml("hello :pog: <gps>");

        Assert.DoesNotContain("<?xml", xml);
        Assert.DoesNotContain("<manialink", xml);
        Assert.Contains("emoji-chat.lab.preview", xml);
        Assert.Contains("&lt;gps&gt;", xml);
    }
}
