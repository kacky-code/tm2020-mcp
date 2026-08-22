using Tm2020Mcp.Manialinks;

namespace Tm2020Mcp.Tests.Manialinks;

public sealed class ManialinkValidatorTests
{
    private readonly ManialinkValidator _validator = new();

    private static string[] Codes(IEnumerable<ManialinkFinding> findings) =>
        findings.Select(finding => finding.Code).ToArray();

    [Fact]
    public void Validate_CleanTm2020Document_HasNoErrors()
    {
        const string xml = """
            <manialink version="3" id="widget" name="widget" layer="normal">
              <frame pos="-60 35">
                <quad size="120 18" bgcolor="0d1014d" />
                <label pos="3 -3" text="Hello" />
              </frame>
            </manialink>
            """;

        var findings = _validator.Validate(xml);

        Assert.DoesNotContain(findings, finding => finding.Severity == ManialinkSeverity.Error);
    }

    [Fact]
    public void Validate_TmfDialect_IsRejected()
    {
        const string xml = """
            <manialink id="1">
              <label text="TMF style" />
            </manialink>
            """;

        var findings = _validator.Validate(xml);

        Assert.Contains("dialect.not-tm2020", Codes(findings));
        Assert.Contains(findings, finding =>
            finding.Code == "dialect.not-tm2020" && finding.Severity == ManialinkSeverity.Error);
    }

    [Fact]
    public void Validate_OlderManialinkVersion_IsRejected()
    {
        const string xml = """<manialink version="1"><label text="x" /></manialink>""";

        Assert.Contains("dialect.not-tm2020", Codes(_validator.Validate(xml)));
    }

    [Fact]
    public void Validate_MalformedXml_IsReported()
    {
        const string xml = """<manialink version="3"><label text="unclosed"></manialink>""";

        var findings = _validator.Validate(xml);

        Assert.Contains("xml.malformed", Codes(findings));
    }

    [Theory]
    [InlineData("https://example.com/clip.mp4")]
    [InlineData("https://videodelivery.net/abc123/manifest/video.m3u8")]
    [InlineData("https://clips-dev.kacky.gg/chrmrt")]
    public void Validate_NonWebmVideo_IsRejected(string data)
    {
        var xml = $"""<manialink version="3"><video data="{data}" play="1" /></manialink>""";

        var findings = _validator.Validate(xml);

        Assert.Contains("media.video-format", Codes(findings));
    }

    [Fact]
    public void Validate_WebmVideo_IsAccepted()
    {
        const string xml = """
            <manialink version="3">
              <video data="https://cdn.kacky.gg/clips/abc.webm" play="1" />
            </manialink>
            """;

        Assert.DoesNotContain("media.video-format", Codes(_validator.Validate(xml)));
    }

    [Fact]
    public void Validate_AnimatedGifImage_IsRejected()
    {
        const string xml = """
            <manialink version="3">
              <quad image="https://cdn.7tv.app/emote/01F6MQ33FG000FFJ97ZB8MWV52/3x.gif" size="4 4" />
            </manialink>
            """;

        Assert.Contains("media.image-animated", Codes(_validator.Validate(xml)));
    }

    [Fact]
    public void Validate_AvifImage_IsRejectedAsUnsupported()
    {
        const string xml = """
            <manialink version="3">
              <quad image="https://cdn.7tv.app/emote/01F5VW2TKR0003RCV2Z6JBHCST/4x.avif" size="4 4" />
            </manialink>
            """;

        var findings = _validator.Validate(xml);

        Assert.Contains(findings, finding =>
            finding.Code == "media.image-format" && finding.Severity == ManialinkSeverity.Error);
    }

    [Fact]
    public void Validate_WebpImage_WarnsBecauseAnimatedWebpDoesNotDecode()
    {
        const string xml = """
            <manialink version="3">
              <quad image="https://cdn.7tv.app/emote/63071bb9464de28875c52531/4x.webp" size="4 4" />
            </manialink>
            """;

        var findings = _validator.Validate(xml);

        Assert.Contains(findings, finding =>
            finding.Code == "media.image-format" && finding.Severity == ManialinkSeverity.Warning);
    }

    [Fact]
    public void Validate_PngImage_IsAccepted()
    {
        const string xml = """
            <manialink version="3">
              <quad image="https://cdn.kacky.gg/emotes/peepoRun.png" size="4 4" />
            </manialink>
            """;

        var codes = Codes(_validator.Validate(xml));

        Assert.DoesNotContain("media.image-format", codes);
        Assert.DoesNotContain("media.image-animated", codes);
    }

    // The video-backed quad is how animated content actually works in TM2020: a remote VP9
    // WebM set as a quad image. Proven by the dashmap probes and shipped by the Kacky emote CDN.
    [Fact]
    public void Validate_WebmQuadImage_IsAccepted()
    {
        const string xml = """
            <manialink version="3">
              <quad size="12 12" image="https://cdn.kacky.gg/emotes/peepoRun.webm" keepratio="Fit" />
            </manialink>
            """;

        var findings = _validator.Validate(xml);

        Assert.DoesNotContain(findings, finding => finding.Severity == ManialinkSeverity.Error);
    }

