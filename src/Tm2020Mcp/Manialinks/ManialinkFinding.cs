namespace Tm2020Mcp.Manialinks;

public enum ManialinkSeverity
{
    Info,
    Warning,
    Error
}

public enum ManialinkTarget
{
    // A full document pushed to the game or served as server HUD.
    Manialink,

    // A fragment meant for manual paste into the in-game Interface Designer.
    InterfaceDesigner
}

public sealed record ManialinkFinding(ManialinkSeverity Severity, string Code, string Message, string? Element = null);
