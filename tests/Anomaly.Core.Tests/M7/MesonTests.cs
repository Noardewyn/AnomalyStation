using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7;

/// <summary>
/// M7 slice T3 (D164): the meson goggles (_glasses.dm:88-135: the optical, the night vision and the gar ones) and what they need: the
/// glasses' sight in /mob/living/update_sight (living.dm:2029), the clothing traits (TRAIT_MESON_VISION, TRAIT_MADNESS_IMMUNE), the client
/// colour (client_colour.dm, wearable_client_colour.dm: the light green tint the wearer toggles with a right alt-click), the night vision
/// toggle (toggles.dm:123), and tg's unit test client_colours.
/// </summary>
public class MesonTests
{
    private const int Second = 20;
    private const string AltRight = "icon-x=16;icon-y=16;right=1;alt=1;button=right";

    private static (AudioRoom R, mob_living_carbon_human Bob) Build(ulong seed)
    {
        var r = new AudioRoom(13, 10, seed: seed);
        mob_living_carbon_human bob = r.Join(4, 5, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, bob);
    }

    private static T Wear<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item_clothing_glasses => r.In(() =>
    {
        T glasses = Dm.@new<T>(get_turf(who));
        Assert.True(Dm.truthy(Waiting.NoSleep(who.equip_to_slot_if_possible(glasses, ITEM_SLOT_EYES))), "the goggles go on the eyes");
        return glasses;
    });

    private static DmList Cutoffs(mob_living_carbon_human who) => who.lighting_color_cutoffs;

    private static void AssertCutoffs(mob_living_carbon_human who, float lighting, float r, float g, float b)
    {
        Assert.Equal(lighting, who.lighting_cutoff ?? 0f, 3);
        Assert.Equal(r, Dm.num(Cutoffs(who)[1]), 3);
        Assert.Equal(g, Dm.num(Cutoffs(who)[2]), 3);
        Assert.Equal(b, Dm.num(Cutoffs(who)[3]), 3);
    }

