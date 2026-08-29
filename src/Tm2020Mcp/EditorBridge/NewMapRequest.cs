using System.Text.Json.Serialization;

namespace Tm2020Mcp.EditorBridge;

/// <summary>
/// Options for <c>POST /map/new</c>. The bridge passes these straight to
/// <c>CGameManiaTitleControlScriptAPI::EditNewMap2</c>, so the values are engine strings,
/// not friendly names.
/// </summary>
public sealed record NewMapRequest(
    [property: JsonPropertyName("environment")] string Environment = "Stadium",
    [property: JsonPropertyName("decoration")] string Decoration = "48x48Screen155Day",
    [property: JsonPropertyName("map_type")] string MapType = "TrackMania\\TM_Race",
    [property: JsonPropertyName("mod")] string Mod = "",
    [property: JsonPropertyName("player_model")] string PlayerModel = "",
    [property: JsonPropertyName("use_simple_editor")] bool UseSimpleEditor = false);
