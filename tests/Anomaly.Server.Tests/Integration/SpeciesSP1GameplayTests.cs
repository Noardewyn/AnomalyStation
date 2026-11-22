using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Gaps SP1 (D338): the lizard and the felinid through the real server. The server's config makes them selectable (tg's
/// ROUNDSTART_RACES); a lizard's parts reach another client as tg's bodypart overlays on his appearance (the lizard's greyscale limbs,
/// the snout, horns, frills and spines, the tail and its spines), which the client composes from tg's sprites over the human body's
/// layers; his wagging tail is tg's animated wagging state; a felinid's cat ears and tail come from the cat features sheet. The species
/// page's pictures and the lizard's side shots (an /icon recipe with its direction pick) are composed by the client's icon library.
/// </summary>
public class SpeciesSP1GameplayTests(ITestOutputHelper output)
{
    // The client's ReplicaTables (godot/scripts/Render, not in this test project): the replica's tables for the compositor.
    private sealed class Tables(ReplicaWorld replica) : IAppearanceSource
    {
        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => replica.TryGetAppearance(reference, out entry);
        public string ResolveString(TableRef reference) => replica.ResolveString(reference);
        public string ResolveResource(TableRef reference) => replica.ResolveResource(reference);
    }

    // Every (resource, icon state) of an appearance and its overlays and underlays, as the client walks them
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
    public void Another_player_sees_the_lizards_parts_and_his_wagging_tail_and_the_felinids_cat_ears()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 9851, TimeoutSteps = 600 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        RealHeadlessWorld.Player carol = w.Join("Carol");
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var carolMob = (mob_living_carbon_human)w.MobOf(carol);
        w.Run(10);
        using (w.World.Enter())
        {
            // the server's config: the human, the felinid and the lizard are selectable
            Assert.Equal(new[] { "felinid", "human", "lizard" }, Dm.each(GlobalProcs.get_selectable_species()).Select(Dm.str).OrderBy(s => s));
            bobMob.dna.features[FEATURE_MUTANT_COLOR] = "#22aa44";
            bobMob.dna.features[FEATURE_SNOUT] = "Round";
            bobMob.dna.features[FEATURE_HORNS] = "Ram";
            bobMob.dna.features[FEATURE_FRILLS] = "Aquatic";
            bobMob.dna.features[FEATURE_SPINES] = "Long + Membrane";
            bobMob.dna.features[FEATURE_TAIL_LIZARD] = "Smooth";
            bobMob.set_species(typeof(datum_species_lizard));
            carolMob.dna.features[FEATURE_EARS] = "Cat";
            carolMob.dna.features[FEATURE_TAIL_CAT] = "Cat";
            carolMob.set_species(typeof(datum_species_human_felinid));
        }
        w.Run(10);
        ReplicaWorld replica = alice.Session.Replica;
        ReplicaEntity seenBob = replica.Entities.Single(e => e.Id == bobMob.dm_id);
        List<(string Resource, string State)> sprites = Sprites(replica, seenBob.State.Appearance);
        output.WriteLine(string.Join("\n", sprites.Where(s => s.Resource.Contains("lizard", StringComparison.Ordinal)).Select(s => s.Resource + " " + s.State)));
        const string misc = "icons/mob/human/species/lizard/lizard_misc.dmi";
        Assert.Contains((misc, "m_snout_round_ADJ"), sprites);
        Assert.Contains((misc, "m_horns_ram_ADJ"), sprites);
        Assert.Contains((misc, "m_frills_aqua_ADJ"), sprites);
        Assert.Contains(("icons/mob/human/species/lizard/lizard_spines.dmi", "m_spines_longmeme_ADJ"), sprites);
        Assert.Contains(("icons/mob/human/species/lizard/lizard_tails.dmi", "m_tail_lizard_smooth_BEHIND"), sprites);
        Assert.Contains(sprites, s => s.Resource == "icons/mob/human/species/lizard/bodyparts.dmi" && s.State.StartsWith("lizard_chest", StringComparison.Ordinal));

        ReplicaEntity seenCarol = replica.Entities.Single(e => e.Id == carolMob.dm_id);
        List<(string Resource, string State)> cat = Sprites(replica, seenCarol.State.Appearance);
        Assert.Contains(("icons/mob/human/cat_features.dmi", "m_ears_cat_BEHIND"), cat);
        Assert.Contains(("icons/mob/human/cat_features.dmi", "m_tail_cat_default_BEHIND"), cat);

