using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M6;

/// <summary>
/// M6 slice 2, the wound engine: tg's pregen data and wound series (__DEFINES/wounds.dm), a bodypart's wound roll on damage
/// (receive_damage, check_wounding), forcing a wound of a type and severity (cause_wound_of_type_and_severity), a wounded leg's
/// limp and the wound's own status effect, full healing that leaves a scar (remove_wound, /datum/scar with tg's scar texts),
/// the self-check report (check_self_for_injuries, check_for_injuries), the health analyzer's trauma rows and the clothing
/// over a cut limb torn by the blow (take_damage_zone). The wound types' own behaviour is slice 3.
/// </summary>
public class WoundEngineTests
{
    private const int Second = 20;

    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(12, 9, seed: seed);
        // tg's strings/ files (the scars' texts), as the server mounts them
        if (TgClone.Root != null)
            TgStrings.Mount(DmWorldState.Of(r.World).Files, TgClone.Root);
        r.In(() =>
        {
            for (int x = 2; x <= 11; x++)
            {
                for (int y = 2; y <= 8; y++)
                    Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_floor_iron), Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating))));
            }
        });
        r.Step(Second);
        return r;
    }

    private static obj_item_bodypart Part(mob_living_carbon_human h, string zone) => (obj_item_bodypart)h.get_bodypart(zone);

    private static object PregenSeries(datum_wound w) => ((datum_wound_pregen_data)GLOB.all_wound_pregen_data[w.type]).wound_series;

    [Fact]
    public void ThePregenDataHasOneSingletonPerConcreteWoundAndTheSeriesCollectTheirSeverities()
    {
        var r = Room(6401);
        r.In(() =>
        {
            int concrete = 0;
            foreach (Type t in typeof(datum_wound_pregen_data).Assembly.GetTypes())
            {
                if (t == typeof(datum_wound_pregen_data) || !typeof(datum_wound_pregen_data).IsAssignableFrom(t) || DmTypes.PathOf(t) == null)
                    continue;
                if (!Dm.truthy(Dm.initial(t, (datum_wound_pregen_data p) => p.@abstract)))
                    concrete++;
            }
            Assert.Equal(concrete, length(GLOB.all_wound_pregen_data));
            foreach (Type path in Dm.each_cast<Type>(GLOB.all_wound_pregen_data))
                Assert.Same(path, ((datum_wound_pregen_data)GLOB.all_wound_pregen_data[path]).wound_path_to_generate);

            // the blunt, slash, burn and puncture series hold a wound of each of the three severities
            foreach (string series in new[] { WOUND_SERIES_BONE_BLUNT_BASIC, WOUND_SERIES_FLESH_SLASH_BLEED, WOUND_SERIES_FLESH_BURN_BASIC, WOUND_SERIES_FLESH_PUNCTURE_BLEED })
            {
                var severities = (DmList)GLOB.wound_series_collections[series];
                foreach (float severity in new[] { WOUND_SEVERITY_MODERATE, WOUND_SEVERITY_SEVERE, WOUND_SEVERITY_CRITICAL })
                    Assert.True(length(severities[Dm.text("[]", severity)]) > 0, series + " has a severity " + severity + " wound");
            }
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AHardSharpBlowRollsASlashWoundOnTheLimb()
    {
        var r = Room(6402);
        var victim = r.Dummy(6, 5);
        r.Step(Second);
        r.In(() =>
        {
            obj_item_bodypart arm = Part(victim, BODY_ZONE_L_ARM);
            Waiting.NoSleep(arm.receive_damage(brute: 20, wound_bonus: 100, sharpness: SHARP_EDGED));
            Assert.NotEmpty(Dm.each_cast<datum_wound>(arm.wounds));
            datum_wound wound = Dm.each_cast<datum_wound>(arm.wounds).Single();
            Assert.Equal(WOUND_SERIES_FLESH_SLASH_BLEED, PregenSeries(wound));
            Assert.Same(victim, wound.victim);
            Assert.Same(arm, wound.limb);
            Assert.True(Dm.@in(wound, victim.all_wounds), "the carbon tracks the wound");
            Assert.True(Dm.@in(arm, (DmList)victim.get_wounded_bodyparts()), "the arm is a wounded bodypart");
            Assert.False(string.IsNullOrEmpty(wound.wound_source as string), "the wound knows its source");
            if (Dm.truthy(Dm.bitand(wound.wound_flags, MANGLES_EXTERIOR)))
                Assert.True(Dm.truthy(Dm.bitand(arm.mangled_state, BODYPART_MANGLED_EXTERIOR)), "a mangling wound mangles the limb");
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AWeakBlowAndACantWoundBlowRollNoWound()
    {
        var r = Room(6403);
        var victim = r.Dummy(6, 5);
        r.Step(Second);
        r.In(() =>
        {
            obj_item_bodypart chest = Part(victim, BODY_ZONE_CHEST);
            Waiting.NoSleep(chest.receive_damage(brute: 4, sharpness: SHARP_EDGED)); // under WOUND_MINIMUM_DAMAGE
            Waiting.NoSleep(chest.receive_damage(brute: 30, wound_bonus: CANT_WOUND, sharpness: SHARP_EDGED));
            Assert.Empty(Dm.each_cast<datum_wound>(chest.wounds));
            Assert.Equal(0f, LAZYLEN(victim.all_wounds));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASevereBoneWoundOnALegLimpsAndHasItsStatusEffect()
    {
        var r = Room(6404);
        var victim = r.Dummy(6, 5);
        r.Step(Second);
        datum_wound wound = r.In(() =>
            (datum_wound)Waiting.NoSleep(victim.cause_wound_of_type_and_severity(WOUND_BLUNT, Part(victim, BODY_ZONE_R_LEG), WOUND_SEVERITY_SEVERE)));
        r.Step(1);
        r.In(() =>
        {
            Assert.NotNull(wound);
            Assert.Equal((float)WOUND_SEVERITY_SEVERE, wound.severity);
            Assert.Equal(WOUND_SERIES_BONE_BLUNT_BASIC, PregenSeries(wound));
            Assert.True(Dm.num(wound.limp_slowdown) > 0, "a severe bone wound slows the leg");
            var limp = victim.has_status_effect(typeof(datum_status_effect_limp)) as datum_status_effect_limp;
            Assert.NotNull(limp);
            Assert.True(limp.slowdown_right > 0, "the limp reads the right leg's wound");
            Assert.True(Dm.truthy(victim.has_status_effect(wound.status_effect_type)), "the wound's own status effect");
            // a second, weaker blunt wound on the same leg does not replace the severe one (no downgrades)
            Assert.Null(Waiting.NoSleep(victim.cause_wound_of_type_and_severity(WOUND_BLUNT, Part(victim, BODY_ZONE_R_LEG), WOUND_SEVERITY_MODERATE)));
            Assert.Single(Dm.each_cast<datum_wound>(Part(victim, BODY_ZONE_R_LEG).wounds));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void FullHealingRemovesTheWoundsLeavesAScarAndStopsTheLimp()
    {
        var r = Room(6405);
        var victim = r.Dummy(6, 5);
        r.Step(Second);
        r.In(() => Waiting.NoSleep(victim.cause_wound_of_type_and_severity(WOUND_BLUNT, Part(victim, BODY_ZONE_L_LEG), WOUND_SEVERITY_CRITICAL)));
        r.Step(1);
        r.In(() => Waiting.NoSleep(victim.fully_heal(HEAL_WOUNDS)));
        r.Step(1);
        r.In(() =>
        {
            obj_item_bodypart leg = Part(victim, BODY_ZONE_L_LEG);
            Assert.Equal(0f, LAZYLEN(victim.all_wounds));
            Assert.Equal(0f, LAZYLEN(leg.wounds));
            Assert.Null(victim.has_status_effect(typeof(datum_status_effect_limp)));
            datum_scar scar = Dm.each_cast<datum_scar>(leg.scars).Single();
            Assert.True(Dm.@in(scar, victim.all_scars));
            Assert.Equal((object)(float)WOUND_SEVERITY_CRITICAL, scar.severity);
            Assert.False(string.IsNullOrEmpty(scar.description as string), "tg's scar description");
            Assert.False(string.IsNullOrEmpty(scar.precise_location as string), "tg's scar location");
            Assert.Equal(5f, scar.visibility);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSelfCheckAndTheHealthAnalyzerReportTheWound()
    {
        var r = Room(6406);
        mob_living_carbon_human player = r.Join(6, 5, "Patient");
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        datum_wound wound = r.In(() =>
            (datum_wound)Waiting.NoSleep(player.cause_wound_of_type_and_severity(WOUND_BLUNT, Part(player, BODY_ZONE_L_ARM), WOUND_SEVERITY_MODERATE)));
        r.Step(1);
        r.In(() => player.check_self_for_injuries());
        r.Step(2); // SSchat delivers its queue when it fires
        string report = string.Join("\n", chat.Html);
        Assert.Contains("You check yourself for injuries.", report);
        Assert.Contains(r.In(() => (string)wound.get_self_check_description()), report);

        string scan = r.In(() => (string)healthscan(player, player, SCANNER_VERBOSE, SCANPOWER_BASIC, tochat: FALSE));
        Assert.Contains("Physical trauma: " + r.In(() => wound.name) + " (" + r.In(() => (string)wound.severity_text()) + ")", scan);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACuttingWoundTearsTheJumpsuitOverTheLimb()
    {
        var r = Room(6407);
        var victim = r.Dummy(6, 5);
        r.Step(Second);
        obj_item_clothing suit = r.In(() =>
        {
            var made = Dm.@new<obj_item_clothing_under_color_grey>(locate(6, 5, 1));
            Waiting.NoSleep(victim.equip_to_slot_if_possible(made, ITEM_SLOT_ICLOTHING));
            return (obj_item_clothing)made;
        });
        r.Step(1);
        r.In(() =>
        {
            Assert.Same(suit, victim.w_uniform);
            Assert.True(suit.limb_integrity > 0, "the jumpsuit tracks its limbs");
            float integrity = Dm.num(suit.atom_integrity);
            Waiting.NoSleep(Part(victim, BODY_ZONE_CHEST).receive_damage(brute: 20, wound_bonus: 100, sharpness: SHARP_EDGED));
            Assert.True(Dm.num(suit.damage_by_parts?[BODY_ZONE_CHEST]) > 0, "the chest of the jumpsuit took the blow: integrity " + Dm.num(suit.atom_integrity) + "/" + integrity + ", wounds " + LAZYLEN(Part(victim, BODY_ZONE_CHEST).wounds) + ", missing damage override " + InertSeam.MissingDamageOverride(suit) + ", covering " + length((DmList)victim.get_clothing_on_part(Part(victim, BODY_ZONE_CHEST))));
            Assert.True(Dm.num(suit.atom_integrity) < integrity, "a tenth of it reaches the jumpsuit's integrity");
        });
        r.AssertNoRuntimes();
    }
}
