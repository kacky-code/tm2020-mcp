using GBX.NET;
using GBX.NET.Engines.Game;
using GBX.NET.LZO;

namespace Tm2020Mcp.Maps;

/// <summary>
/// Reads blocks out of .Map.Gbx files. Needs no running game and no bridge, which is the
/// point: it answers grid questions the editor API cannot, such as whether a track's blocks
/// actually connect.
/// </summary>
public sealed class MapGbxReader
{
    private static readonly Lock LzoGate = new();
    private static bool _lzoReady;

    /// <summary>
    /// The map body is LZO-compressed; parsing throws without this. Setting it is global
    /// state in GBX.NET, so do it once and lazily rather than at every call site.
    /// </summary>
    internal static void EnsureLzo()
    {
        if (_lzoReady)
            return;

        lock (LzoGate)
        {
            if (_lzoReady)
                return;

            Gbx.LZO = new Lzo();
            _lzoReady = true;
        }
    }

    public MapGbxFile Read(string path)
    {
        EnsureLzo();

        var map = Gbx.ParseNode<CGameCtnChallenge>(path);
        var blocks = map.GetBlocks()
            .Select(b => new MapBlock(
                b.Name,
                b.Coord.X,
                b.Coord.Y,
                b.Coord.Z,
                b.IsFree ? "" : b.Direction.ToString(),
                b.IsFree,
                b.Variant,
                b.AbsolutePositionInMap is { } p ? new Vector3(p.X, p.Y, p.Z) : null,
                b.YawPitchRoll is { } r ? new Vector3(r.X, r.Y, r.Z) : null))
            .ToArray();

        return new MapGbxFile(
            Path.GetFileName(path),
            map.MapName,
            map.Decoration?.Id,
            map.Size.ToString(),
            blocks);
    }

    /// <summary>
    /// Expands a file or directory into map paths, skipping the <c>._</c> AppleDouble stubs
    /// that macOS-sourced archives carry. Those double the apparent map count.
    /// </summary>
    public static IReadOnlyList<string> EnumerateMaps(string path)
    {
        if (File.Exists(path))
            return [path];

        if (!Directory.Exists(path))
            return [];

        return Directory
            .EnumerateFiles(path, "*.Map.Gbx", SearchOption.AllDirectories)
            .Where(p => !Path.GetFileName(p).StartsWith("._", StringComparison.Ordinal))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
