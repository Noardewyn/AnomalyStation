using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Silicons;

/// <summary>
/// The cyborg's module controls (a player's report): a click on a module in the cyborg's module storage equips it to the first free
/// module slot (tg's obj/item attack_robot, items.dm:613) and the slot shows it (inventory_slot.dm:55 on the cyborg's module_slot);
/// the engineering cyborg's meson vision, the cyborg camera and the janitor's auto-wash toggle on and back off.
/// </summary>
public class CyborgModuleTests
{
    private const int Second = 20;

    private static (AudioRoom R, mob_living_silicon_robot Borg) Player(ulong seed, Type model)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => (get_area(locate(5, 5, 1)) as area).requires_power = FALSE);
        r.Step(Second);
        r.In(() => Waiting.NoSleep(bob.Robotize()));
        r.Step(2 * Second);
        var borg = r.In(() => Dm.each<mob_living_silicon_robot>(GLOB.silicon_mobs).Single());
        r.In(() => Waiting.NoSleep(borg.model.transform_to(model, forced: TRUE, transform: FALSE)));
        r.Step(Second);
        return (r, borg);
    }

    [Fact]
    public void AClickOnAModuleEquipsItAndItsSlotShowsIt()
    {
        var (r, borg) = Player(9411, typeof(obj_item_robot_model_engineering));
        var rcd = r.In(() => Dm.each<obj_item_construction_rcd_borg>(borg.model.modules).Single());
        r.In(() => Waiting.NoSleep(borg.ClickOn(rcd, "icon-x=16;icon-y=16;left=1")));
        r.Step(2);
        r.In(() =>
        {
            Assert.Same(rcd, borg.held_items[1]);
            var slot = (atom)borg.hud_used.screen_objects[datum_hud.HUD_KEY_HAND_SLOT(1)];
            Assert.True(Dm.@in(rcd, slot.vis_contents), "the module slot shows the RCD");
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMesonVisionTogglesBackOff()
    {
        var (r, borg) = Player(9412, typeof(obj_item_robot_model_engineering));
        var meson = r.In(() => Dm.each<datum_action_cooldown_borg_meson>(borg.actions).Single());
        r.In(() => Waiting.NoSleep(meson.Trigger(borg)));
        r.Step(Second);
        Assert.True(r.In(() => HAS_TRAIT(borg, TRAIT_MESON_VISION)), "on");
        r.In(() => Waiting.NoSleep(meson.Trigger(borg)));
        r.Step(Second);
        Assert.False(r.In(() => HAS_TRAIT(borg, TRAIT_MESON_VISION)), "off");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCameraModeTogglesBackOff()
    {
        var (r, borg) = Player(9413, typeof(obj_item_robot_model_engineering));
        r.In(() => borg.aicamera.toggle_camera_mode(borg));
        Assert.Same(r.In(() => borg.aicamera), r.In(() => borg.click_intercept));
        r.In(() => borg.aicamera.toggle_camera_mode(borg));
        Assert.Null(r.In(() => borg.click_intercept));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheAutoWashTogglesBackOff()
    {
        var (r, borg) = Player(9414, typeof(obj_item_robot_model_janitor));
        var wash = r.In(() => Dm.each<datum_action_toggle_buffer>(borg.actions).Single());
        // the bucket the auto-wash draws from starts empty (tg's janitor refills it in a recharging station)
        r.In(() => Waiting.NoSleep((Dm.each<obj_item_reagent_containers_cup_bucket>(borg.model.modules).Single().reagents as datum_reagents).add_reagent(typeof(datum_reagent_water), 50)));
        r.In(() => Waiting.Detach(wash.Trigger(borg)));
        r.Step(6 * Second);
        Assert.True(r.In(() => Dm.truthy(wash.buffer_on)), "on");
        r.In(() => Waiting.Detach(wash.Trigger(borg)));
        r.Step(8 * Second);
        Assert.False(r.In(() => Dm.truthy(wash.buffer_on)), "off");
        r.AssertNoRuntimes();
    }
}
