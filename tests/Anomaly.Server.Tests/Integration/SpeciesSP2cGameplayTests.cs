using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Gaps SP2c (D349): the jelly family and the cerulean through the real server. Another player sees a cerulean's fish tail from tg's cerulean
/// tail sheet and a slimeperson's jelly limbs; the cerulean is in the server's roundstart list; a slimeperson splits and swaps bodies through
/// tg's body swap window (SlimeBodySwapper) over the wire.
/// </summary>
public class SpeciesSP2cGameplayTests(ITestOutputHelper output)
{
    private static List<(string Resource, string State)> Sprites(ReplicaWorld replica, TableRef root)
    {
        var found = new List<(string, string)>();
        var seen = new HashSet<TableRef>();
        void Walk(TableRef r)
        {
            if (!seen.Add(r) || !replica.TryGetAppearance(r, out AppearanceEntry e))
                return;
            found.Add((replica.ResolveResource(e.Icon) ?? "", replica.ResolveString(e.IconState) ?? ""));
            foreach (TableRef o in e.Underlays)
                Walk(o);
            foreach (TableRef o in e.Overlays)
                Walk(o);
        }
        Walk(root);
        return found;
    }

    [Fact]
    public void Another_player_sees_the_ceruleans_tail_and_the_slimepersons_jelly_limbs_and_the_cerulean_is_selectable()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 9861, TimeoutSteps = 600 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        RealHeadlessWorld.Player carol = w.Join("Carol");
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var carolMob = (mob_living_carbon_human)w.MobOf(carol);
        w.Run(10);
        using (w.World.Enter())
        {
            Assert.Contains("cerulean", Dm.each(GlobalProcs.get_selectable_species()).Select(Dm.str));
            bobMob.set_species(typeof(datum_species_human_cerulean));
            carolMob.set_species(typeof(datum_species_jelly_slime));
        }
        w.Run(10);
        ReplicaWorld replica = alice.Session.Replica;
        List<(string Resource, string State)> bobSprites = Sprites(replica, replica.Entities.Single(e => e.Id == bobMob.dm_id).State.Appearance);
        output.WriteLine(string.Join("\n", bobSprites.Where(s => s.Resource.Contains("cerulean", StringComparison.Ordinal)).Select(s => s.Resource + " " + s.State)));
        Assert.Contains(bobSprites, s => s.Resource == "icons/mob/human/species/cerulean/cerulean_tails.dmi");
        List<(string Resource, string State)> carolSprites = Sprites(replica, replica.Entities.Single(e => e.Id == carolMob.dm_id).State.Appearance);
        output.WriteLine(string.Join("\n", carolSprites.Select(s => s.Resource + " " + s.State)));
        Assert.Contains(carolSprites, s => s.State.StartsWith("slime_", StringComparison.Ordinal) || s.State.StartsWith("jelly_", StringComparison.Ordinal));
    }

    [Fact]
    public void A_slimeperson_splits_and_swaps_bodies_through_the_body_swap_window()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 9862, TimeoutSteps = 2000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var mob = (mob_living_carbon_human)w.MobOf(alice);
        w.Run(10);
        using (w.World.Enter())
        {
            mob.set_species(typeof(datum_species_jelly_slime));
            mob.set_blood_volume(BLOOD_VOLUME_SLIME_SPLIT + 10);
            Waiting.NoSleep(((datum_species_jelly_slime)mob.dna.species).slime_split.make_dupe());
        }
        w.Run(10);
        var spare = (mob_living_carbon_human)w.MobOf(alice);
        Assert.NotSame(mob, spare);
        ReplicaWorld replica = alice.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters),
            SendClose = window => alice.Session.UiClose(window),
        };
        using (w.World.Enter())
            Waiting.NoSleep(((datum_species_jelly_slime)spare.dna.species).swap_body.Activate());
        w.Run(10);
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        TguiWindow window = windows.Open.Single(x => x.Interface == "SlimeBodySwapper");
        UiNode swap = window.Content.Walk().Single(n => n.Kind == UiKind.Button && n.Text == "Swap");
        swap.OnClick();
        w.Run(10);
        Assert.Same(mob, w.MobOf(alice));
    }
}
