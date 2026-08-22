using System.Net;
using System.Text;
using Tm2020Mcp.Manialinks;

namespace Tm2020Mcp.Tests.Manialinks;

public sealed class ManialinkMediaProbeTests
{
    private static string[] Codes(IEnumerable<ManialinkFinding> findings) =>
        findings.Select(finding => finding.Code).ToArray();

    private static byte[] Webp(bool animated)
    {
        var bytes = new byte[32];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(bytes, 0);
        Encoding.ASCII.GetBytes("WEBP").CopyTo(bytes, 8);
        Encoding.ASCII.GetBytes("VP8X").CopyTo(bytes, 12);
        bytes[20] = animated ? (byte)0x02 : (byte)0x00;
        return bytes;
    }

    private static ManialinkMediaProbe ProbeReturning(
        HttpStatusCode status, string contentType, byte[]? body = null)
    {
        var handler = new StubHandler(status, contentType, body ?? []);
        return new ManialinkMediaProbe(new HttpClient(handler));
    }

    [Fact]
    public void ExtractReferences_FindsImageVideoAndAudioUrls()
    {
        const string xml = """
            <manialink version="3">
              <quad image="https://cdn.test/a.png" imagefocus="https://cdn.test/b.png" />
              <video data="https://cdn.test/c.webm" />
              <audio data="https://cdn.test/d.ogg" />
              <label text="not media" />
            </manialink>
            """;

        var references = ManialinkMediaProbe.ExtractReferences(xml);

        Assert.Equal(4, references.Count);
        Assert.Contains(references, reference => reference.Url == "https://cdn.test/a.png" && reference.Attribute == "image");
        Assert.Contains(references, reference => reference.Url == "https://cdn.test/b.png" && reference.Attribute == "imagefocus");
        Assert.Contains(references, reference => reference.Element == "video");
        Assert.Contains(references, reference => reference.Element == "audio");
    }

    [Fact]
    public void ExtractReferences_IgnoresMalformedXmlWithoutThrowing()
    {
        Assert.Empty(ManialinkMediaProbe.ExtractReferences("<manialink><quad image="));
    }

    [Fact]
    public async Task ProbeAsync_SkipsNonHttpReferences()
    {
        const string xml = """
            <manialink version="3">
              <quad image="file://Media/Images/local.png" />
              <quad image="file://ZoneFlags/Path/World" />
            </manialink>
            """;

        var probe = ProbeReturning(HttpStatusCode.NotFound, "text/html");

        Assert.Empty(await probe.ProbeAsync(xml));
    }

    [Fact]
    public async Task ProbeAsync_ReportsUnreachableOnNotFound()
    {
        const string xml = """<manialink version="3"><quad image="https://cdn.test/gone.png" /></manialink>""";

        var findings = await ProbeReturning(HttpStatusCode.NotFound, "text/html").ProbeAsync(xml);

        Assert.Contains("media.unreachable", Codes(findings));
        Assert.Contains(findings, finding => finding.Severity == ManialinkSeverity.Error);
    }

    [Fact]
    public async Task ProbeAsync_ReportsUnreachableWhenTheRequestThrows()
    {
        const string xml = """<manialink version="3"><quad image="https://nope.invalid/a.png" /></manialink>""";

        var probe = new ManialinkMediaProbe(new HttpClient(new ThrowingHandler()));

        Assert.Contains("media.unreachable", Codes(await probe.ProbeAsync(xml)));
    }

    [Fact]
    public async Task ProbeAsync_AcceptsAHealthyImage()
    {
        const string xml = """<manialink version="3"><quad image="https://cdn.test/ok.png" /></manialink>""";

        var findings = await ProbeReturning(HttpStatusCode.OK, "image/png").ProbeAsync(xml);

        Assert.DoesNotContain(findings, finding => finding.Severity == ManialinkSeverity.Error);
    }

    // A media URL that answers with a web page is the documented "clip page instead of a direct
    // media file" mistake, and it returns 200 so status alone will not catch it.
    [Fact]
    public async Task ProbeAsync_ReportsHtmlServedForAMediaUrl()
    {
        const string xml = """<manialink version="3"><video data="https://clips.test/chrmrt" /></manialink>""";

        var findings = await ProbeReturning(HttpStatusCode.OK, "text/html; charset=utf-8").ProbeAsync(xml);

        Assert.Contains("media.content-type", Codes(findings));
    }

    [Fact]
    public async Task ProbeAsync_ToleratesOctetStreamBecauseCdnsUseIt()
    {
        const string xml = """<manialink version="3"><quad image="https://cdn.test/ok.dds" /></manialink>""";

        var findings = await ProbeReturning(HttpStatusCode.OK, "application/octet-stream").ProbeAsync(xml);

        Assert.DoesNotContain("media.content-type", Codes(findings));
    }

    [Fact]
    public void IsAnimatedWebp_DetectsTheVp8xAnimationFlag()
    {
        Assert.True(ManialinkMediaProbe.IsAnimatedWebp(Webp(animated: true)));
        Assert.False(ManialinkMediaProbe.IsAnimatedWebp(Webp(animated: false)));
    }

    [Fact]
    public void IsAnimatedWebp_IsFalseForShortOrNonWebpData()
    {
        Assert.False(ManialinkMediaProbe.IsAnimatedWebp([1, 2, 3]));
        Assert.False(ManialinkMediaProbe.IsAnimatedWebp(Encoding.ASCII.GetBytes("not a webp at all, really")));
    }

    // The payoff: the static validator can only warn that a .webp may be animated. This decides it.
    [Fact]
    public async Task ProbeAsync_ConfirmsAnAnimatedWebpAsAnError()
    {
        const string xml = """<manialink version="3"><quad image="https://cdn.test/catjam.webp" /></manialink>""";

        var findings = await ProbeReturning(HttpStatusCode.OK, "image/webp", Webp(animated: true)).ProbeAsync(xml);

        Assert.Contains(findings, finding =>
            finding.Code == "media.image-animated" && finding.Severity == ManialinkSeverity.Error);
    }

    [Fact]
    public async Task ProbeAsync_ClearsAStaticWebp()
    {
        const string xml = """<manialink version="3"><quad image="https://cdn.test/pog.webp" /></manialink>""";

        var findings = await ProbeReturning(HttpStatusCode.OK, "image/webp", Webp(animated: false)).ProbeAsync(xml);

        Assert.Contains(findings, finding =>
            finding.Code == "media.image-static" && finding.Severity == ManialinkSeverity.Info);
        Assert.DoesNotContain("media.image-animated", Codes(findings));
    }

    [Fact]
    public async Task ProbeAsync_ChecksEachDistinctUrlOnlyOnce()
    {
        const string xml = """
            <manialink version="3">
              <quad image="https://cdn.test/same.png" />
              <quad image="https://cdn.test/same.png" />
              <quad image="https://cdn.test/same.png" />
            </manialink>
            """;

        var handler = new StubHandler(HttpStatusCode.NotFound, "text/html", []);
        var findings = await new ManialinkMediaProbe(new HttpClient(handler)).ProbeAsync(xml);

        Assert.Equal(1, handler.Requests);
        Assert.Single(findings, finding => finding.Code == "media.unreachable");
    }

    private sealed class StubHandler(HttpStatusCode status, string contentType, byte[] body) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            var response = new HttpResponseMessage(status) { Content = new ByteArrayContent(body) };
            response.Content.Headers.Remove("Content-Type");
            response.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("no such host");
    }
}
