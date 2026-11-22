using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package CL1 on the converted MetaStation: every piece of clothing mapped on the station (on the floor, on the racks, in the
/// lockers, the wardrobes and the boxes) boots live, but the kinds the package leaves with a not-translated row (section 209: the
/// reactive armour, the plasmaman's and the ethereal's clothing, the chameleon kit, the polymorph belt, the perceptomatrix, the
/// horrific necktie, the mind-monkey helmet, the collar bomb, the skill capes, the pearl necklace, the fishing gloves, the wheelys);
/// the kinds that were inert before (the welding goggles, the cargo gauntlets, the collectable hats, the traffic cones, the top hat,
/// the animal masks, the winter boots, the surgical apron, the well-worn shirts) are found, the theatre's fake wizard robe (a costume spawner's random pick) is live, with no runtime from
/// the package's files.
/// </summary>
public class ClothingCL1MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public ClothingCL1MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    // the files the package touched (a runtime from one of them fails the test)
    private static readonly string[] Files =
    {
        "modules/clothing/", "strong_pull", "onwear_mood", "elements/series", "energy/laser.cs", "projectiles/pins.cs", "security_boxes",
        "alternate_appearance", "subtypes/pockets", "__DEFINES/inventory",
    };

    // the kinds left with a row (not-translated.md 209 and the earlier rows): their types stay inert
    private static readonly string[] LeftInert =
    {
        "reactive", "plasmaman", "ethereal", "chameleon", "polymorph", "perceptomatrix", "horrific", "mind_monkey", "collar_bomb",
        "necklace_pearl", "gloves_fishing", "wheelys", "wizrobe_paper", // the skill capes came with gaps SL1
        // the mining skull helmet (mining/equipment/explorer_gear.dm, its armour plate waits for the mining gear): a random roll that
        // shows on Meta since gaps MO1's live MODsuits draw rand() at init (their MODlink ids), not CL1's
        "helmet_skull",
    };

    private static IEnumerable<atom> Walk(IEnumerable<atom> things)
    {
        foreach (atom thing in things)
        {
            yield return thing;
            foreach (atom inner in Walk(Dm.each<atom>(thing.contents)))
                yield return inner;
        }
    }

    [MetaFact]
    public void Metas_clothing_boots_live()
    {
        using (_meta.World.Enter())
        {
            var counts = new Dictionary<string, int>();
            var inertKinds = new Dictionary<string, int>();
            int clothing = 0;
            for (int y = 1; y <= 255; y++)
            {
                for (int x = 1; x <= 255; x++)
                {
                    foreach (atom thing in Walk(locate(x, y, 1).dm_held))
                    {
                        if (thing is not obj_item_clothing)
                            continue;
                        clothing++;
                        string kind = thing switch
                        {
                            obj_item_clothing_glasses_welding => "welding goggles",
                            obj_item_clothing_gloves_cargo_gauntlet => "cargo gauntlets",
                            obj_item_clothing_head_collectable => "collectable hat",
                            obj_item_clothing_head_cone => "traffic cone",
                            obj_item_clothing_head_hats_tophat => "top hat",
                            obj_item_clothing_mask_animal => "animal mask",
                            obj_item_clothing_shoes_winterboots => "winter boots",
                            obj_item_clothing_suit_apron_surgical => "surgical apron",
                            obj_item_clothing_suit_costume_wellworn_shirt => "well-worn shirt",
                            obj_item_clothing_mask_gas_welding => "welding mask",
                            obj_item_clothing_suit_wizrobe_fake => "fake wizard robe",
                            _ => null,
                        };
                        if (kind != null)
                            counts[kind] = counts.GetValueOrDefault(kind) + 1;
                        if (InertSeam.IsInert(thing))
                        {
                            string name = thing.GetType().Name;
                            inertKinds[name] = inertKinds.GetValueOrDefault(name) + 1;
                        }
                    }
                }
            }
            var unexpected = inertKinds.Where(k => !LeftInert.Any(l => k.Key.Contains(l, StringComparison.Ordinal))).ToList();
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine($"Meta: {clothing} pieces of clothing; " + string.Join(", ", counts.OrderBy(c => c.Key).Select(c => c.Value + " " + c.Key))
                + $"; inert left by rows: {string.Join(", ", inertKinds.Where(k => !unexpected.Contains(k)).Select(k => k.Value + " " + k.Key))}"
                + $"; inert unexpected: {string.Join(", ", unexpected.Select(k => k.Value + " " + k.Key))}; CL1 runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.True(clothing >= 300, "too little clothing on Meta");
            foreach (string one in new[] { "welding goggles", "cargo gauntlets", "collectable hat", "traffic cone", "top hat", "animal mask", "winter boots", "surgical apron", "well-worn shirt" })
                Assert.True(counts.GetValueOrDefault(one) >= 1, "no " + one);
            // the theatre's fake wizard robe is one of the costume spawners' random picks (spawners/costume.dm:190), so whether Meta
            // has one moves with the random stream at init (gaps train 9: MO2's and SL1's live types drew it away): its type is live
            Assert.False(InertSeam.IsInertType(typeof(obj_item_clothing_suit_wizrobe_fake)), "the fake wizard robe is inert");
            Assert.Empty(unexpected);
            Assert.Empty(runtimes);
        }
    }
}
