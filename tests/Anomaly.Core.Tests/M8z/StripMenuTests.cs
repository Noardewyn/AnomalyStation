using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M8z;

/// <summary>
/// M8z slice B (D234-D238) in a booted toy room with two joined players, Alice and Bob: the strippable element on a human, the drag of
/// him onto Alice opening his menu, the menu's payload (strippable.dm:349-398), the strip rules (can't strip what is stuck to him),
/// the radial's Strip, the jumpsuit's adjusting (toggle_jumpsuit_adjust) and the shoes' lace verbs.
/// </summary>
public class StripMenuTests
{
    private const int Second = 20;
    private const string Drag = "left=1;button=left;drag=left";

    private sealed record Rig(AudioRoom R, mob_living_carbon_human Alice, mob_living_carbon_human Bob);

    private static Rig Build(ulong seed)
    {
        var r = new AudioRoom(12, 9, seed: seed);
        mob_living_carbon_human alice = r.Join(4, 4, "Alice");
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        r.Step(Second);
        // the toy room's joiners are bare: Bob wears a jumpsuit and shoes
        r.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_under_color_grey>(bob.loc as atom), ITEM_SLOT_ICLOTHING))));
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_shoes_sneakers_black>(bob.loc as atom), ITEM_SLOT_FEET))));
        });
        return new Rig(r, alice, bob);
    }

    private static datum_strip_menu MenuOf(Rig g)
    {
        g.R.Command(() => ClientSeam.Drop(g.Alice.client, g.Bob, g.Alice, Drag, Drag, 5));
        g.R.Step(Second);
        return g.R.In(() => (datum_strip_menu)((datum_element_strippable)SSdcs.GetElement(Dm.list(typeof(datum_element_strippable), GLOB.strippable_human_items, nameof(mob_living_carbon_human.should_strip)))).strip_menus?[g.Bob]);
    }

    [Fact]
    public void DraggingAHumanOntoAnotherOpensHisMenuAndDraggingHimOntoHimselfDoesNot()
    {
        var g = Build(9401);
        g.R.Command(() => ClientSeam.Drop(g.Bob.client, g.Bob, g.Bob, Drag, Drag, 5));
        g.R.Step(Second);
        Assert.Empty(Dm.each(g.R.In(() => g.Alice.open_uis)));
        Assert.Empty(Dm.each(g.R.In(() => g.Bob.open_uis)));

        datum_strip_menu menu = MenuOf(g);
        Assert.NotNull(menu);
        Assert.Same(g.Bob, g.R.In(() => menu.owner));
        Assert.Single(Dm.each(g.R.In(() => menu.open_uis)));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheMenuListsTheSlotsWithTheItemsAsIconAndStateAndHidesAPocketsItem()
    {
        var g = Build(9402);
        var pen = g.R.In(() => Dm.@new<obj_item_pen>(g.Bob.loc as atom));
        g.R.In(() => Waiting.NoSleep(g.Bob.equip_to_slot(pen, ITEM_SLOT_LPOCKET)));
        datum_strip_menu menu = MenuOf(g);
        DmList data = g.R.In(() => (DmList)Waiting.NoSleep(menu.ui_data(g.Alice)));
        g.R.In(() =>
        {
            DmList items = (DmList)data["items"];
            Assert.Equal(g.Bob.name, Dm.str(data["name"])); // a human's name is a proper noun: no "the"
            // tg's table order and no handcuffs or legcuffs (should_show: nothing is cuffed)
            Assert.Equal(new[] { "head", "back", "mask", "neck", "eyes", "ears", "jumpsuit", "suit", "gloves", "shoes", "suit_storage", "id", "belt", "left_pocket", "right_pocket", "left_hand", "right_hand" },
                Dm.each(items).Select(k => Dm.str(k)).ToArray());
            // a pocket's item is hidden (STRIPPABLE_OBSCURING_HIDDEN) and carries no picture
            var pocket = (DmList)items["left_pocket"];
            Assert.Equal(STRIPPABLE_OBSCURING_HIDDEN, Dm.num(pocket["obscured"]));
            Assert.Null(pocket["name"]);
            // a worn jumpsuit is its icon, state and name, and offers its three alternate actions
            var jumpsuit = (DmList)items["jumpsuit"];
            Assert.Equal(g.Bob.w_uniform.name, Dm.str(jumpsuit["name"]));
            Assert.Equal(g.Bob.w_uniform.icon_state, Dm.str(jumpsuit["icon_state"]));
            Assert.Equal(Dm.str(g.Bob.w_uniform.icon), Dm.str(jumpsuit["icon"]));
            Assert.Equal(STRIPPABLE_OBSCURING_NONE, Dm.num(jumpsuit["obscured"]));
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void WhatIsStuckToAManCannotBeStrippedAndTheJumpsuitAdjustsBackAndForth()
    {
        var g = Build(9403);
        g.R.In(() =>
        {
            obj_item shoes = g.Bob.shoes as obj_item;
            var slot = (datum_strippable_item)GLOB.strippable_human_items["shoes"];
            Assert.True(Dm.truthy(slot.try_unequip(g.Bob, g.Alice)));
            ADD_TRAIT(shoes, TRAIT_NODROP, "test");
            Assert.False(Dm.truthy(slot.try_unequip(g.Bob, g.Alice)));
            Assert.False(Dm.truthy(shoes.canStrip(g.Alice, g.Bob)));

            var jumpsuit = (obj_item_clothing_under)g.Bob.w_uniform;
            float covered = jumpsuit.body_parts_covered;
            Assert.Equal(ALT_STYLE, Dm.num(jumpsuit.toggle_jumpsuit_adjust()));
            Assert.True(jumpsuit.alt_covers_chest != 0 || Dm.num(jumpsuit.body_parts_covered) < covered);
            Assert.Equal(NORMAL_STYLE, Dm.num(jumpsuit.toggle_jumpsuit_adjust()));
            Assert.Equal(covered, Dm.num(jumpsuit.body_parts_covered));
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ATankOnTheBeltOffersInternalsAndAliceOpensTheValveAfterFourSeconds()
    {
        var g = Build(9406);
        obj_item_tank tank = g.R.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Bob.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_mask_breath>(g.Bob.loc as atom), ITEM_SLOT_MASK))));
            var made = Dm.@new<obj_item_tank_internals_emergency_oxygen>(g.Bob.loc as atom);
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Bob.equip_to_slot_if_possible(made, ITEM_SLOT_BELT))));
            return made;
        });
        datum_strip_menu menu = MenuOf(g);
        g.R.In(() =>
        {
            DmList data = (DmList)Waiting.NoSleep(menu.ui_data(g.Alice));
            var belt = (DmList)((DmList)data["items"])["belt"];
            Assert.Equal(new[] { "enable_internals" }, Dm.each(belt["alternate"]).Select(a => Dm.str(a)).ToArray());
            Assert.Null(g.Bob.@internal);
        });
        // the valve takes INTERNALS_TOGGLE_DELAY (4 s) and Bob and the room are told
        g.R.Command(() => Waiting.Detach(GlobalProcs.strippable_alternate_action_internals(tank, g.Bob, g.Alice)));
        g.R.Step(Second);
        Assert.Null(g.R.In(() => g.Bob.@internal));
        g.R.Step(Second * 4);
        Assert.Same(tank, g.R.In(() => g.Bob.@internal));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheCuffsSlotsShowOnlyWhileHeIsCuffedAndNothingCanBeEquippedInThem()
    {
        var g = Build(9407);
        datum_strip_menu menu = MenuOf(g);
        Func<string[]> keys = () => g.R.In(() => Dm.each(((DmList)Waiting.NoSleep(menu.ui_data(g.Alice)))["items"] as DmList).Select(k => Dm.str(k)).ToArray());
        Assert.DoesNotContain("handcuffs", keys());
        var cuffs = g.R.In(() => Dm.@new<obj_item_restraints_handcuffs>(g.Bob.loc as atom));
        g.R.In(() => Waiting.NoSleep(g.Bob.equip_to_slot(cuffs, ITEM_SLOT_HANDCUFFED)));
        Assert.Contains("handcuffs", keys());
        Assert.DoesNotContain("legcuffs", keys());
        g.R.In(() => Assert.False(Dm.truthy(Waiting.NoSleep(((datum_strippable_item)GLOB.strippable_human_items["handcuffs"]).try_equip(g.Bob, Dm.@new<obj_item_pen>(g.Bob.loc as atom), g.Alice)))));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheRadialOffersStripOnAnotherHumanButNotOnHimself()
    {
        var g = Build(9404);
        RadialExtra[] Of(mob_living_carbon_human user, atom target) => g.R.In(() => RadialExtras.Of(user, target).Select(e => e.Kind).ToArray());
        Assert.Contains(RadialExtra.Strip, Of(g.Alice, g.Bob));
        Assert.DoesNotContain(RadialExtra.Strip, Of(g.Alice, g.Alice));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheShoelaceVerbsAndPronounsFollowTgs()
    {
        var g = Build(9405);
        g.R.In(() =>
        {
            var shoes = (obj_item_clothing_shoes)g.Bob.shoes;
            Assert.Equal(("untied", "tie", "tying"), (Dm.str(shoes.untied_adjective()), Dm.str(shoes.fasten_verb()), Dm.str(shoes.fastening_verb())));
            shoes.fastening_type = SHOES_VELCRO;
            Assert.Equal(("loose", "fasten", "fastening"), (Dm.str(shoes.untied_adjective()), Dm.str(shoes.fasten_verb()), Dm.str(shoes.fastening_verb())));
            shoes.fastening_type = SHOES_LACED;
            shoes.adjust_laces(SHOES_KNOTTED, g.Alice);
            Assert.Equal(SHOES_KNOTTED, shoes.tied);
            shoes.adjust_laces(SHOES_TIED, g.Bob);
            Assert.Equal(SHOES_TIED, shoes.tied);
            Assert.Equal(("was", "were"), (Dm.str(g.Bob.p_were()), Dm.str(g.Bob.p_were("plural"))));
        });
        g.R.AssertNoRuntimes();
    }
}
