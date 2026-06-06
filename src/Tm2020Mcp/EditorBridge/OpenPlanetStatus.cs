namespace Tm2020Mcp.EditorBridge;

public sealed record OpenPlanetStatus(
    bool Running,
    bool EditorOpen,
    bool MapEditor,
    bool InterfaceDesigner,
    bool ModuleEditor,
    bool ManialinkPreview);

