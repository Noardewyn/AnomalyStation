namespace Anomaly.Core.Kernel;

/// <summary>
/// Order-sensitive 64-bit FNV-1a state hasher used to compare world states in determinism tests
/// ("the same scenario run twice gives an identical state hash"). Deliberately not
/// <see cref="HashCode"/>, which is randomized per process.
/// </summary>
public struct StateHasher
{
    private ulong _h;

    public static StateHasher Create() => new StateHasher { _h = 14695981039346656037UL };

    public ulong Value => _h;

    public void Add(ulong v)
    {
        for (int i = 0; i < 8; i++)
        {
            _h ^= (byte)(v >> (i * 8));
            _h *= 1099511628211UL;
        }
    }

    public void Add(long v) => Add(unchecked((ulong)v));
    public void Add(int v) => Add(unchecked((ulong)(long)v));
    public void Add(bool v) => Add(v ? 1UL : 0UL);
    /// <summary>
    /// Adds a float with -0 folded into +0 and every NaN folded into one canonical NaN: a NaN produced at run
    /// time has a different payload on arm64 (0x7FC00000) than on x64 and float.NaN (0xFFC00000).
    /// </summary>
    public void Add(float v)
    {
        if (float.IsNaN(v)) v = float.NaN;
        else if (v == 0f) v = 0f;
        Add(BitConverter.SingleToInt32Bits(v));
    }

    /// <summary>Adds a double with the same -0 and NaN folding as <see cref="Add(float)"/>.</summary>
    public void Add(double v)
    {
        if (double.IsNaN(v)) v = double.NaN;
        else if (v == 0d) v = 0d;
        Add(BitConverter.DoubleToInt64Bits(v));
    }

    public void Add(string v)
    {
        if (v == null) { Add(0UL); return; }
        Add((ulong)v.Length + 1);
        foreach (char c in v)
        {
            _h ^= (byte)c; _h *= 1099511628211UL;
            _h ^= (byte)(c >> 8); _h *= 1099511628211UL;
        }
    }
}
