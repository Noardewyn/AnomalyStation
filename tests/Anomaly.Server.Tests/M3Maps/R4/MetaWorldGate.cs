namespace Anomaly.Server.Tests.M3Maps.R4;

/// <summary>
/// How many MetaStation worlds the test run builds at once. A booted Meta world holds about 2 GB; xUnit runs as many test classes as
/// the machine has cores (32 here), and 32 Meta worlds at once overflow the memory: the machine swaps and every Meta test runs 15 to
/// 20 times slower than alone. The gate keeps the count under what the memory holds (the GC's available memory over 3 GB, at least 2,
/// at most the core count; <c>ANOMALY_META_PARALLEL</c> overrides it), while the light tests run as before. A world that waits more than
/// ten minutes goes ahead anyway (a test that holds two worlds at once cannot deadlock the run).
/// </summary>
internal static class MetaWorldGate
{
    private const long BytesPerWorld = 3L * 1024 * 1024 * 1024;
    private static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(10);

    public static int Capacity { get; } = ComputeCapacity();

    private static readonly SemaphoreSlim Slots = new(Capacity, Capacity);

    private static int ComputeCapacity()
    {
        if (int.TryParse(Environment.GetEnvironmentVariable("ANOMALY_META_PARALLEL"), out int set) && set > 0)
            return set;
        long available = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        return (int)Math.Clamp(available / BytesPerWorld, 2, Environment.ProcessorCount);
    }

    /// <summary>Waits for a slot; true when one was taken (and must be given back with <see cref="Exit"/>).</summary>
    public static bool Enter() => Slots.Wait(MaxWait);

    public static void Exit() => Slots.Release();
}
