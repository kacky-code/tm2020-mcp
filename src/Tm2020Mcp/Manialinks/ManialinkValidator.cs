using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Tm2020Mcp.Manialinks;

// Checks ManiaLink XML against the Trackmania 2020 constraints in Tm2020ManialinkFacts,
// so a fragment can be rejected locally instead of silently rendering nothing in-game.
public sealed partial class ManialinkValidator
{
    private const string SyntheticRoot = "tm2020McpValidationRoot";

    public IReadOnlyList<ManialinkFinding> Validate(string xml, ManialinkTarget target = ManialinkTarget.Manialink)
    {
        var findings = new List<ManialinkFinding>();

        if (string.IsNullOrWhiteSpace(xml))
        {
            findings.Add(new ManialinkFinding(ManialinkSeverity.Error, "xml.empty", "No ManiaLink XML provided."));
            return findings;
        }

        var declaration = XmlDeclarationRegex().Match(xml);
        var body = declaration.Success ? xml.Remove(declaration.Index, declaration.Length) : xml;

        XElement root;
        try
        {
            root = XElement.Parse($"<{SyntheticRoot}>{body}</{SyntheticRoot}>", LoadOptions.PreserveWhitespace);
        }
        catch (System.Xml.XmlException exception)
        {
            findings.Add(new ManialinkFinding(
                ManialinkSeverity.Error, "xml.malformed", $"XML does not parse: {exception.Message}"));
            return findings;
        }

        var elements = root.Descendants().ToList();

        if (target == ManialinkTarget.Manialink)
            CheckDialect(elements, findings);
        else
            CheckDesignerSafety(declaration.Success, elements, findings);

        CheckElements(elements, findings);
        CheckMedia(elements, findings);
        CheckLayout(elements, findings);
        CheckScriptEvents(elements, findings);
        CheckDuplicateIds(elements, findings);

        return findings;
    }

    // TM2020 renders ManiaLink v3. A wrapper without version="3" is TMF/Maniaplanet-era XML,
    // which parses fine and then does nothing useful here.
    private static void CheckDialect(List<XElement> elements, List<ManialinkFinding> findings)
    {
        foreach (var element in elements.Where(candidate => Name(candidate) == "manialink"))
        {
            var version = element.Attribute("version")?.Value;
            if (version == Tm2020ManialinkFacts.RequiredManialinkVersion)
                continue;

            var described = version is null ? "no version attribute" : $"version=\"{version}\"";
            findings.Add(new ManialinkFinding(
                ManialinkSeverity.Error,
                "dialect.not-tm2020",
                $"<manialink> has {described}. Trackmania 2020 expects version=\"3\"; "
                    + "an unversioned or lower-versioned document is TMF/Maniaplanet-era ManiaLink.",
                "manialink"));
        }
    }

    private static void CheckElements(List<XElement> elements, List<ManialinkFinding> findings)
    {
        foreach (var element in elements)
        {
            var name = Name(element);

            if (!Tm2020ManialinkFacts.KnownElements.Contains(name))
            {
                findings.Add(new ManialinkFinding(
                    ManialinkSeverity.Warning,
                    "element.unknown",
                    $"<{name}> is not a known Trackmania 2020 ManiaLink element and will be ignored by the client.",
                    name));
                continue;
            }

            if (name == "music" && element.Ancestors().Any(ancestor => Name(ancestor) == "frame"))
            {
                findings.Add(new ManialinkFinding(
                    ManialinkSeverity.Error,
                    "element.music-in-frame",
                    "<music> must sit outside any <frame>.",
                    name));
            }
        }
    }

    private static void CheckMedia(List<XElement> elements, List<ManialinkFinding> findings)
    {
        foreach (var element in elements)
        {
            var name = Name(element);

            foreach (var attribute in element.Attributes())
            {
                var attributeName = attribute.Name.LocalName.ToLowerInvariant();
                var value = attribute.Value;

                if (string.IsNullOrWhiteSpace(value))
                    continue;

                // A quad style/substyle reference is a built-in name, not a media path.
                if (attributeName is not ("image" or "imagefocus" or "data"))
                    continue;

                if (Tm2020ManialinkFacts.IsBuiltInEngineResource(value))
                    continue;

                if (Tm2020ManialinkFacts.IsLocalFileReference(value))
                {
                    findings.Add(new ManialinkFinding(
                        ManialinkSeverity.Warning,
                        "media.local-file",
                        $"<{name} {attributeName}=\"{value}\"> uses file://. Local media did not load in map-editor "
                            + "ManiaLink preview; host it over http(s) instead.",
                        name));
                }

                var extension = Tm2020ManialinkFacts.ExtensionOf(value);

                switch (name)
                {
                    case "video":
                        CheckVideo(name, value, extension, findings);
                        break;
                    case "audio":
                    case "music":
                        CheckAudio(name, attributeName, value, extension, findings);
                        break;
                    case "quad":
                        CheckImage(name, attributeName, value, extension, findings);
                        break;
                }
            }
        }
    }

