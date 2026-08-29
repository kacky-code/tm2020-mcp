namespace Tm2020Mcp.Maps;

/// <summary>
/// One free block to write into a map: a world position and a rotation, with no grid cell
/// involved.
/// </summary>
/// <remarks>
/// Angles are degrees here because that is how the corpus reads: yaw in real maps clusters on
/// multiples of 45, and radians hide that. They are converted on the way into the file.
/// </remarks>
public sealed record FreeBlockPlacement(
    string Name,
    float X,
    float Y,
    float Z,
    float YawDegrees = 0,
    float PitchDegrees = 0,
    float RollDegrees = 0)
{
    public Vector3 Position => new(X, Y, Z);

    /// <remarks>
    /// Angles are wrapped into (-180, 180] first. A caller stepping a spiral round produces
    /// 405 or 450 degrees quite naturally, and while that survives a round trip it is not what
    /// any real map carries - the corpus never holds an angle outside that range.
    /// </remarks>
    public Vector3 RotationRadians => new(
        Wrap(YawDegrees) * MathF.PI / 180f,
        Wrap(PitchDegrees) * MathF.PI / 180f,
        Wrap(RollDegrees) * MathF.PI / 180f);

    public static float Wrap(float degrees)
    {
        var wrapped = degrees % 360f;

        if (wrapped > 180f)
            wrapped -= 360f;
        else if (wrapped <= -180f)
            wrapped += 360f;

        return wrapped;
    }

    /// <summary>Places a block by grid cell instead of world units, for mixing with grid work.</summary>
    public static FreeBlockPlacement AtCell(
        string name,
        float cellX,
        float cellY,
        float cellZ,
        float yawDegrees = 0,
        float pitchDegrees = 0,
        float rollDegrees = 0) =>
        new(
            name,
            cellX * MapBlock.CellWidth,
            cellY * MapBlock.CellHeight,
            cellZ * MapBlock.CellWidth,
            yawDegrees,
            pitchDegrees,
            rollDegrees);
}
