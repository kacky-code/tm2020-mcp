namespace Tm2020Mcp.EditorBridge;

/// <summary>
/// One server-sent HUD layer on the local client.
/// </summary>
/// <remarks>
/// This is the only way to see what a Nadeo UI module renders. The module scripts ship
/// inside the title pack and are not published, but the XML they produce is readable live.
/// <para>
/// <see cref="AttachId"/> is "Unassigned" on every layer in Trackmania 2020, so it cannot
/// identify a layer. Use <see cref="Tag"/>, the manialink opening tag, which carries the id.
/// </para>
/// </remarks>
public sealed record UiLayer(
    int Index,
    string AttachId,
    string Type,
    bool Visible,
    bool AnimInProgress,
    bool ScriptRunning,
    int XmlLength,
    string Tag);

/// <summary>
/// The layer list, plus whether the client was connected to a server at all.
/// </summary>
/// <remarks>
/// Connected is load bearing: in the menus there is no playground and the list is empty for
/// that reason rather than because the HUD is empty. An agent must be able to tell those apart.
/// </remarks>
public sealed record UiLayerList(bool Connected, IReadOnlyList<UiLayer> Layers, string? Error);
