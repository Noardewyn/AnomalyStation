using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.P4;
using Anomaly.Core.Tests.Tg.M4aAccess.P5;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M4v;

/// <summary>
/// M4v slice 3 (D143): tg's short-lived overlay images (__HELPERS/game.dm:83-150) and their callers. flick_overlay_view puts a
/// /atom/movable/flick_visual with the given look into the atom's vis_contents and deletes it after the duration (it leaves the
/// vis_contents through vis_locs = null); flick_overlay_global adds an image to the clients' images and takes it out again; the
/// pickup image flies off the floor tile toward the picker, the attack images sit on the target, the buttons flash.
/// </summary>
public class FlickOverlayTests
{
    private const int Second = 20;

    private static object[] Items(DmList list) => Enumerable.Range(1, list.Count).Select(i => list[(float)i]).ToArray();

    private static atom_movable_flick_visual[] Visuals(atom holder) => Items(holder.vis_contents).OfType<atom_movable_flick_visual>().ToArray();

    [Fact]
    public void FlickOverlayViewShowsTheStateOverTheAtomAndDeletesItAfterTheDuration()
    {
        var room = new CapsuleRoom(4501);
        room.Join(5, 5, "Alice");
        obj_item_crowbar crowbar = room.In(() => Dm.@new<obj_item_crowbar>(locate(6, 5, 1)));
        atom_movable_flick_visual visual = room.In(() => crowbar.flick_overlay_view("crowbar_large", 1 * SECONDS) as atom_movable_flick_visual);
        room.In(() =>
        {
            Assert.NotNull(visual);
            Assert.Same(visual, Assert.Single(Visuals(crowbar)));
            Assert.Null(visual.loc);
            Assert.Equal("crowbar_large", visual.icon_state);
            Assert.Equal(crowbar.icon, visual.icon);
            Assert.Equal(crowbar.layer, visual.layer); // mutable_appearance(icon, state, layer) takes the atom's layer
            Assert.Equal((float)MOUSE_OPACITY_TRANSPARENT, visual.mouse_opacity);
            Assert.Same(crowbar, Assert.Single(Items(visual.vis_locs)));
        });
        room.Step(Second / 2);
        room.In(() => Assert.Single(Visuals(crowbar)));
        room.Step(Second / 2 + 2);
        room.In(() =>
        {
            Assert.NotNull(visual.gc_destroyed); // QDEL_IN_CLIENT_TIME(visual, duration)
            Assert.Empty(Visuals(crowbar)); // /atom/movable/Destroy: vis_locs = null
            Assert.Empty(Items(visual.vis_locs));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AnImageWithoutALayerGoesJustAboveTheAtomAndTheAreaShowsNothing()
    {
        var room = new CapsuleRoom(4502);
        room.Join(5, 5, "Alice");
        room.In(() =>
        {
            turf floor = locate(6, 5, 1) as turf;
            image look = Dm.image("icons/effects/effects.dmi", null, "punch");
            Assert.Equal(Dm.FLOAT_LAYER, look.layer);
            var visual = floor.flick_overlay_view(look, 5) as atom_movable_flick_visual;
            Assert.Equal(floor.layer + 0.1f, visual.layer, 4);
            Assert.Equal("punch", visual.icon_state);
            Assert.Null(((area)get_area(floor)).flick_overlay_view("anything", 5));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void FlickOverlayGlobalShowsTheImageToTheClientsForTheDuration()
    {
        var room = new CapsuleRoom(4503);
        mob_living_carbon_human alice = room.Join(5, 5, "Alice");
        image shown = room.In(() =>
        {
            image look = Dm.image("icons/effects/effects.dmi", alice, "punch");
            flick_overlay_global(look, Dm.list(alice.client), 6);
            Assert.True(Dm.@in(look, alice.client.images));
            return look;
        });
        room.Step(8); // 0.4 s of the 0.6
        room.In(() => Assert.True(Dm.@in(shown, alice.client.images)));
        room.Step(8);
        room.In(() => Assert.False(Dm.@in(shown, alice.client.images)));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AnImageTransformReadIsACopyAndAnImageOfAnAtomKeepsItsLook()
    {
        var room = new CapsuleRoom(4504);
        room.Join(5, 5, "Alice");
        room.In(() =>
        {
            var crowbar = Dm.@new<obj_item_crowbar>(locate(6, 5, 1));
            crowbar.pixel_x = 3;
            crowbar.pixel_z = 2;
            crowbar.transform = Dm.matrix().Scale(2f);
            image look = Dm.image(icon: crowbar);
            Assert.Equal(3f, look.pixel_x);
            Assert.Equal(2f, look.pixel_z);
            Assert.Equal(2f, ((matrix)look.transform).a);
            ((matrix)look.transform).Scale(0.5f); // a copy: no change (tg's pickup_animation.transform.Scale(0.75), items.dm:1516)
            Assert.Equal(2f, ((matrix)look.transform).a);
            image plain = Dm.image("icons/effects/effects.dmi", null, "punch");
            Assert.Null(((IDmAppearance)plain).transform);
            plain.transform = Dm.mul(plain.transform, 0.4f); // tg's attack_image.transform *= 0.4 (items.dm:1596) scales the identity
            Assert.Equal(0.4f, ((matrix)plain.transform).a, 4);

            var visual = Dm.@new<atom_movable_flick_visual>();
            visual.appearance = look;
            Assert.Equal(crowbar.icon_state, visual.icon_state);
            Assert.Equal(3f, visual.pixel_x);
            Assert.Equal(2f, ((matrix)visual.transform).a);
            Assert.Equal(crowbar.icon_state, ((IDmAppearance)visual.appearance).icon_state);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void APickupFliesTheItemsImageFromItsTileTowardThePicker()
    {
        var room = new CapsuleRoom(4505);
        mob_living_carbon_human alice = room.Join(5, 5, "Alice");
        obj_item_crowbar crowbar = room.In(() => Dm.@new<obj_item_crowbar>(locate(6, 5, 1)));
        turf floor = room.In(() => crowbar.loc as turf);
        room.In(() => Waiting.NoSleep(alice.put_in_hand(crowbar, 1, ignore_anim: Dm.FALSE)));
        room.In(() =>
        {
            Assert.Same(alice, crowbar.loc);
            atom_movable_flick_visual pickup = Assert.Single(Visuals(floor));
            Assert.Equal(crowbar.icon_state, pickup.icon_state);
            Assert.Equal((float)APPEARANCE_UI_IGNORE_ALPHA, pickup.appearance_flags);
            // The picker is west of the tile: the image ends a tile west (to_x = -32), transparent, shrunk to 0.7 (BYOND has set the end values).
            Assert.Equal(-32f, pickup.pixel_x);
            Assert.Equal(0f, pickup.pixel_y);
            Assert.Equal(0f, pickup.alpha);
            Assert.Equal(0.7f, ((matrix)pickup.transform).a, 3);
        });
        room.Step(Second / 2);
        room.In(() => Assert.Empty(Visuals(floor))); // 0.4 s
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AnItemsAttackShowsItsImageOnTheTargetAndAPunchItsEffect()
    {
        var room = new CapsuleRoom(4506);
        mob_living_carbon_human alice = room.Join(5, 5, "Alice");
        mob_living_carbon_human bob = room.Join(6, 5, "Bob");
        room.In(() =>
        {
            var crowbar = Dm.@new<obj_item_crowbar>(alice.loc);
            alice.do_item_attack_animation(bob, null, crowbar, null);
            atom_movable_flick_visual swing = Assert.Single(Visuals(bob));
            Assert.Equal(crowbar.icon_state, swing.icon_state);
            Assert.Equal(bob.plane + 1, swing.plane);
            Assert.Equal((float)APPEARANCE_UI, swing.appearance_flags);
            Assert.Equal(0f, swing.alpha); // the blunt chain (a crowbar is not sharp) ends transparent
            alice.do_item_attack_animation(bob, ATTACK_EFFECT_PUNCH, null, null);
            atom_movable_flick_visual punch = Visuals(bob).Single(v => v != swing);
            Assert.Equal(ATTACK_EFFECT_PUNCH, punch.icon_state);
            Assert.Equal("icons/effects/effects.dmi", punch.icon?.ToString());
        });
        room.Step(Second + 2);
        room.In(() => Assert.Empty(Visuals(bob)));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AButtonFlashesSuccessOnAPressAndErrorWithoutAccess()
    {
        var room = new CapsuleRoom(4507);
        mob_living_carbon_human alice = room.Join(5, 5, "Alice");
        obj_machinery_button_door button = room.In(() =>
        {
            var made = Dm.@new<obj_machinery_button_door>(locate(6, 5, 1));
            made.req_access = Dm.list(ACCESS_MEDICAL);
            return made;
        });
        room.Step(Second);
        room.In(() =>
        {
            Waiting.NoSleep(button.attempt_press(alice));
            Assert.Equal("button-overlay-error", Assert.Single(Visuals(button)).icon_state);
        });
        room.Step(Second + 2);
        room.In(() => Assert.Empty(Visuals(button)));
        room.In(() => IdCards.Hold(alice, ACCESS_MEDICAL));
        room.In(() =>
        {
            Waiting.NoSleep(button.attempt_press(alice));
            Assert.Equal("button-overlay-success", Assert.Single(Visuals(button)).icon_state);
        });
        room.Step(Second + 2);
        room.In(() => Assert.Empty(Visuals(button)));
        room.AssertNoRuntimes();
    }
}
