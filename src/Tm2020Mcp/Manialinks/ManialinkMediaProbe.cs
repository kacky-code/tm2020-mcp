using System.Text;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace Tm2020Mcp.Manialinks;

public sealed record MediaReference(string Element, string Attribute, string Url);

// Answers the media questions the game engine will not.
//
// The W7 probe established that CGameManialinkQuad reports no usable load state: across 2,338
// image quads on a real client, including ones visibly on screen, CPlugBitmap Image was null and
// DownloadInProgress false. So "did this image actually load" cannot be read from the client, and
// is answered here instead by fetching the URL directly.
//
// This deliberately needs no game, no Openplanet and no Club Access, which keeps it on the
// ungated side of the tool. See docs/manialink-tm2020.md and WAYFINDER-MAP.md.
public sealed partial class ManialinkMediaProbe
{
    private const string SyntheticRoot = "tm2020McpMediaRoot";

    // Enough to cover a RIFF header plus the VP8X chunk's flags byte at offset 20.
    private const int SniffBytes = 32;

    private readonly HttpClient _http;

    public ManialinkMediaProbe(HttpClient http)
    {
        _http = http;
    }

    public static IReadOnlyList<MediaReference> ExtractReferences(string xml)
    {
        var references = new List<MediaReference>();
        if (string.IsNullOrWhiteSpace(xml))
            return references;

        var declaration = XmlDeclarationRegex().Match(xml);
        var body = declaration.Success ? xml.Remove(declaration.Index, declaration.Length) : xml;

        XElement root;
        try
        {
            root = XElement.Parse($"<{SyntheticRoot}>{body}</{SyntheticRoot}>");
        }
        catch (System.Xml.XmlException)
        {
            // Malformed XML is the validator's finding to report, not this probe's.
            return references;
        }

        foreach (var element in root.Descendants())
        {
            var name = element.Name.LocalName.ToLowerInvariant();
            foreach (var attribute in element.Attributes())
            {
                var attributeName = attribute.Name.LocalName.ToLowerInvariant();
                var isMedia = (name == "quad" && attributeName is "image" or "imagefocus")
                    || (name is "video" or "audio" or "music" && attributeName == "data");

                if (isMedia && !string.IsNullOrWhiteSpace(attribute.Value))
                    references.Add(new MediaReference(name, attributeName, attribute.Value));
            }
        }

        return references;
    }

    public async Task<IReadOnlyList<ManialinkFinding>> ProbeAsync(
        string xml, CancellationToken cancellationToken = default)
    {
        var findings = new List<ManialinkFinding>();

        // Only http(s) can be probed. file:// references, including built-in engine resources,
        // are the static validator's business.
        var targets = ExtractReferences(xml)
            .Where(reference => reference.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || reference.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            .GroupBy(reference => reference.Url, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();

        foreach (var target in targets)
            findings.AddRange(await ProbeOneAsync(target, cancellationToken));

        return findings;
    }

    private async Task<IReadOnlyList<ManialinkFinding>> ProbeOneAsync(
        MediaReference reference, CancellationToken cancellationToken)
    {
        var findings = new List<ManialinkFinding>();

        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, reference.Url);
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or UriFormatException or InvalidOperationException)
        {
            findings.Add(new ManialinkFinding(
                ManialinkSeverity.Error,
                "media.unreachable",
                $"<{reference.Element} {reference.Attribute}=\"{reference.Url}\"> could not be fetched: {exception.Message}",
                reference.Element));
            return findings;
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                findings.Add(new ManialinkFinding(
                    ManialinkSeverity.Error,
                    "media.unreachable",
                    $"<{reference.Element} {reference.Attribute}=\"{reference.Url}\"> returned "
                        + $"{(int)response.StatusCode} {response.ReasonPhrase}. The client will render nothing.",
                    reference.Element));
                return findings;
            }

            var contentType = response.Content.Headers.TryGetValues("Content-Type", out var values)
                ? values.FirstOrDefault() ?? string.Empty
                : string.Empty;

            // Only flag content types that are unambiguously a document. CDNs legitimately serve
            // media as application/octet-stream, so anything less specific would false-positive.
            if (LooksLikeADocument(contentType))
            {
                findings.Add(new ManialinkFinding(
                    ManialinkSeverity.Error,
                    "media.content-type",
                    $"<{reference.Element} {reference.Attribute}=\"{reference.Url}\"> answered 200 but served "
                        + $"'{contentType}'. That is a web page, not a media file. ManiaLink needs a direct media URL.",
                    reference.Element));
                return findings;
            }

            if (Tm2020ManialinkFacts.ExtensionOf(reference.Url).Equals(".webp", StringComparison.OrdinalIgnoreCase))
                findings.AddRange(await InspectWebpAsync(reference, response, cancellationToken));
        }

        return findings;
    }

    // Settles what the static validator can only warn about: a .webp URL does not say whether the
    // payload is animated, and animated WebP does not decode in-game.
    private static async Task<IReadOnlyList<ManialinkFinding>> InspectWebpAsync(
        MediaReference reference, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var header = new byte[SniffBytes];
        int read;
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            read = await ReadAtLeastAsync(stream, header, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException)
        {
            return
            [
                new ManialinkFinding(
                    ManialinkSeverity.Warning,
                    "media.image-format",
                    $"<{reference.Element} {reference.Attribute}=\"{reference.Url}\"> could not be inspected "
                        + $"({exception.Message}), so whether it is animated is still unknown.",
                    reference.Element)
            ];
        }

        if (IsAnimatedWebp(header.AsSpan(0, read)))
        {
            return
            [
                new ManialinkFinding(
                    ManialinkSeverity.Error,
                    "media.image-animated",
                    $"<{reference.Element} {reference.Attribute}=\"{reference.Url}\"> is an animated WebP, "
                        + "confirmed from its VP8X header. It will not decode in-game. Mirror it as a "
                        + "VP9-with-alpha WebM and use it as a video-backed quad.",
                    reference.Element)
            ];
        }

        return
        [
            new ManialinkFinding(
                ManialinkSeverity.Info,
                "media.image-static",
                $"<{reference.Element} {reference.Attribute}=\"{reference.Url}\"> is a static WebP, "
                    + "confirmed from its header. It loads in-game.",
                reference.Element)
        ];
    }

    // WebP is a RIFF container. Animation is advertised by the ANIM flag (0x02) in the flags byte
    // at offset 20, which only exists when the first chunk is the extended-format VP8X.
    public static bool IsAnimatedWebp(ReadOnlySpan<byte> header)
    {
        if (header.Length < 21)
            return false;

        if (!Ascii(header[..4]).Equals("RIFF", StringComparison.Ordinal))
            return false;

        if (!Ascii(header.Slice(8, 4)).Equals("WEBP", StringComparison.Ordinal))
            return false;

        if (!Ascii(header.Slice(12, 4)).Equals("VP8X", StringComparison.Ordinal))
            return false;

        return (header[20] & 0x02) != 0;
    }

    private static string Ascii(ReadOnlySpan<byte> bytes) => Encoding.ASCII.GetString(bytes);

    private static bool LooksLikeADocument(string contentType)
    {
        var value = contentType.ToLowerInvariant();
        return value.StartsWith("text/html")
            || value.StartsWith("text/plain")
            || value.StartsWith("application/json")
            || value.StartsWith("application/xhtml");
    }

    private static async Task<int> ReadAtLeastAsync(
        Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken);
            if (read == 0)
                break;
            total += read;
        }

        return total;
    }

    [GeneratedRegex(@"<\?xml\b[^>]*\?>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex XmlDeclarationRegex();
}
