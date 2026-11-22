using Anomaly.Client.Shared.Light;

namespace Anomaly.Server.Tests.Client.Light;

/// <summary>A map of arrays for the light tests: one level (z 1), tiles 1..MaxX by 1..MaxY.</summary>
internal sealed class ArrayCells : ILightCells
{
    private readonly bool[] _opaque;
    private readonly uint[] _baseLight;
    private readonly bool[] _space;

    public ArrayCells(int maxX, int maxY)
    {
        MaxX = maxX;
        MaxY = maxY;
        _opaque = new bool[(maxX + 1) * (maxY + 1)];
        _baseLight = new uint[(maxX + 1) * (maxY + 1)];
        _space = new bool[(maxX + 1) * (maxY + 1)];
    }

    public int MaxX { get; }

    public int MaxY { get; }

    public bool IsOpaque(int x, int y, int z) => x < 1 || y < 1 || x > MaxX || y > MaxY || _opaque[y * (MaxX + 1) + x];

    public uint BaseLight(int x, int y, int z) => _baseLight[y * (MaxX + 1) + x];

    public bool IsSpaceTurf(int x, int y, int z) => _space[y * (MaxX + 1) + x];

    public void SetOpaque(int x, int y, bool value) => _opaque[y * (MaxX + 1) + x] = value;

    public void SetBaseLight(int x, int y, uint value) => _baseLight[y * (MaxX + 1) + x] = value;

    public void SetSpace(int x, int y, bool value) => _space[y * (MaxX + 1) + x] = value;

    public void Fill(int x0, int y0, int x1, int y1, bool opaque)
    {
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                SetOpaque(x, y, opaque);
    }
}
