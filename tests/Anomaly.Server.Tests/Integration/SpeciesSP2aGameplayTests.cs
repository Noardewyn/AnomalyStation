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
/// Gaps SP2a (D349): the moth and the plasmaman through the real server. Another client sees the moth's wings, antennae, markings and
/// limbs as tg's bodypart overlays on his appearance, and the plasmaman's bone limbs with his envirosuit and helmet over them; the
/// moth's Character Setup features are choices with pictures the client's icon library composes; a plasmaman without his suit burns
/// in the oxygen the server's room is filled with, and the burning reaches the other client as the fire overlay.
/// </summary>
public class SpeciesSP2aGameplayTests(ITestOutputHelper output)
{
    private sealed class Tables(ReplicaWorld replica) : IAppearanceSource
    {
        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => replica.TryGetAppearance(reference, out entry);
        public string ResolveString(TableRef reference) => replica.ResolveString(reference);
        public string ResolveResource(TableRef reference) => replica.ResolveResource(reference);
    }

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
    public void Another_player_sees_the_moths_parts_and_the_plasmamans_bones_with_his_envirosuit_and_helmet()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 9961, TimeoutSteps = 600 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        RealHeadlessWorld.Player carol = w.Join("Carol");
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var carolMob = (mob_living_carbon_human)w.MobOf(carol);
        w.Run(10);
        using (w.World.Enter())
        {
            bobMob.dna.features[FEATURE_MOTH_WINGS] = "Monarch";
            bobMob.dna.features[FEATURE_MOTH_ANTENNAE] = "Royal";
            bobMob.dna.features[FEATURE_MOTH_MARKINGS] = "Reddish";
            bobMob.set_species(typeof(datum_species_moth));
            carolMob.set_species(typeof(datum_species_plasmaman));
            foreach (obj_item item in Dm.each_cast<obj_item>(carolMob.get_equipped_items(1)).ToList())
                GlobalProcs.qdel(item);
            Waiting.NoSleep(carolMob.equipOutfit(typeof(datum_outfit_plasmaman_security)));
        }
        w.Run(20);
        ReplicaWorld replica = alice.Session.Replica;
        ReplicaEntity seenBob = replica.Entities.Single(e => e.Id == bobMob.dm_id);
        List<(string Resource, string State)> moth = Sprites(replica, seenBob.State.Appearance);
        output.WriteLine(string.Join("\n", moth.Where(s => s.Resource.Contains("moth", StringComparison.Ordinal)).Select(s => s.Resource + " " + s.State)));
        Assert.Contains(moth, s => s.Resource == "icons/mob/human/species/moth/bodyparts.dmi" && s.State.StartsWith("moth_chest", StringComparison.Ordinal));
        Assert.Contains(moth, s => s.Resource == "icons/mob/human/species/moth/moth_wings.dmi" && s.State.Contains("monarch", StringComparison.Ordinal));
        Assert.Contains(moth, s => s.Resource == "icons/mob/human/species/moth/moth_antennae.dmi" && s.State.Contains("royal", StringComparison.Ordinal));
        Assert.Contains(moth, s => s.Resource.Contains("moth_markings", StringComparison.Ordinal) && s.State.Contains("reddish", StringComparison.Ordinal));