    [Fact]
    public void OpticalMesonsGiveTheTraitsSeeTurfsAndTgsColourCutoffsWhileWornAndTakeThemAwayWhenRemoved()
    {
        var (r, bob) = Build(8401);
        r.In(() =>
        {
            Assert.False(HAS_TRAIT(bob, TRAIT_MESON_VISION));
            Assert.Equal(0f, bob.sight);
            AssertCutoffs(bob, 0, 0, 0, 0);
        });
        var meson = Wear<obj_item_clothing_glasses_meson>(r, bob);
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(bob, TRAIT_MESON_VISION));
            Assert.True(HAS_TRAIT(bob, TRAIT_MADNESS_IMMUNE)); // as a trait only (the supermatter's madness is another slice's)
            Assert.True(Dm.truthy(Dm.bitand(bob.sight, SEE_TURFS)), "SEE_TURFS from TRAIT_MESON_VISION (living.dm:2071)");
            Assert.False(Dm.truthy(Dm.bitand(bob.sight, SEE_MOBS)));
            // color_cutoffs list(5, 15, 5) of the glasses (:93); the trait's LIGHTING_CUTOFF_REAL_LOW (4.5) less their average (8.33) is below 0
            AssertCutoffs(bob, 0, 5, 15, 5);
            Assert.Empty(Dm.each(bob.client_colours)); // the tint is off until toggled (below)
        });

        r.In(() => Waiting.NoSleep(bob.dropItemToGround(meson)));
        r.In(() =>
        {
            Assert.False(HAS_TRAIT(bob, TRAIT_MESON_VISION));
            Assert.False(HAS_TRAIT(bob, TRAIT_MADNESS_IMMUNE));
            Assert.Equal(0f, bob.sight);
            AssertCutoffs(bob, 0, 0, 0, 0);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void NightVisionMesonsHaveTheStrongerCutoffsAndTheirToggleTakesThemAndTheTintOff()
    {
        var (r, bob) = Build(8402);
        var night = Wear<obj_item_clothing_glasses_meson_night>(r, bob);
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(bob, TRAIT_MESON_VISION));
            AssertCutoffs(bob, 0, 10, 35, 10); // :112
            Assert.Equal(FLASH_PROTECTION_SENSITIVE, Dm.num(night.flash_protect));
            Assert.Single(Dm.each(night.actions));
            Assert.Equal("nvgmeson", night.icon_state);
        });
        // the action's button: tg's toggle_nv (toggles.dm:132) stores the cutoffs, takes the sight lift off and clicks
        r.Sounds.Heard.Clear();
        r.In(() =>
        {
            var action = Assert.IsType<datum_action_item_action_toggle_nv>(Dm.each(night.actions).Single());
            Assert.True(Dm.truthy(Waiting.NoSleep(action.do_effect(0))));
            Assert.Equal(FLASH_PROTECTION_NONE, Dm.num(night.flash_protect));
            Assert.Equal(0f, Dm.num(night.color_cutoffs.len));
            AssertCutoffs(bob, 4.5f, 0, 0, 0); // without the colour cutoffs the trait's LIGHTING_CUTOFF_REAL_LOW (4.5) stands (living.dm:2071)
            Assert.True(HAS_TRAIT(bob, TRAIT_MESON_VISION)); // the mesons stay on: the turfs are still seen
            Assert.True(Dm.truthy(Dm.bitand(bob.sight, SEE_TURFS)));
            Assert.Equal("nvgmeson_off", night.icon_state);
        });
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/machines/click.ogg");
        r.In(() =>
        {
            var action = (datum_action_item_action_toggle_nv)Dm.each(night.actions).Single();
            Waiting.NoSleep(action.do_effect(0));
            AssertCutoffs(bob, 0, 10, 35, 10);
            Assert.Equal("nvgmeson", night.icon_state);
        });
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/night_vision_on.ogg");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePlainTgColourOfTheMesonsIsAppliedOnlyOnceTheWearerTogglesItWithARightAltClickAndGoesWithTheGlasses()
    {
        var (r, bob) = Build(8403);
        var meson = Wear<obj_item_clothing_glasses_meson>(r, bob);
        r.In(() =>
        {
            Assert.Empty(Dm.each(bob.client_colours)); // wearable_client_colour.dm:82: not forced, no TRAIT_SEE_WORN_COLOURS
            Assert.Contains("Right-Alt-Click", string.Join(" ", Dm.each(Waiting.NoSleep(meson.examine(bob))).Select(Dm.str))); // the element's examine line (:123)
        });
        // the toggle (:109): a right alt-click on the glasses
        r.Command(() => ClientSeam.Click(bob.client, meson, AltRight));
        r.Step(Second);
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(bob, TRAIT_SEE_WORN_COLOURS));
            datum_client_colour colour = Assert.IsType<datum_client_colour_glass_colour_lightgreen>(Assert.Single(Dm.each(bob.client_colours)));
            Assert.Equal("#ccffcc", Dm.str(colour.color));
            Assert.Equal(GLASSES_TRAIT, Dm.str(bob.client_colours[colour]));
            Assert.Equal(CLIENT_COLOR_GLASSES_PRIORITY, colour.priority);
            Assert.Same(bob, colour.owner);
            // the filter the client gets: one colour matrix filter on the game plane master controller
            DmList filters = (DmList)get_atom_filters__test(bob);
            Assert.Equal(1f, filters.len);
            Assert.Equal("color", Dm.str(((DmList)filters[1])["type"]));
            Assert.Equal("#ccffcc", Dm.str(((DmList)filters[1])["color"]));
        });
        // taken off the eyes: the colour goes (:72), its trait stays with the wearer (a preference of his)
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(meson)));
        r.In(() =>
        {
            Assert.Empty(Dm.each(bob.client_colours));
            Assert.Equal(0f, ((DmList)get_atom_filters__test(bob)).len);
        });
        // put on again: the trait is still his, so the tint comes at once
        var again = Wear<obj_item_clothing_glasses_meson>(r, bob);
        r.In(() => Assert.Single(Dm.each(bob.client_colours)));
        // and a second toggle turns it off for good
        r.Command(() => ClientSeam.Click(bob.client, again, AltRight));
        r.Step(Second);
        r.In(() =>
        {
            Assert.False(HAS_TRAIT(bob, TRAIT_SEE_WORN_COLOURS));
            Assert.Empty(Dm.each(bob.client_colours));
        });
        r.AssertNoRuntimes();
    }

    // the filters of the player's game plane master controller, the list that reaches the client (protocol version 29)
    private static object get_atom_filters__test(mob who)
    {
        var controller = (atom_movable_plane_master_controller)who.hud_used.plane_master_controllers[PLANE_MASTERS_GAME];
        return controller.filter_data ?? Dm.list();
    }

    [Fact]
    public void ClientColoursBlendByPriorityAndAnOverrideStopsTheLowerOnes()
    {
        var (r, bob) = Build(8404);
        r.In(() =>
        {
            bob.add_client_colour(typeof(datum_client_colour_glass_colour_lightgreen), "a");
            // two plain colours of the same group blend by tg's averaging (get_client_colour_filters)
            bob.add_client_colour(typeof(datum_client_colour_glass_colour_lightblue), "b");
            DmList filters = (DmList)bob.get_client_colour_filters();
            Assert.Equal(1f, filters.len);
            DmList blended = (DmList)((DmList)((DmList)filters[1])[1])["color"];
            // lightgreen #ccffcc (0.8, 1, 0.8) averaged with lightblue #ccccff (0.8, 0.8, 1): diagonal entries
            Assert.Equal(0.8f, Dm.num(blended[1]), 3);
            Assert.Equal(0.9f, Dm.num(blended[6]), 3);
            Assert.Equal(0.9f, Dm.num(blended[11]), 3);
            // a split_filters colour (the nightmare glasses) stands apart: two filters
            bob.add_client_colour(typeof(datum_client_colour_glass_colour_nightmare), "c");
            Assert.Equal(2f, ((DmList)bob.get_client_colour_filters()).len);
            // the same source replaces the colour, the same type with the same source is kept
            var kept = bob.add_client_colour(typeof(datum_client_colour_glass_colour_nightmare), "c");
            Assert.Equal(3f, bob.client_colours.len);
            Assert.NotNull(kept);
            Assert.True(Dm.truthy(bob.remove_client_colour("c")));
            Assert.False(Dm.truthy(bob.remove_client_colour("c")));
            Assert.Equal(2f, bob.client_colours.len);
        });
        r.AssertNoRuntimes();
    }

    // tg's unit test client_colours (code/modules/unit_tests/client_colours.dm)
    [Fact]
    public void EveryClientColourHasAValidDefaultColour()
    {
        var (r, bob) = Build(8405);
        r.In(() =>
        {
            int checkedColours = 0;
            foreach (Type colour_type in Dm.each_cast<Type>(subtypesof(typeof(datum_client_colour))))
            {
                var colour = (datum_client_colour)Dm.@new(colour_type);
                // colours can be color matrices (lists), which initial() cannot read.
                if (colour.color is DmList potential_filter && Dm.truthy(potential_filter["type"]))
                    continue; // If our list has "type" in it then its a filter, ignore that
                checkedColours++;
                if (!Dm.truthy(colour.color)) // rave and temp have no colour of their own
                    continue;
                Assert.True(Dm.truthy(color_to_full_rgba_matrix(colour.color, FALSE)), colour.type + " has an invalid default colour value: " + Dm.str(colour.color));
            }
            Assert.True(checkedColours >= 20);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheGarMesonsAreVarOnlyAndSharpAndSeeTurfsToo()
    {
        var (r, bob) = Build(8406);
        var gar = Wear<obj_item_clothing_glasses_meson_gar>(r, bob);
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(bob, TRAIT_MESON_VISION));
            Assert.Equal(10f, Dm.num(gar.force));
            Assert.True(Dm.truthy(Dm.bitand(bob.sight, SEE_TURFS)));
            AssertCutoffs(bob, 0, 5, 15, 5);
        });
        r.AssertNoRuntimes();
    }
}
