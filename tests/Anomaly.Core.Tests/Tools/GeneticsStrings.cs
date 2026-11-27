using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;

namespace Anomaly.Core.Tests.Tools;

/// <summary>
/// Gaps P3: the world files every world that initializes SSatoms needs. SSatoms' setupGenetics makes one of every mutation, and the
/// speech mutations read tg's strings files in their New (<see cref="TgStrings.GeneticsFiles"/>), as the real host mounts them.
/// Test worlds that build their own start (not through <see cref="TgStrings.Mount(IDmFileSystem, string)"/>) mount these, and the
/// dream words a sleeping mob dreams from (<see cref="TgStrings.DreamFiles"/>).
/// </summary>
internal static class GeneticsStrings
{
    /// <summary>Copies the genetics strings files and the dream words from the tgstation clone into <paramref name="world"/>'s file seam (none without the clone).</summary>
    public static void Mount(World world)
    {
        if (TgClone.Root != null)
            TgStrings.Mount(DmWorldState.Of(world).Files, TgClone.Root, TgStrings.GeneticsFiles.AddRange(TgStrings.DreamFiles));
    }
}
