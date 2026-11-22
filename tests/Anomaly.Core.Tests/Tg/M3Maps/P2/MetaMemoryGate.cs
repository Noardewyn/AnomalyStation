using System.IO;
using System.Runtime.InteropServices;

namespace Anomaly.Core.Tests.Tg.M3Maps.P2;

/// <summary>
/// The Meta boots of the run wait for room in the memory. A booted Meta holds about 2 GB and the Core tests keep theirs (a class fixture's
/// for the class, a test's until the test ends) with no dispose to count, so the gate watches the machine's free physical memory: a boot
/// starts only while at least <see cref="Headroom"/> stays free (a full collection first, to drop the worlds of finished tests), and at
/// most <see cref="Boots"/> boot at once. Without it xUnit's parallel classes overflowed the memory and every test, the small ones too,
/// ran many times slower in the swap. A boot that waits longer than ten minutes goes ahead anyway.
/// </summary>
internal static class MetaMemoryGate
{
    private const long Headroom = 4L * 1024 * 1024 * 1024;
    private const int Boots = 4;
    private static readonly SemaphoreSlim Booting = new(Boots, Boots);

    public static T Boot<T>(Func<T> boot)
    {
        Booting.Wait();
        try
        {
            var deadline = DateTime.UtcNow.AddMinutes(10);
            while (FreePhysicalBytes() is long free && free < Headroom && DateTime.UtcNow < deadline)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                if (FreePhysicalBytes() >= Headroom)
                    break;
                Thread.Sleep(2000);
            }
            return boot();
        }
        finally
        {
            Booting.Release();
        }
    }

    // The machine's available physical memory, or null where it cannot be read (the gate then only limits the boots at once).
    private static long? FreePhysicalBytes()
    {
        if (OperatingSystem.IsWindows())
        {
            var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            return GlobalMemoryStatusEx(ref status) ? (long)status.AvailPhys : null;
        }
        if (File.Exists("/proc/meminfo"))
        {
            foreach (string line in File.ReadLines("/proc/meminfo"))
            {
                if (line.StartsWith("MemAvailable:", StringComparison.Ordinal)
                    && long.TryParse(line.AsSpan(13).Trim().TrimEnd("kB").Trim(), out long kb))
                    return kb * 1024;
            }
        }
        return null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}
