using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Examine;

/// <summary>
/// The remaining object examines (D291, package X3) in a booted toy room: clothing (shredding, zone damage, pockets, the armour tag
/// and its readout, the tags), jumpsuits' sensors, shoelaces, the winter coat's zip, limbs and heads, the medical HUD's sensor line,
/// the stomach healing its cut, the microwave, a vehicle, the hide and thermoplastic stacks, a HUD alert, the fishing rod, a fish and
/// the mobs' dead and damage lines; each in the state its condition reads and not in the state it does not.
/// </summary>
public class RemainingExamineTests
{
    private const int Second = 20;

    private sealed record Rig(AudioRoom R, mob_living_carbon_human Bob);

    private static Rig Room(ulong seed)
    {
        var r = new AudioRoom(12, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(2, 2, "Bob");
        r.Step(Second);
        return new Rig(r, bob);
    }

    private static T Make<T>(Rig g, Action<T> setup = null, int x = 6, int y = 5) where T : atom => g.R.In(() =>
    {
        var thing = Dm.@new<T>(locate(x, y, 1));
        setup?.Invoke(thing);
        return thing;
    });

    private static string Look(Rig g, atom thing) => g.R.In(() => string.Join("\n", Dm.each(Waiting.NoSleep(thing.examine(g.Bob))).Select(Dm.str)));

    private static string LookMore(Rig g, atom thing) => g.R.In(() => string.Join("\n", Dm.each(Waiting.NoSleep(thing.examine_more(g.Bob))).Select(Dm.str)));

    private static void Has(string text, params string[] lines)
    {
        foreach (string line in lines)
            Assert.True(text.Contains(line, StringComparison.Ordinal), $"missing '{line}' in: {text}");
    }

    private static void Lacks(string text, params string[] lines)
    {
        foreach (string line in lines)
            Assert.DoesNotContain(line, text, StringComparison.Ordinal);
    }

    [Fact]
    public void ClothingShowsItsDamagePocketsArmourTagAndTags()
    {
        Rig g = Room(8901);
        // a helmet: the armour tag link and its readout (clothing.dm:362, :408)
        var helmet = Make<obj_item_clothing_head_helmet>(g);
        string text = Look(g, helmet);
        Has(text, "listing its protection classes.", "clothing.");
        Lacks(text, "storage opens", "crisp and pristine", "shredded");
        string link = System.Text.RegularExpressions.Regex.Match(text, @"href='byond://\?([^']+)'>tag").Groups[1].Value;
        Assert.Contains("list_armor=1", link, StringComparison.Ordinal);
        var chat = new List<string>();
        PresentationSinks.Of(g.R.World).Messages = new Feed(chat);
        g.R.Command(() => ClientSeam.Topic(g.Bob.client, link));
        g.R.Step(2);
        Has(string.Join("\n", chat), "PROTECTION CLASSES", "ARMOR (I-X)", "MELEE ", "It covers the wearer's head.");

        // pockets, laundering, zone damage, then shredded (clothing.dm:330-366)
        g.R.In(() =>
        {
            Waiting.NoSleep(helmet.create_storage(max_slots: 2));
            helmet.is_laundered = TRUE;
            helmet.limb_integrity = 100;
            helmet.damage_by_parts = Dm.list(Dm.kv(BODY_ZONE_HEAD, 65));
            return 0;
        });
        Has(Look(g, helmet), "storage opens when clicked.", "can store 2 item", "looks crisp and pristine.", "The head is heavily shredded!");
        g.R.In(() => helmet.damaged_clothes = CLOTHING_SHREDDED);
        text = Look(g, helmet);
        Has(text, "completely shredded and require");
        Lacks(text, "storage opens", "heavily shredded");

        // the tags: a space suit is pressure-proof and insulated (clothing.dm:368)
        var suit = Make<obj_item_clothing_suit_space_eva>(g, x: 7);
        Has(Look(g, suit), "pressure-proof", "thermally insulated", "Protects the wearer from");
        var gloves = Make<obj_item_clothing_gloves_color_yellow>(g, x: 8);
        Lacks(Look(g, gloves), "pressure-proof", "thermally insulated");
    }

    [Fact]
    public void JumpsuitsShoesAndCoatsShowTheirOwnLinesAndTheMedicalHudReadsTheSensors()
    {
        Rig g = Room(8902);
        var suit = Make<obj_item_clothing_under_color_grey>(g);
        string text = Look(g, suit);
        Has(text, "Alt-click on", "to wear it casually.");
        Lacks(text, "shorted out");
        g.R.In(() => suit.set_sensor_mode(SENSOR_COORDS));
        Has(Look(g, suit), "Its vital tracker and tracking beacon appear to be enabled.");
        g.R.In(() => suit.has_sensor = NO_SENSORS);
        Has(Look(g, suit), "It isn't equipped with medical sensors.");
        g.R.In(() => suit.has_sensor = BROKEN_SENSORS);
        Has(Look(g, suit), "The medical sensors appear to be shorted out.");

        // the medical HUD's line: only a broken sensor speaks (human/examine.dm:2, _under.dm:334)
        var wearer = g.R.In(() => Dm.@new<mob_living_carbon_human>(locate(4, 4, 1)));
        g.R.In(() => Waiting.NoSleep(wearer.equip_to_slot_if_possible(suit, ITEM_SLOT_ICLOTHING)));
        string hud = g.R.In(() => string.Join("\n", Dm.each(wearer.get_medhud_examine_info(g.Bob, null)).Select(Dm.str)));
        Has(hud, "Sensor Status: ", "Non-Functional: Repair with cable coil");
        g.R.In(() => suit.has_sensor = HAS_SENSORS);
        hud = g.R.In(() => string.Join("\n", Dm.each(wearer.get_medhud_examine_info(g.Bob, null)).Select(Dm.str)));
        Lacks(hud, "Sensor Status");

        // shoes: the laces only on someone's feet (_shoes.dm:86)
        var shoes = Make<obj_item_clothing_shoes_sneakers_black>(g, x: 7);
        g.R.In(() => shoes.tied = SHOES_UNTIED);
        Lacks(Look(g, shoes), "The shoelaces are");
        g.R.In(() => Waiting.NoSleep(wearer.equip_to_slot_if_possible(shoes, ITEM_SLOT_FEET)));
        g.R.In(() => shoes.tied = SHOES_UNTIED);
        Has(Look(g, shoes), "The shoelaces are untied.");
        g.R.In(() => shoes.tied = SHOES_KNOTTED);
        Has(Look(g, shoes), "The shoelaces are all knotted together.");

        // the winter coat's zip (wintercoats.dm:48)
        var coat = Make<obj_item_clothing_suit_hooded_wintercoat>(g, x: 8);
        Has(Look(g, coat), "<b>Alt-click</b> to zip.");
        g.R.In(() => coat.zipped = TRUE);
        Has(Look(g, coat), "<b>Alt-click</b> to unzip.");
        g.R.In(() => coat.can_altclick_zip = FALSE);
        Lacks(Look(g, coat), "Alt-click");
    }

    [Fact]
    public void LimbsHeadsAndStomachsShowTheirState()
    {
        Rig g = Room(8903);
        var arm = Make<obj_item_bodypart_arm_left>(g);
        Lacks(Look(g, arm), "bruising", "burns.");
        g.R.In(() => { arm.brute_dam = 40; arm.burn_dam = 5; return 0; });
        Has(Look(g, arm), "This limb has severe bruising.", "This limb has minor burns.");
        Assert.NotNull(LookMore(g, arm));

        // a loose head with nothing in it (head.dm:119)
        var head = Make<obj_item_bodypart_head>(g, x: 7);
        Has(Look(g, head), "The brain has been removed from", "'s eyes have been removed.", "'s ears have been removed.", "'s tongue has been removed.");
        g.R.In(() => head.show_organs_on_examine = FALSE);
        Lacks(Look(g, head), "has been removed");

        // the stomach's cut heals with it (_stomach.dm:460)
        var stomach = Make<obj_item_organ_stomach>(g, x: 8);
        g.R.In(() => { stomach.damage = 20; stomach.prev_damage = 20; stomach.cut_open_damage = 10; return 0; });
        g.R.In(() => Waiting.NoSleep(stomach.apply_organ_damage(-6)));
        Assert.Equal(4f, g.R.In(() => stomach.cut_open_damage));
        g.R.In(() => Waiting.NoSleep(stomach.apply_organ_damage(5)));
        Assert.Equal(4f, g.R.In(() => stomach.cut_open_damage));
    }

    [Fact]
    public void MachinesVehiclesStacksAlertsAndFishingThingsShowTheirLines()
    {
        Rig g = Room(8904);
        // the microwave next to Bob: its hints, contents and status display (microwave.dm:171)
        var microwave = Make<obj_machinery_microwave>(g, x: 3, y: 2);
        string text = Look(g, microwave);
        Has(text, "<b>Right-click</b> to start cooking cycle.", "is empty.", "The status display reads:", "- Capacity: <b>10</b> items.", "- Power: <b>2500W</b>.");
        Lacks(text, "Wave™", "too far away");
        var far = Make<obj_machinery_microwave>(g, x: 10, y: 7);
        Has(Look(g, far), "You're too far away to examine");

        var vehicle = Make<obj_vehicle>(g);
        Lacks(Look(g, vehicle), "damaged", "falling apart");
        g.R.In(() => vehicle.atom_integrity = 120);
        Has(Look(g, vehicle), "It appears heavily damaged.");

        var hide = Make<obj_item_stack_sheet_animalhide_goliath_hide>(g, x: 7);
        Has(Look(g, hide), "You can remove the hair with any sharp object.");
        var plastic = Make<obj_item_stack_thermoplastic>(g, x: 8);
        Lacks(Look(g, plastic), "throwing weapon");
        g.R.In(() => plastic.throwforce = 10);
        Has(Look(g, plastic), "Those could work as a fairly decent throwing weapon.");

        var alert = g.R.In(() => { var a = Dm.@new<atom_movable_screen_alert>(); a.name = "Choking"; a.desc = "No air."; return a; });
        Assert.Equal("<span class='boldnotice'>Choking</span>\n<span class='info'>No air.</span>", Look(g, alert));

        var rod = Make<obj_item_fishing_rod>(g, x: 9);
        Has(Look(g, rod), "It doesn't have a bait attached to it.");
        Lacks(Look(g, rod), "Thanks to your fishing skills");
        var fish = Make<obj_item_fish>(g, x: 9, y: 6);
        Lacks(Look(g, fish), "bitten");
        g.R.In(() => { fish.bites_amount = 1; fish.catcher_name = "Bob"; fish.catch_date = "today"; return 0; });
        Has(Look(g, fish), "been bitten by someone.", "Caught by Bob on today.");
    }

    [Fact]
    public void MobsShowTheirDeadAndDamageLines()
    {
        Rig g = Room(8905);
        var drone = Make<mob_living_basic_drone_classic>(g);
        string text = Look(g, drone);
        Has(text, "Its status LED is blinking at a steady rate.");
        Lacks(text, "screws", "ERROR -- OFFLINE");
        g.R.In(() => { drone.health = drone.maxHealth * 0.2f; drone.stat = DEAD; return 0; });
        Has(Look(g, drone), "Its screws are very loose!", "ERROR -- OFFLINE");

        // the basic mob's and the simple animal's dead line (basic.dm:223, simple_animal.dm:222)
        var critter = Make<mob_living_basic>(g, x: 8);
        Lacks(Look(g, critter), "Upon closer examination");
        g.R.In(() => critter.stat = DEAD);
        Has(Look(g, critter), "Upon closer examination, ", "to be dead.");
        var animal = Make<mob_living_simple_animal>(g, x: 9);
        g.R.In(() => animal.stat = DEAD);
        Has(Look(g, animal), "Upon closer examination, ", "to be dead.");

        var construct = Make<mob_living_basic_construct_proteon>(g, x: 7);
        Lacks(Look(g, construct), "dented");
        g.R.In(() => construct.health = construct.maxHealth * 0.75f);
        Has(Look(g, construct), "slightly dented.");
        Assert.StartsWith("<span class='cult'>", g.R.In(() => Dm.str(construct.get_examine_name(g.Bob))));
    }

    private sealed class Feed(List<string> chat) : IMessageSink
    {
        void IMessageSink.Chat(object target, DmList message) => chat.Add(Dm.str(message?["html"]));

        public void Output(object target, string text) { }

        public void Balloon(object viewer, object atom, string text) { }
    }
}
