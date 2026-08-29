using System.Text.Json.Serialization;

namespace Tm2020Mcp.EditorBridge;

/// <summary>
/// One cell to clear for <c>POST /map/blocks/remove</c>. A negative <paramref name="Y"/> asks
/// the bridge to scan the column and take whatever is stacked there, mirroring the placement
/// scan.
/// </summary>
public sealed record MapBlockRemoval(
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("z")] int Z,
    [property: JsonPropertyName("y")] int Y = -1);