        ReplicaEntity seenCarol = replica.Entities.Single(e => e.Id == carolMob.dm_id);
        List<(string Resource, string State)> plasma = Sprites(replica, seenCarol.State.Appearance);
        output.WriteLine(string.Join("\n", plasma.Select(s => s.Resource + " " + s.State)));
        Assert.Contains(plasma, s => s.Resource == "icons/mob/human/species/plasmaman/bodyparts.dmi" && s.State.StartsWith("plasmaman_chest", StringComparison.Ordinal));
        Assert.Contains(plasma, s => s.Resource == "icons/mob/clothing/under/plasmaman.dmi" && s.State.Contains("security_envirosuit", StringComparison.Ordinal));
        Assert.Contains(plasma, s => s.Resource == "icons/mob/clothing/head/plasmaman_head.dmi" && s.State.Contains("security_envirohelm", StringComparison.Ordinal));

        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root == null)
            return;
        var compositor = new AppearanceCompositor(new IconLibrary(new DirectoryIconFiles(root)));
        var tables = new Tables(replica);
        Assert.True(compositor.TryCompose(tables, seenBob.State.Appearance, ByondDir.South, 0, out ComposedRaster mothPicture));
        Assert.True(compositor.TryCompose(tables, seenCarol.State.Appearance, ByondDir.South, 0, out ComposedRaster plasmaPicture));
        Assert.False(mothPicture.Image.Pixels.AsSpan().SequenceEqual(plasmaPicture.Image.Pixels), "the moth is drawn like the plasmaman");
    }

    [Fact]
    public void A_plasmaman_without_his_suit_burns_in_oxygen_and_the_other_client_sees_the_flames()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 9962, TimeoutSteps = 900 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        w.Run(10);
        using (w.World.Enter())
        {
            foreach (turf_open open in Dm.each<object>(Builtins.range(8, bobMob)).OfType<turf_open>().ToList())
                open.air = Anomaly.Core.Tg.GlobalProcs.SSair.parse_gas_string("o2=21;n2=79;TEMP=293.15") as datum_gas_mixture;
            bobMob.set_species(typeof(datum_species_plasmaman));
            foreach (obj_item item in Dm.each_cast<obj_item>(bobMob.get_equipped_items(1)).ToList())
                GlobalProcs.qdel(item);
        }
        w.Run(80);
        using (w.World.Enter())
            Assert.True(Dm.truthy(bobMob.on_fire), "a plasmaman in oxygen without a suit burns");
        w.Run(10);
        ReplicaEntity seen = alice.Session.Replica.Entities.Single(e => e.Id == bobMob.dm_id);
        List<(string Resource, string State)> sprites = Sprites(alice.Session.Replica, seen.State.Appearance);
        output.WriteLine(string.Join("\n", sprites.Select(s => s.Resource + " " + s.State)));
        Assert.Contains(sprites, s => s.Resource.Contains("OnFire", StringComparison.OrdinalIgnoreCase) || s.State.Contains("fire", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_moths_character_setup_features_are_choices_with_pictures_from_tgs_sprites()
    {
        using var rig = new Bots.BotServerRig(strict: true);
        var pictures = new Dictionary<string, (string Resource, string State)>();
        using (rig.World.Enter())
        {
            foreach ((string name, Type type, string choice) in new[]
            {
                ("antennae", typeof(datum_preference_choiced_species_feature_moth_antennae), "Royal"),
                ("markings", typeof(datum_preference_choiced_species_feature_moth_markings), "Reddish"),
                ("wings", typeof(datum_preference_choiced_species_feature_moth_wings), "Monarch"),
            })
            {
                var pref = (datum_preference)GlobalProcs.GLOB.preference_entries[type];
                var data = (DmList)pref.compile_constant_data();
                var icons = (DmList)data["icons"];
                Assert.NotNull(icons[choice]);
                var entry = (DmList)icons[choice];
                pictures[name] = (Dm.str(entry[1]), entry[2] as string);
                Assert.True(pictures[name].Resource.Length <= Limits.MaxUiStringBytes, name);
            }
        }

        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root == null)
            return;
        var library = new IconLibrary(new DirectoryIconFiles(root));
        foreach ((string name, (string Resource, string State) picture) in pictures)
        {
            Assert.True(library.TryGetIcon(picture.Resource, out IIcon icon), "no icon for " + name);
            Assert.True(icon.TryGetShown(picture.State ?? "", out IIconState state), "no state for " + name + " " + picture.State);
            Raster raster = state.Get(0, 0);
            int opaque = 0;
            for (int i = 3; i < raster.Pixels.Length; i += 4)
                if (raster.Pixels[i] != 0)
                    opaque++;
            Assert.True(opaque > 0, name + " is blank");
        }
    }
}
