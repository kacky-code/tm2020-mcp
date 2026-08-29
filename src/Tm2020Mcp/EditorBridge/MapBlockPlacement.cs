using System.Text.Json.Serialization;

namespace Tm2020Mcp.EditorBridge;

/// <summary>
/// One block for <c>POST /map/blocks</c>. A negative <paramref name="Y"/> asks the bridge to
/// scan upwards for the first level where the engine accepts the block, which avoids
/// hardcoding a ground height that differs per decoration.
/// </summary>
public sealed record MapBlockPlacement(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("z")] int Z,
    [property: JsonPropertyName("y")] int Y = -1,
    [property: JsonPropertyName("dir")] string Dir = "North");
