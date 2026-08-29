using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tm2020Mcp.Maps;

/// <summary>
/// Which neighbouring cells a block connects to, learned per (block name, direction) from a
/// corpus of maps.
/// </summary>
/// <remarks>
/// The engine will not tell us this. A block model's shape - straight, curve, start - only
/// shows up in how it sits next to its neighbours in maps people built, so the model is
/// counted rather than declared. A straight yields two opposite offsets, a curve two
/// perpendicular ones, and a start or finish exactly one.
///
/// Derivation and caveats: docs/tm2020-map-geometry.md.
/// </remarks>
public sealed class BlockConnectionModel
{
    private readonly Dictionary<string, IReadOnlyList<GridOffset>> _connections;

    private BlockConnectionModel(Dictionary<string, IReadOnlyList<GridOffset>> connections)
    {
        _connections = connections;
    }

    public int EntryCount => _connections.Count;

    /// <summary>Offsets this block connects to, or an empty list when it was never learned.</summary>
    public IReadOnlyList<GridOffset> Connections(string name, string direction) =>
        _connections.GetValueOrDefault(Key(name, direction), []);

    public bool Knows(string name, string direction) => _connections.ContainsKey(Key(name, direction));

    /// <summary>
    /// Counts neighbours per (name, direction) and keeps the offsets that show up often
    /// enough to be structural rather than an adjacent part of the route.
    /// </summary>
    /// <param name="minimumSamples">Ignore blocks seen fewer times than this.</param>
    /// <param name="keepRatio">
    /// Fraction of a block's sightings an offset must reach to count as a connection. Kacky
    /// maps run routes alongside each other, so a low threshold invents connections through
    /// walls; too high a one loses curve exits, which are rarer than straight chaining.
    /// </param>
    /// <param name="variant">
    /// Restrict learning to one block variant. The bridge places variant 0, and a curve's
    /// variant changes its shape, so a model meant for generation must not average across
    /// them.
    /// </param>
    /// <param name="namePrefix">Restrict learning to one block family, for example "Road".</param>
    public static BlockConnectionModel Learn(
        IEnumerable<IReadOnlyList<MapBlock>> maps,
        int minimumSamples = 20,
        double keepRatio = 0.35,
        int? variant = null,
        string? namePrefix = null)
    {
        var neighbours = new Dictionary<(string Name, string Dir, GridOffset Offset), int>();
        var totals = new Dictionary<(string Name, string Dir), int>();

        foreach (var blocks in maps)
        {
            var byCoord = new Dictionary<(int, int, int), MapBlock>();
            foreach (var block in blocks.Where(b => !b.IsFree && b.Direction.Length > 0))
                byCoord[(block.X, block.Y, block.Z)] = block;

            foreach (var block in byCoord.Values)
            {
                if (variant is not null && block.Variant != variant)
                    continue;

                if (namePrefix is not null && !block.Name.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                var key = (block.Name, block.Direction);
                totals[key] = totals.GetValueOrDefault(key) + 1;

                foreach (var offset in GridOffset.All)
                {
                    var coord = (block.X + offset.X, block.Y, block.Z + offset.Z);
                    if (!byCoord.ContainsKey(coord))
                        continue;

                    var neighbourKey = (block.Name, block.Direction, offset);
                    neighbours[neighbourKey] = neighbours.GetValueOrDefault(neighbourKey) + 1;
                }
            }
        }

        var learned = new Dictionary<string, IReadOnlyList<GridOffset>>();
        foreach (var ((name, dir), total) in totals)
        {
            if (total < minimumSamples)
                continue;

            var kept = GridOffset.All
                .Where(o => neighbours.GetValueOrDefault((name, dir, o)) >= total * keepRatio)
                .ToArray();

            if (kept.Length > 0)
                learned[Key(name, dir)] = kept;
        }

        return new BlockConnectionModel(learned);
    }

    public string ToJson() => JsonSerializer.Serialize(
        _connections.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Select(o => o.Label).ToArray()),
        JsonOptions);

    public static BlockConnectionModel FromJson(string json)
    {
        var raw = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json, JsonOptions)
            ?? throw new ArgumentException("Connection model JSON was empty.", nameof(json));

        return new BlockConnectionModel(raw.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<GridOffset>)kv.Value.Select(GridOffset.Parse).ToArray()));
    }

    /// <summary>
    /// The road-family model shipped with the server, learned from 450 Kacky maps at
    /// variant 0. Regenerate it with <c>learn_map_block_connections</c> against a corpus.
    /// </summary>
    public static BlockConnectionModel LoadBundled()
    {
        const string resource = "Tm2020Mcp.Maps.block-connections.json";

        using var stream = typeof(BlockConnectionModel).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Embedded resource {resource} is missing.");

        using var reader = new StreamReader(stream);
        return FromJson(reader.ReadToEnd());
    }

    private static string Key(string name, string direction) => $"{name}|{direction}";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

public readonly record struct GridOffset(int X, int Z)
{
    public static readonly GridOffset PlusX = new(1, 0);
    public static readonly GridOffset MinusX = new(-1, 0);
    public static readonly GridOffset PlusZ = new(0, 1);
    public static readonly GridOffset MinusZ = new(0, -1);

    public static readonly GridOffset[] All = [PlusX, MinusX, PlusZ, MinusZ];

    public GridOffset Reversed => new(-X, -Z);

    public string Label => (X, Z) switch
    {
        (1, 0) => "+X",
        (-1, 0) => "-X",
        (0, 1) => "+Z",
        (0, -1) => "-Z",
        _ => $"({X},{Z})"
    };

    public static GridOffset Parse(string label) => label switch
    {
        "+X" => PlusX,
        "-X" => MinusX,
        "+Z" => PlusZ,
        "-Z" => MinusZ,
        _ => throw new ArgumentException($"Unknown offset label '{label}'.", nameof(label))
    };

    public override string ToString() => Label;
}
