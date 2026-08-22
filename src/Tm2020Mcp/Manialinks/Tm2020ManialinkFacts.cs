namespace Tm2020Mcp.Manialinks;

// Trackmania 2020 ManiaLink facts used by ManialinkValidator.
// Every constraint here is either documented upstream or was observed in-game; see
// docs/manialink-tm2020.md for the source of each group.
public static class Tm2020ManialinkFacts
{
    // ManiaLink coordinate space is 320 x 180 centred on the origin.
    public const double HalfWidth = 160;
    public const double HalfHeight = 90;

    // TM2020 serves ManiaLink v3. Earlier versions are Maniaplanet/TMF era.
    public const string RequiredManialinkVersion = "3";

    public static readonly IReadOnlySet<string> KnownElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "manialinks", "manialink", "frame", "framemodel", "frameinstance",
        "quad", "label", "entry", "fileentry", "textedit",
        "audio", "music", "video", "graph", "gauge",
        "include", "script", "stylesheet"
    };

    // Elements whose children use coordinates relative to the container.
    public static readonly IReadOnlySet<string> PositioningContainers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "frame", "framemodel", "frameinstance"
    };

    // .webm belongs here on purpose. A remote VP9 WebM set as a quad image is the supported
    // way to show animated content in TM2020; generic ManiaLink references list only stills.
    public static readonly IReadOnlySet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".dds", ".webm"
    };

    // Static WebP decodes; animated WebP does not, and the URL never says which it is.
    public static readonly IReadOnlySet<string> AmbiguousImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".webp"
    };

    // Observed in-game: the client attempts these and fails to decode.
    public static readonly IReadOnlySet<string> AnimatedImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".gif", ".apng"
    };

    public static readonly IReadOnlySet<string> VideoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".webm"
    };

    public static readonly IReadOnlySet<string> AudioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".ogg", ".wav", ".mux"
    };

    // Adaptive-streaming manifests are not playable by the ManiaLink video element.
    public static readonly IReadOnlySet<string> StreamingManifestExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".m3u8", ".mpd"
    };

    // Constructs the Interface Designer either rejects on paste or renders unpredictably.
    public static readonly IReadOnlySet<string> DesignerUnsafeElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "framemodel", "frameinstance", "entry", "fileentry", "textedit", "script", "include"
    };

    public static readonly IReadOnlySet<string> DesignerUnsafeAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "action", "scriptaction", "scriptevents", "class", "hidden", "url", "manialink"
    };

    public static readonly IReadOnlySet<string> MediaAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "image", "imagefocus", "data", "style"
    };

    // "https://host/path.png?x=1#y" -> ".png". Empty when the URL has no file extension.
    public static string ExtensionOf(string value)
    {
        var path = value;
        var cut = path.IndexOfAny(['?', '#']);
        if (cut >= 0)
            path = path[..cut];

        var lastSlash = path.LastIndexOf('/');
        var leaf = lastSlash >= 0 ? path[(lastSlash + 1)..] : path;
        var dot = leaf.LastIndexOf('.');
        return dot > 0 ? leaf[dot..] : string.Empty;
    }

    public static bool IsLocalFileReference(string value) =>
        value.StartsWith("file://", StringComparison.OrdinalIgnoreCase);

    // Built-in engine resources are extensionless file:// paths such as
    // file://ZoneFlags/Path/World. They resolve inside the client, unlike file:// paths
    // that point at real media files.
    public static bool IsBuiltInEngineResource(string value) =>
        IsLocalFileReference(value) && ExtensionOf(value).Length == 0;
}