    private static void CheckVideo(string name, string value, string extension, List<ManialinkFinding> findings)
    {
        if (Tm2020ManialinkFacts.VideoExtensions.Contains(extension))
            return;

        var reason = Tm2020ManialinkFacts.StreamingManifestExtensions.Contains(extension)
            ? "adaptive-streaming manifests are not playable by the ManiaLink video element"
            : extension.Length == 0
                ? "the URL has no media extension, so it is probably a web page rather than a direct media file"
                : $"{extension} is not playable; the video element needs WebM";

        findings.Add(new ManialinkFinding(
            ManialinkSeverity.Error,
            "media.video-format",
            $"<{name} data=\"{value}\"> will not play: {reason}. Provide a direct .webm URL.",
            name));
    }

    private static void CheckAudio(
        string name, string attributeName, string value, string extension, List<ManialinkFinding> findings)
    {
        if (attributeName != "data" || Tm2020ManialinkFacts.AudioExtensions.Contains(extension))
            return;

        var supported = string.Join(", ", Tm2020ManialinkFacts.AudioExtensions);
        findings.Add(new ManialinkFinding(
            ManialinkSeverity.Error,
            "media.audio-format",
            $"<{name} data=\"{value}\"> is not a supported audio format. Supported: {supported}.",
            name));
    }

    private static void CheckImage(
        string name, string attributeName, string value, string extension, List<ManialinkFinding> findings)
    {
        if (attributeName is not ("image" or "imagefocus"))
            return;

        if (Tm2020ManialinkFacts.ImageExtensions.Contains(extension))
            return;

        if (Tm2020ManialinkFacts.AnimatedImageExtensions.Contains(extension))
        {
            findings.Add(new ManialinkFinding(
                ManialinkSeverity.Error,
                "media.image-animated",
                $"<{name} {attributeName}=\"{value}\"> uses {extension}. Animated image payloads do not decode "
                    + "in-game; mirror the asset as a VP9-with-alpha WebM and drive it as a video-backed quad.",
                name));
            return;
        }

        if (Tm2020ManialinkFacts.AmbiguousImageExtensions.Contains(extension))
        {
            findings.Add(new ManialinkFinding(
                ManialinkSeverity.Warning,
                "media.image-format",
                $"<{name} {attributeName}=\"{value}\"> uses {extension}. Static WebP loads, animated WebP does not, "
                    + "and the URL does not say which this is. Verify in-game.",
                name));
            return;
        }

        var supported = string.Join(", ", Tm2020ManialinkFacts.ImageExtensions);
        var detail = extension.Length == 0 ? "no file extension" : extension;
        findings.Add(new ManialinkFinding(
            ManialinkSeverity.Error,
            "media.image-format",
            $"<{name} {attributeName}=\"{value}\"> has {detail}, which the client cannot load. Supported: {supported}.",
            name));
    }

    // Only un-framed elements are bounds-checked: inside a frame, coordinates are relative
    // to the container and an out-of-space number is meaningless on its own.
    private static void CheckLayout(List<XElement> elements, List<ManialinkFinding> findings)
    {
        foreach (var element in elements)
        {
            if (element.Ancestors().Any(ancestor =>
                    Tm2020ManialinkFacts.PositioningContainers.Contains(Name(ancestor))))
                continue;

            var pos = element.Attribute("pos")?.Value;
            if (pos is null || !TryReadPair(pos, out var x, out var y))
                continue;

            if (Math.Abs(x) <= Tm2020ManialinkFacts.HalfWidth && Math.Abs(y) <= Tm2020ManialinkFacts.HalfHeight)
                continue;

            findings.Add(new ManialinkFinding(
                ManialinkSeverity.Warning,
                "layout.out-of-bounds",
                $"<{Name(element)} pos=\"{pos}\"> sits outside the 320 x 180 ManiaLink space "
                    + "(x -160..160, y -90..90) and will not be visible.",
                Name(element)));
        }
    }

