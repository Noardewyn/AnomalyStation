namespace Anomaly.Client.Shared;

public static class NameTagPlacement
{
    // The label is bottom-aligned: reserve room for its text below the ceiling, with a gap above the visible head.
    public static float Height(float spriteTop, float ceiling)
        => Math.Max(spriteTop + 0.12f, Math.Min(spriteTop + 0.24f, ceiling - 0.35f));
}