    // Built-in engine resources are extensionless file:// paths and load fine.
    [Fact]
    public void Validate_BuiltInEngineResourcePath_IsAccepted()
    {
        const string xml = """
            <manialink version="3">
              <quad size="6 5" image="file://ZoneFlags/Path/World" keepratio="Fit" />
            </manialink>
            """;

        var findings = _validator.Validate(xml);

        Assert.DoesNotContain(findings, finding => finding.Severity == ManialinkSeverity.Error);
        Assert.DoesNotContain("media.local-file", Codes(findings));
    }

    [Fact]
    public void Validate_LocalFileMedia_WarnsAboutEditorPreview()
    {
        const string xml = """
            <manialink version="3">
              <quad image="file://Media/Images/emote.png" size="4 4" />
            </manialink>
            """;

        Assert.Contains("media.local-file", Codes(_validator.Validate(xml)));
    }

    [Fact]
    public void Validate_UnsupportedAudioFormat_IsRejected()
    {
        const string xml = """<manialink version="3"><audio data="https://x.test/a.mp3" /></manialink>""";

        Assert.Contains("media.audio-format", Codes(_validator.Validate(xml)));
    }

    [Fact]
    public void Validate_UnknownElement_IsReported()
    {
        const string xml = """<manialink version="3"><div class="row" /></manialink>""";

        Assert.Contains("element.unknown", Codes(_validator.Validate(xml)));
    }

    [Fact]
    public void Validate_MusicInsideFrame_IsRejected()
    {
        const string xml = """
            <manialink version="3">
              <frame><music data="https://x.test/track.ogg" /></frame>
            </manialink>
            """;

        Assert.Contains("element.music-in-frame", Codes(_validator.Validate(xml)));
    }

    [Fact]
    public void Validate_TopLevelPositionOutsideCoordinateSpace_IsReported()
    {
        const string xml = """<manialink version="3"><label pos="240 -12" text="off screen" /></manialink>""";

        Assert.Contains("layout.out-of-bounds", Codes(_validator.Validate(xml)));
    }

    [Fact]
    public void Validate_PositionInsideFrame_IsNotBoundsChecked()
    {
        const string xml = """
            <manialink version="3">
              <frame pos="-150 80"><label pos="240 -12" text="relative" /></frame>
            </manialink>
            """;

        Assert.DoesNotContain("layout.out-of-bounds", Codes(_validator.Validate(xml)));
    }

    [Fact]
    public void Validate_ScriptEventsWithoutScriptBlock_IsReported()
    {
        const string xml = """<manialink version="3"><label id="Close" scriptevents="1" text="Close" /></manialink>""";

        Assert.Contains("script.events-without-script", Codes(_validator.Validate(xml)));
    }

    [Fact]
    public void Validate_ScriptEventsWithScriptBlock_IsAccepted()
    {
        const string xml = """
            <manialink version="3">
              <label id="Close" scriptevents="1" text="Close" />
              <script><!-- main() { while (True) { yield; } } --></script>
            </manialink>
            """;

        Assert.DoesNotContain("script.events-without-script", Codes(_validator.Validate(xml)));
    }

    [Fact]
    public void Validate_DuplicateIds_AreReported()
    {
        const string xml = """
            <manialink version="3">
              <label id="row" text="a" />
              <label id="row" text="b" />
            </manialink>
            """;

        Assert.Contains("id.duplicate", Codes(_validator.Validate(xml)));
    }

    [Fact]
    public void Validate_DesignerTarget_RejectsDeclarationAndWrapper()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8" standalone="yes" ?>
            <manialink version="3"><label text="x" /></manialink>
            """;

        var codes = Codes(_validator.Validate(xml, ManialinkTarget.InterfaceDesigner));

        Assert.Contains("designer.declaration", codes);
        Assert.Contains("designer.wrapper", codes);
    }

    [Fact]
    public void Validate_DesignerTarget_RejectsInteractiveConstructs()
    {
        const string xml = """
            <frame>
              <label action="open-gps" text="GPS" />
              <quad scriptevents="1" />
              <entry name="search" />
            </frame>
            """;

        var codes = Codes(_validator.Validate(xml, ManialinkTarget.InterfaceDesigner));

        Assert.Contains("designer.interactive", codes);
    }

    [Fact]
    public void Validate_CleanDesignerFragment_HasNoErrors()
    {
        const string xml = """
            <frame pos="-60 35">
              <quad size="120 18" bgcolor="0d1014d" />
              <label pos="3 -3" text="Submission &lt; 12" />
            </frame>
            """;

        var findings = _validator.Validate(xml, ManialinkTarget.InterfaceDesigner);

        Assert.DoesNotContain(findings, finding => finding.Severity == ManialinkSeverity.Error);
    }

    [Fact]
    public void Validate_EmptyInput_IsReported()
    {
        Assert.Contains("xml.empty", Codes(_validator.Validate("   ")));
    }

    [Fact]
    public void Format_GroupsFindingsBySeverity()
    {
        const string xml = """<manialink id="1"><div /></manialink>""";

        var text = ManialinkValidator.Format(_validator.Validate(xml));

        Assert.Contains("Errors", text);
        Assert.Contains("dialect.not-tm2020", text);
    }
}