    private static void CheckScriptEvents(List<XElement> elements, List<ManialinkFinding> findings)
    {
        var hasScript = elements.Any(element => Name(element) == "script");
        if (hasScript)
            return;

        foreach (var element in elements)
        {
            if (element.Attribute("scriptevents")?.Value != "1")
                continue;

            findings.Add(new ManialinkFinding(
                ManialinkSeverity.Warning,
                "script.events-without-script",
                $"<{Name(element)} id=\"{element.Attribute("id")?.Value ?? "(none)"}\"> sets scriptevents=\"1\" "
                    + "but the document has no <script> block, so its events go nowhere.",
                Name(element)));
        }
    }

    private static void CheckDuplicateIds(List<XElement> elements, List<ManialinkFinding> findings)
    {
        var duplicates = elements
            .Select(element => element.Attribute("id")?.Value)
            .Where(id => !string.IsNullOrEmpty(id))
            .GroupBy(id => id!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1);

        foreach (var group in duplicates)
        {
            findings.Add(new ManialinkFinding(
                ManialinkSeverity.Warning,
                "id.duplicate",
                $"id=\"{group.Key}\" is used {group.Count()} times. ManiaScript GetFirstChild resolves only one of them."));
        }
    }

    private static void CheckDesignerSafety(
        bool hasDeclaration, List<XElement> elements, List<ManialinkFinding> findings)
    {
        if (hasDeclaration)
        {
            findings.Add(new ManialinkFinding(
                ManialinkSeverity.Error,
                "designer.declaration",
                "Interface Designer paste fragments must not carry an XML declaration."));
        }

        foreach (var element in elements)
        {
            var name = Name(element);

            if (name is "manialink" or "manialinks")
            {
                findings.Add(new ManialinkFinding(
                    ManialinkSeverity.Error,
                    "designer.wrapper",
                    $"Interface Designer paste fragments must not include the <{name}> wrapper.",
                    name));
                continue;
            }

            if (Tm2020ManialinkFacts.DesignerUnsafeElements.Contains(name))
            {
                findings.Add(new ManialinkFinding(
                    ManialinkSeverity.Error,
                    "designer.interactive",
                    $"<{name}> is not paste-safe for the Interface Designer. Keep fragments to static "
                        + "frame, quad and label nodes.",
                    name));
                continue;
            }

            foreach (var attribute in element.Attributes())
            {
                if (!Tm2020ManialinkFacts.DesignerUnsafeAttributes.Contains(attribute.Name.LocalName))
                    continue;

                findings.Add(new ManialinkFinding(
                    ManialinkSeverity.Error,
                    "designer.interactive",
                    $"<{name}> carries the runtime attribute {attribute.Name.LocalName}=\"{attribute.Value}\", "
                        + "which is not paste-safe for the Interface Designer.",
                    name));
            }
        }
    }

    public static string Format(IReadOnlyList<ManialinkFinding> findings)
    {
        if (findings.Count == 0)
            return "No Trackmania 2020 ManiaLink issues found.";

        var builder = new StringBuilder();
        var errors = findings.Count(finding => finding.Severity == ManialinkSeverity.Error);
        var warnings = findings.Count(finding => finding.Severity == ManialinkSeverity.Warning);
        var infos = findings.Count(finding => finding.Severity == ManialinkSeverity.Info);

        builder.AppendLine($"Errors: {errors}, Warnings: {warnings}, Info: {infos}");

        foreach (var severity in new[] { ManialinkSeverity.Error, ManialinkSeverity.Warning, ManialinkSeverity.Info })
        {
            var group = findings.Where(finding => finding.Severity == severity).ToList();
            if (group.Count == 0)
                continue;

            builder.AppendLine();
            builder.AppendLine($"{severity}s ({group.Count}):");
            foreach (var finding in group)
                builder.AppendLine($"- [{finding.Code}] {finding.Message}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string Name(XElement element) => element.Name.LocalName.ToLowerInvariant();

    private static bool TryReadPair(string value, out double first, out double second)
    {
        first = 0;
        second = 0;
        var parts = value.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out first)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out second);
    }

    [GeneratedRegex(@"<\?xml\b[^>]*\?>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex XmlDeclarationRegex();
}
