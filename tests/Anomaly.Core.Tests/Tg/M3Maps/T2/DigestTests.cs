using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit;

namespace Anomaly.Core.Tests.Tg.M3Maps.T2;

/// <summary>
/// M3 T2: the map digest (plan.md: types, positions, appearance and density of every cell's atoms, MetaMaps.Digest) and the
/// state hash of small loaded maps: two loads of one seed are identical, and a one-object edit of a fixture changes the digest.
/// Fixtures: digesta, digestb (digesta with the crowbar's pixel_x edited), digestc (digesta with one more landmark).
/// </summary>
public class DigestTests
{
    private static (ulong Hash, int Atoms) DigestOf(string fixture, ulong seed = 1)
    {
        World world = T2Maps.Boot(fixture, out TgTestWorld.Log log, seed);
        Assert.Empty(log.Runtimes);
        return MetaMaps.Digest(world);
    }

    [Fact]
    public void Two_loads_of_one_seed_have_the_same_digest()
    {
        Assert.Equal(DigestOf("digesta"), DigestOf("digesta"));
    }

    [Fact]
    public void The_digest_does_not_depend_on_the_seed_because_a_map_load_draws_no_random_number()
    {
        Assert.Equal(DigestOf("digesta", 1), DigestOf("digesta", 77));
    }

    [Fact]
    public void An_edit_of_one_object_changes_the_digest_and_one_more_object_changes_it_and_the_count()
    {
        (ulong hash, int atoms) = DigestOf("digesta");
        (ulong editedHash, int editedAtoms) = DigestOf("digestb");
        Assert.Equal(atoms, editedAtoms);
        Assert.NotEqual(hash, editedHash);
        (ulong addedHash, int addedAtoms) = DigestOf("digestc");
        Assert.NotEqual(hash, addedHash);
        Assert.Equal(atoms + 1, addedAtoms);
    }

    [Fact]
    public void Two_loads_of_one_seed_have_the_same_state_hash_at_boot_and_after_ticks()
    {
        World a = T2Maps.Boot("digesta");
        World b = T2Maps.Boot("digesta");
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        a.Step(30);
        b.Step(30);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        // and the edited map is another world
        World c = T2Maps.Boot("digestb");
        c.Step(30);
        Assert.NotEqual(a.ComputeStateHash(), c.ComputeStateHash());
    }
}