        // the lizard wags his tail: tg's animated wagging state reaches the other client
        using (w.World.Enter())
            Waiting.NoSleep(bobMob.emote("wag"));
        w.Run(10);
        seenBob = replica.Entities.Single(e => e.Id == bobMob.dm_id);
        List<(string Resource, string State)> wagging = Sprites(replica, seenBob.State.Appearance);
        Assert.Contains(("icons/mob/human/species/lizard/lizard_tails.dmi", "m_tail_lizard_wagging_smooth_BEHIND"), wagging);

        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root == null)
            return;
        var compositor = new AppearanceCompositor(new IconLibrary(new DirectoryIconFiles(root)));
        var tables = new Tables(replica);
        // the lizard is drawn from tg's sprites: his tail behind him from the north, and its wagging is animated
        Assert.True(compositor.TryCompose(tables, seenBob.State.Appearance, ByondDir.North, 0, out ComposedRaster north));
        Assert.False(compositor.IsBlank(tables, seenBob.State.Appearance, ByondDir.North));
        Assert.Contains(compositor.AnimatedStates(tables, seenBob.State.Appearance, ByondDir.South), s => s.Frames > 1);
        // the lizard's picture differs from a human's
        ReplicaEntity seenAlice = alice.Session.Replica.Entities.FirstOrDefault(e => e.Id == w.MobOf(alice).dm_id);
        if (seenAlice != null && compositor.TryCompose(tables, seenAlice.State.Appearance, ByondDir.South, 0, out ComposedRaster human)
            && compositor.TryCompose(tables, seenBob.State.Appearance, ByondDir.South, 0, out ComposedRaster lizard))
            Assert.False(human.Image.Pixels.AsSpan().SequenceEqual(lizard.Image.Pixels), "the lizard is drawn like the human");
    }

    [Fact]
    public void The_species_pages_pictures_and_the_lizards_side_shots_compose_from_tgs_sprites()
    {
        using var rig = new Bots.BotServerRig(strict: true);
        (string Resource, string State) snout, roundSnout, body;
        var species = new Dictionary<string, (string Resource, string State)>();
        (string, string) Of(DmList icons, string choice) => (Dm.str(((DmList)icons[choice])[1]), ((DmList)icons[choice])[2] as string);
        using (rig.World.Enter())
        {
            var prefs = Dm.@new<datum_preferences>(Dm.@new<datum_client_interface>()); // a mock client, as randomize_human_appearance's
            DmList pictures = Waiting.NoSleep(prefs.species_preview_icons());
            foreach (object key in Dm.each(pictures))
                species[Dm.str(key)] = Of(pictures, Dm.str(key));
            DmList Icons(Type type) => (DmList)((DmList)((datum_preference)GlobalProcs.GLOB.preference_entries[type]).compile_constant_data())["icons"];
            DmList snouts = Icons(typeof(datum_preference_choiced_species_feature_lizard_snout));
            snout = Of(snouts, "Sharp");
            roundSnout = Of(snouts, "Round");
            body = Of(Icons(typeof(datum_preference_choiced_species_feature_lizard_body_markings)), "Dark Tiger Body");
        }
        Assert.Equal(new[] { "Felinid", "Human", "Lizardperson" }, species.Keys.OrderBy(k => k));
        Assert.Contains(",d,4", snout.Resource, StringComparison.Ordinal); // the side shot faces east
        foreach (string recipe in species.Values.Select(v => v.Resource).Append(snout.Resource).Append(body.Resource))
            Assert.True(recipe.Length <= Limits.MaxUiStringBytes, recipe.Length + " bytes");

        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root == null)
            return;
        var library = new IconLibrary(new DirectoryIconFiles(root));
        Raster Picture((string Resource, string State) picture, out int width, out int height)
        {
            Assert.True(library.TryGetIcon(picture.Resource, out IIcon icon), "no icon for " + picture.Resource);
            Assert.True(icon.TryGetShown(picture.State ?? "", out IIconState state), "no state " + picture.State);
            width = state.Width;
            height = state.Height;
            return state.Get(0, 0);
        }
        int Opaque((string Resource, string State) picture, out int width, out int height)
        {
            Raster raster = Picture(picture, out width, out height);
            int opaque = 0;
            for (int i = 3; i < raster.Pixels.Length; i += 4)
                if (raster.Pixels[i] != 0)
                    opaque++;
            return opaque;
        }
        foreach ((string key, (string Resource, string State) picture) in species)
        {
            int filled = Opaque(picture, out int w, out int h);
            Assert.Equal((64, 64), (w, h)); // tg's 64 px species pictures
            Assert.True(filled > 64 * 16, key + ": " + filled);
        }
        Assert.True(Opaque(snout, out int sw, out int sh) > 0);
        Assert.Equal((32, 32), (sw, sh));
        // the sharp snout and the round one are drawn differently
        Assert.False(Picture(snout, out _, out _).Pixels.AsSpan().SequenceEqual(Picture(roundSnout, out _, out _).Pixels));
        Assert.True(Opaque(body, out _, out _) > 0);
    }
}
