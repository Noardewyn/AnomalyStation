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
/// The object examines (D291, package X2) in a booted toy room: the lines tg's examine of a chair, a door and a windoor assembly, a
/// girder, a grille, a window and a reinforced one, a railing, plastic flaps, a false wall, a rack, a table, a wall and a reinforced
/// one, the iron, plating and engine floors, a machine circuit board, an airlock painter, a wall frame, a flamethrower and a
/// maintenance spawner adds, in the states its conditions read, and not in the states they do not; the structure base's damage
/// lines; the weapon description's link and readout, the lore element, the storage can-hold lines, the ID card's appraisal
/// and the organs' and the brain's lines.
/// </summary>
public class ObjectExamineTests
{
    private const int Second = 20;

    private sealed record Rig(AudioRoom R, mob_living_carbon_human Bob);

    private static Rig Room(ulong seed)
    {
        var r = new AudioRoom(12, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(2, 2, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return new Rig(r, bob);
    }

    private static T Make<T>(Rig g, Action<T> setup = null, int x = 6, int y = 5) where T : atom => g.R.In(() =>
    {
        var thing = Dm.@new<T>(locate(x, y, 1));
        setup?.Invoke(thing);
        return thing;
    });

    /// <summary>The examine list of <paramref name="thing"/> as one string, the way the client's examine joins it.</summary>
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
    public void ChairsGrillesRailingsAndFlapsShowTheirFastenings()
    {
        Rig g = Room(8801);
        var chair = Make<obj_structure_chair>(g);
        string text = Look(g, chair);
        Has(text, "It's held together by a couple of <b>bolts</b>.", "While standing on", "drag and drop your sprite onto", "to buckle to it.");
        g.R.In(() => chair.can_buckle = FALSE);
        Lacks(Look(g, chair), "drag and drop your sprite");

        var grille = Make<obj_structure_grille>(g, x: 7);
        Has(Look(g, grille), "It's secured in place with <b>screws</b>. The rods look like they could be <b>cut</b> through.");
        g.R.In(() => grille.anchored = FALSE);
        Has(Look(g, grille), "The anchoring screws are <b>unscrewed</b>.");
        g.R.In(() => Dm.combine(ref grille.resistance_flags, INDESTRUCTIBLE));
        Lacks(Look(g, grille), "anchoring screws", "secured in place");

        var railing = Make<obj_structure_railing>(g, x: 8);
        Has(Look(g, railing), "The railing is <b>bolted</b> to the floor.");
        g.R.In(() => railing.anchored = FALSE);
        Has(Look(g, railing), "The railing is <i>unbolted</i> from the floor and can be deconstructed with <b>wirecutters</b>.");

        var flaps = Make<obj_structure_plasticflaps>(g, x: 9);
        Has(Look(g, flaps), "are <b>screwed</b> to the floor.");
        g.R.In(() => flaps.anchored = FALSE);
        Has(Look(g, flaps), "are no longer <i>screwed</i> to the floor, and the flaps can be <b>cut</b> apart.");
    }

    [Fact]
    public void GirdersAndAssembliesShowTheirConstructionStep()
    {
        Rig g = Room(8802);
        var girder = Make<obj_structure_girder>(g);
        Has(Look(g, girder), "The bolts are <b>wrenched</b> in place.", "The frame could be sliced apart with a <b>plasmacutter</b>.");
        g.R.In(() => { girder.state = GIRDER_DISPLACED; girder.can_weld_apart = TRUE; });
        string text = Look(g, girder);
        Has(text, "The bolts are <i>loosened</i>, but the <b>screws</b> are holding", "The frame looks weak enough to be <b>welded</b> apart.");
        Lacks(text, "wrenched", "plasmacutter");
        g.R.In(() => girder.state = GIRDER_REINF);
        Has(Look(g, girder), "The support struts are <b>screwed</b> in place.");

        var door = Make<obj_structure_door_assembly>(g, x: 7);
        Has(Look(g, door), "The assembly is <b>welded together</b>, but the anchoring bolts are <i>unwrenched</i>.", "There are <i>empty</i> slots for glass windows and mineral covers.");
        g.R.In(() => { door.anchored = TRUE; door.glass = TRUE; });
        text = Look(g, door);
        Has(text, "The anchoring bolts are <b>wrenched</b> in place, but the maintenance panel lacks <i>wiring</i>.", "There are <i>empty</i> slots for mineral covers.");
        Lacks(text, "glass windows and mineral covers");
        g.R.In(() => { door.state = AIRLOCK_ASSEMBLY_NEEDS_ELECTRONICS; door.created_name = "Vault"; });
        Has(Look(g, door), "The maintenance panel is <b>wired</b>, but the circuit slot is <i>empty</i>.", "written on it is 'Vault'.");
        g.R.In(() => door.state = AIRLOCK_ASSEMBLY_NEEDS_SCREWDRIVER);
        Has(Look(g, door), "The circuit is <b>connected loosely</b> to its slot, but the maintenance panel is <i>unscrewed and open</i>.");

        var windoor = Make<obj_structure_windoor_assembly>(g, x: 8);
        Has(Look(g, windoor), "can be <span class='boldnotice'>wrenched</span> down.", "could also be <span class='boldnotice'>cut apart</span> with a <span class='boldnotice'>welder</span>.");
        g.R.In(() => windoor.anchored = TRUE);
        Has(Look(g, windoor), "needs <span class='boldnotice'>wiring</span>, or could be <span class='boldnotice'>un-wrenched</span> from the floor.");
        g.R.In(() => windoor.cables_added = TRUE);
        Has(Look(g, windoor), "needs <span class='boldnotice'>airlock electronics</span> to continue installation, or <span class='boldnotice'>wirecutters</span> to take apart.");
        g.R.In(() => windoor.electronics = Dm.@new<obj_item_electronics_airlock>(windoor));
        Has(Look(g, windoor), "is ready to be <span class='boldnotice'>levered</span> into place with a <span class='boldnotice'>crowbar</span>.");
    }

    [Fact]
    public void WindowsTablesRacksAndFalseWallsShowTheirStateAndTheStructureBaseShowsDamage()
    {
        Rig g = Room(8803);
        var window = Make<obj_structure_window>(g);
        Has(Look(g, window), "The window is <b>screwed</b> to the frame.");
        g.R.In(() => window.state = WINDOW_IN_FRAME);
        Has(Look(g, window), "The window is <i>unscrewed</i> but <b>pried</b> into the frame.");
        g.R.In(() => { window.state = WINDOW_OUT_OF_FRAME; window.anchored = TRUE; });
        Has(Look(g, window), "It can be <b>pried</b> into a more secure position.");
        g.R.In(() => window.anchored = FALSE);
        Has(Look(g, window), "could be deconstructed by <b>wrenching</b>.");

        var rwindow = Make<obj_structure_window_reinforced>(g, x: 7);
        Has(Look(g, rwindow), "you'd need to <b>heat them</b>");
        g.R.In(() => rwindow.state = RWINDOW_BARS_CUT);
        Has(Look(g, rwindow), "reveal some <b>bolts</b> holding the frame in.");

        // the structure base (structures.dm:36): damage, fire and breakage
        g.R.In(() => window.atom_integrity = window.max_integrity * 0.4f);
        Has(Look(g, window), "It appears heavily damaged.");
        g.R.In(() => window.atom_integrity = window.max_integrity * 0.9f);
        Has(Look(g, window), "It looks slightly damaged.");
        g.R.In(() => { window.atom_integrity = window.max_integrity * 0.1f; });
        Has(Look(g, window), "It's falling apart!");
        g.R.In(() => { window.broken = TRUE; Dm.combine(ref window.resistance_flags, ON_FIRE); });
        string text = Look(g, window);
        Has(text, "It's on fire!", "It appears to be broken.");
        Lacks(text, "falling apart");

        var wall = Make<obj_structure_falsewall>(g, x: 8);
        Has(Look(g, wall), "The outer plating is <b>welded</b> firmly in place.");
        var rwall = Make<obj_structure_falsewall_reinforced>(g, x: 9);
        Has(Look(g, rwall), "The outer <b>grille</b> is fully intact.");

        var rack = Make<obj_structure_rack>(g, y: 6);
        Has(Look(g, rack), "It's held together by a couple of <b>bolts</b>.");
        var table = Make<obj_structure_table>(g, x: 7, y: 6);
        Has(Look(g, table), "The top is <b>screwed</b> on, but the main <b>bolts</b> are also visible.");
        Lacks(Look(g, table), "flipped");
        g.R.In(() => table.is_flipped = TRUE);
        Has(Look(g, table), "It's been flipped on its side!");
        var rtable = Make<obj_structure_table_reinforced>(g, x: 8, y: 6);
        Has(Look(g, rtable), "The top cover is firmly <b>welded</b> on.");
        g.R.In(() => rtable.deconstruction_ready = TRUE);
        Has(Look(g, rtable), "The top cover has been <i>welded</i> loose");
    }

    [Fact]
    public void WallsAndFloorsShowTheirHints()
    {
        Rig g = Room(8804);
        var wall = (turf_closed_wall)g.R.In(() => (turf)Waiting.NoSleep(locate(6, 5, 1).ChangeTurf(typeof(turf_closed_wall))));
        Has(Look(g, wall), "The outer plating is <b>welded</b> firmly in place.");
        // atom_mounted.dm:42: a wall says what it supports when the examiner sees it
        Lacks(Look(g, wall), "is currently supporting");
        var sign = Make<obj_structure_sign>(g, x: 5, y: 5);
        g.R.In(() => Waiting.NoSleep(sign.AddComponent(typeof(datum_component_atom_mounted), wall)));
        Has(Look(g, wall), "is currently supporting", "fall to the ground");
        var rwall = (turf_closed_wall_r_wall)g.R.In(() => (turf)Waiting.NoSleep(locate(7, 5, 1).ChangeTurf(typeof(turf_closed_wall_r_wall))));
        Has(Look(g, rwall), "The outer <b>grille</b> is fully intact.");
        g.R.In(() => rwall.d_state = COVER);
        Has(Look(g, rwall), "the metal cover is <b>welded</b> firmly in place.");
        Lacks(Look(g, rwall), "outer plating");

        var iron = g.R.In(() => (turf)Waiting.NoSleep(locate(6, 6, 1).ChangeTurf(typeof(turf_open_floor_iron))));
        Has(Look(g, iron), "There's a <b>small crack</b> on the edge of it.");
        var plating = (turf_open_floor_plating)g.R.In(() => (turf)Waiting.NoSleep(locate(7, 6, 1).ChangeTurf(typeof(turf_open_floor_plating))));
        Has(Look(g, plating), "There are a few attachment holes for a new <i>tile</i> or reinforcement <i>rods</i>.", "You could probably make this plating more resilient with some plasteel.");
        g.R.In(() => { plating.attachment_holes = FALSE; plating.upgradable = FALSE; });
        Has(Look(g, plating), "You might be able to build ontop of it with some <i>tiles</i>...");
        g.R.In(() => plating.broken = TRUE);
        string text = Look(g, plating);
        Has(text, "It looks like the dents could be <i>welded</i> smooth.");
        Lacks(text, "build ontop");
        var engine = g.R.In(() => (turf)Waiting.NoSleep(locate(8, 6, 1).ChangeTurf(typeof(turf_open_floor_engine))));
        Has(Look(g, engine), "The reinforcement rods are <b>wrenched</b> firmly in place.");
    }

    [Fact]
    public void ItemsShowTheirOwnLines()
    {
        Rig g = Room(8805);
        // a machine circuit board: its components by name (circuitboard.dm:161)
        var board = Make<obj_item_circuitboard_machine>(g);
        Has(Look(g, board), "It requires no components.");
        g.R.In(() => board.req_components = Dm.list(Dm.kv(typeof(obj_item_stack_cable_coil), 5), Dm.kv(typeof(obj_item_stock_parts_capacitor), 2)));
        Has(Look(g, board), "It requires 5 cable pieces and 2 capacitors.");

        // the airlock painter: its ink level, and the controls hint /obj/examine adds
        var painter = Make<obj_item_airlock_painter>(g, x: 7);
        string text = Look(g, painter);
        Has(text, "Its ink levels look high.", "Alt-Click to remove the ink cartridge.");
        g.R.In(() => painter.ink.charges = 1);
        Has(Look(g, painter), "Its ink levels look low.");
        g.R.In(() => painter.ink.charges = 0);
        Has(Look(g, painter), "Its ink levels look empty.");
        g.R.In(() => painter.ink = null);
        Has(Look(g, painter), "It doesn't have a toner cartridge installed.");

        var frame = Make<obj_item_wallframe>(g, x: 8);
        Has(Look(g, frame), "It can be <b>wrenched</b> apart.");

        var thrower = Make<obj_item_flamethrower>(g, x: 9);
        Lacks(Look(g, thrower), "attached");
        g.R.In(() => thrower.ptank = Dm.@new<obj_item_tank_internals_plasma>(thrower));
        Has(Look(g, thrower), "has a plasma tank attached. Alt-click to remove it.");

        var spawner = Make<obj_effect_spawner_random_maintenance>(g, y: 6);
        Has(Look(g, spawner), "This spawner has an effective loot count of");
    }

    [Fact]
    public void TheWeaponDescriptionAndTheLoreAreLinksAndTheirReadouts()
    {
        Rig g = Room(8806);
        // a stun baton: force, notes and the baton's readout (weapon_description.dm, baton.dm:84)
        var baton = Make<obj_item_melee_baton_security_loaded>(g);
        string text = Look(g, baton);
        Has(text, "See combat information.", "You can <b>look closer</b> to learn a little more about", "Left click to stun, right click to harm.");
        Lacks(text, "an unholy union of mace and cattleprod");
        Has(LookMore(g, baton), "an unholy union of mace and cattleprod");

        string link = System.Text.RegularExpressions.Regex.Match(text, @"href='byond://\?([^']+)'").Groups[1].Value;
        Assert.Contains("examine=1", link, StringComparison.Ordinal);
        var chat = new List<string>();
        PresentationSinks.Of(g.R.World).Messages = new Feed(chat);
        g.R.Command(() => ClientSeam.Topic(g.Bob.client, link));
        g.R.Step(2);
        // the readout the link prints: the base lines and the baton's own (baton.dm:84)
        string readout = string.Join("\n", chat);
        Has(readout, "boxed_message", "It takes about", "melee hit", "to stun an enemy");

        // a laser gun and a casing: the energy gun's and the casing's own notes, the laser's lore
        var laser = Make<obj_item_gun_energy_laser>(g, x: 7);
        Has(Look(g, laser), "See combat information.", "to learn a little more about");
        Has(LookMore(g, laser), "Type 5 Heat Delivery System");
        var casing = Make<obj_item_ammo_casing_c9mm>(g, x: 8);
        Has(Look(g, casing), "See combat information.");
        // an item with no force, no throw force and no notes has no link
        var coil = Make<obj_item_stack_cable_coil>(g, x: 9);
        Lacks(Look(g, coil), "See combat information.");
    }

    [Fact]
    public void StorageIdCardsAndOrgansShowTheirLines()
    {
        Rig g = Room(8807);
        // the storage datum: the can-hold hint, its closer look and the Topic link's text (storage.dm:294-311)
        var pouch = Make<obj_item_storage_belt_utility>(g);
        Has(Look(g, pouch), "You can examine this further to check what kind of extra items it can hold.");
        Has(LookMore(g, pouch), "can hold:");
        var chat = new List<string>();
        PresentationSinks.Of(g.R.World).Messages = new Feed(chat);
        g.R.Command(() => ClientSeam.Topic(g.Bob.client, "src=" + g.R.In(() => REF(pouch)) + ";show_valid_pocket_items=1"));
        g.R.Step(2);
        Assert.Contains(chat, c => c.Contains("can hold:", StringComparison.Ordinal));

        // the ID card: the appraiser's line, and the psycho's grip on a tastefully thick card
        var card = Make<obj_item_card_id>(g, x: 3, y: 2);
        Lacks(Look(g, card), "issued from Central Command");
        g.R.In(() => ADD_TRAIT(g.Bob, TRAIT_ID_APPRAISER, "test"));
        Has(Look(g, card), "This ID was created in this sector, not by Central Command.");
        g.R.In(() => ADD_TRAIT(card, TRAIT_TASTEFULLY_THICK_ID_CARD, "test"));
        Has(Look(g, card), "The tasteful thickness of it.");
        Assert.True(g.R.In(() => HAS_TRAIT(card, TRAIT_NODROP)));
        Assert.True(g.R.In(() => Dm.truthy(g.Bob.is_holding(card))), "the card was put in his hands");
        g.R.Step(11 * Second);
        Assert.False(g.R.In(() => HAS_TRAIT(card, TRAIT_NODROP)));
        Assert.False(g.R.In(() => Dm.truthy(g.Bob.is_holding(card))), "drop_card dropped it");

        // organs: where it goes, and its health (organ.dm:200), the brain's life and size, the stomach's cut
        var heart = Make<obj_item_organ_heart>(g, x: 8);
        Has(Look(g, heart), "It should be inserted in the");
        Lacks(Look(g, heart), "discolored");
        g.R.In(() => heart.damage = heart.high_threshold + 1);
        Has(Look(g, heart), "is starting to look discolored.");
        g.R.In(() => Dm.combine(ref heart.organ_flags, ORGAN_FAILING));
        Has(Look(g, heart), heart_failing(g, heart));
        var brain = Make<obj_item_organ_brain>(g, x: 9);
        Has(Look(g, brain), "This one is completely devoid of life.");
        g.R.In(() => { brain.smooth_brain = TRUE; brain.brain_size = 0.8f; });
        Has(Look(g, brain), "All the pesky wrinkles are gone.", "It is a bit on the smaller side...");
        g.R.In(() => brain.suicided = TRUE);
        Has(Look(g, brain), "It's started turning slightly grey.");
        var stomach = Make<obj_item_organ_stomach>(g, y: 6);
        Lacks(Look(g, stomach), "sizeable cut");
        g.R.In(() => stomach.cut_open_damage = 10);
        Has(Look(g, stomach), "It has a sizeable cut in it, exposing its insides!");
    }

    private static string heart_failing(Rig g, obj_item_organ heart) => g.R.In(() => heart.failing_desc);

    private sealed class Feed(List<string> chat) : IMessageSink
    {
        void IMessageSink.Chat(object target, DmList message) => chat.Add(Dm.str(message?["html"]));

        public void Output(object target, string text) { }

        public void Balloon(object viewer, object atom, string text) { }
    }
}
