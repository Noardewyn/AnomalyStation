using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tests.M2Contracts.VarBlocks;

/// <summary>
/// The var blocks of the M2 HUD area (the M2 contracts step): /datum/hud and its human and living subtypes
/// (_onclick/hud/hud.dm, human.dm, living.dm), the screen object base and the health, health doll and move intent
/// screens (screen_objects/screen_objects.dm), the fullscreen overlays (screen_objects/fullscreen.dm) and the alerts
/// (screen_objects/alert.dm). Each type carries its tg path, and its prototype (initial(), rules 10.3) holds tg's
/// declared defaults and the overrides that its constructor assigns (rules 10.2).
/// </summary>
public class HudVarBlockTests
{
    [Theory]
    [InlineData(typeof(datum_hud), "/datum/hud")]
    [InlineData(typeof(datum_hud_human), "/datum/hud/human")]
    [InlineData(typeof(datum_hud_living), "/datum/hud/living")]
    [InlineData(typeof(atom_movable_screen), "/atom/movable/screen")]
    [InlineData(typeof(atom_movable_screen_healthdoll), "/atom/movable/screen/healthdoll")]
    [InlineData(typeof(atom_movable_screen_healthdoll_living), "/atom/movable/screen/healthdoll/living")]
    [InlineData(typeof(atom_movable_screen_healthdoll_human), "/atom/movable/screen/healthdoll/human")]
    [InlineData(typeof(atom_movable_screen_healths), "/atom/movable/screen/healths")]
    [InlineData(typeof(atom_movable_screen_mov_intent), "/atom/movable/screen/mov_intent")]
    [InlineData(typeof(atom_movable_screen_fullscreen), "/atom/movable/screen/fullscreen")]
    [InlineData(typeof(atom_movable_screen_fullscreen_crit), "/atom/movable/screen/fullscreen/crit")]
    [InlineData(typeof(atom_movable_screen_fullscreen_crit_vision), "/atom/movable/screen/fullscreen/crit/vision")]
    [InlineData(typeof(atom_movable_screen_fullscreen_oxy), "/atom/movable/screen/fullscreen/oxy")]
    [InlineData(typeof(atom_movable_screen_fullscreen_brute), "/atom/movable/screen/fullscreen/brute")]
    [InlineData(typeof(atom_movable_screen_alert), "/atom/movable/screen/alert")]
    [InlineData(typeof(atom_movable_screen_alert_not_enough_oxy), "/atom/movable/screen/alert/not_enough_oxy")]
    [InlineData(typeof(atom_movable_screen_alert_too_much_co2), "/atom/movable/screen/alert/too_much_co2")]
    [InlineData(typeof(atom_movable_screen_alert_succumb), "/atom/movable/screen/alert/succumb")]
    public void EachTypeCarriesItsTgPath(Type type, string path)
    {
        Assert.Equal(path, DmTypes.PathOf(type));
    }

    [Fact]
    public void HudDefaultsAndItsSubtypes()
    {
        // hud.dm:26-60
        Assert.True(Dm.eq(initial(typeof(datum_hud), (datum_hud p) => p.hud_shown), TRUE));
        Assert.True(Dm.eq(initial(typeof(datum_hud), (datum_hud p) => p.hud_version), HUD_STYLE_STANDARD));
        Assert.True(Dm.eq(initial(typeof(datum_hud), (datum_hud p) => p.hotkey_ui_hidden), FALSE));
        Assert.Equal("Enabled", initial(typeof(datum_hud), (datum_hud p) => p.screentips_enabled));
        Assert.True(Dm.eq(initial(typeof(datum_hud), (datum_hud p) => p.needs_health_indicator), TRUE));
        Assert.Null(initial(typeof(datum_hud), (datum_hud p) => p.ui_style));
        Assert.Null(initial(typeof(datum_hud), (datum_hud p) => p.default_inventory_slots));
        // human.dm:2: a typepath in the list-declared var (hud.dm:75, typed object)
        Assert.True(Dm.eq(initial(typeof(datum_hud_human), (datum_hud p) => p.default_inventory_slots),
            typeof(datum_inventory_slot_human)));
        Assert.True(Dm.eq(initial(typeof(datum_hud_living), (datum_hud p) => p.hud_shown), TRUE));
        // The list defaults are runtime-initialized (rules 10.2): null on the prototype.
        Assert.Null(initial(typeof(datum_hud), (datum_hud p) => p.screen_objects));
    }

    [Fact]
    public void HudListDefaultsAreMadeByTheInitHook()
    {
        // hud.dm:36-83, a world-free instance: the pre-New hook alone (rules 10.2)
        var hud = new datum_hud();
        hud.dm_init_vars();
        Assert.Equal(0, hud.master_groups.Count);
        Assert.Equal(0, hud.plane_master_controllers.Count);
        Assert.Equal(0, hud.screen_objects.Count);
        Assert.True(Dm.eq(hud.screen_groups.len, SCREEN_GROUP_AMT)); // var/list/screen_groups[SCREEN_GROUP_AMT]
        Assert.Equal(0, hud.inventory_slots.Count);
        Assert.Equal(0, hud.asset_refs_for_reuse.Count);
        Assert.Null(hud.floating_actions);
    }

    [Fact]
    public void ScreenObjectDefaults()
    {
        // screen_objects.dm:11-49
        Assert.Equal("", initial(typeof(atom_movable_screen), (atom_movable_screen p) => p.name));
        Assert.True(Dm.eq(initial(typeof(atom_movable_screen), (atom_movable_screen p) => p.icon),
            Dm.resource("icons/hud/screen_gen.dmi")));
        Assert.True(Dm.eq(initial(typeof(atom_movable_screen), (atom_movable_screen p) => p.plane), HUD_PLANE));
        Assert.True(Dm.eq(initial(typeof(atom_movable_screen), (atom_movable_screen p) => p.appearance_flags), APPEARANCE_UI));
        Assert.True(Dm.eq(initial(typeof(atom_movable_screen), (atom_movable_screen p) => p.speech_span), SPAN_ROBOT));
        float flags = initial(typeof(atom_movable_screen), (atom_movable_screen p) => p.interaction_flags_atom);
        Assert.True(Dm.eq(Dm.bitand(flags, INTERACT_ATOM_MOUSEDROP_IGNORE_CHECKS), INTERACT_ATOM_MOUSEDROP_IGNORE_CHECKS));
        Assert.True(Dm.eq(initial(typeof(atom_movable_screen), (atom_movable_screen p) => p.del_on_map_removal), TRUE));
        Assert.True(Dm.eq(initial(typeof(atom_movable_screen), (atom_movable_screen p) => p.default_click), FALSE));
        Assert.Null(initial(typeof(atom_movable_screen), (atom_movable_screen p) => p.hud));

        // :522-527, :868-871, :905-959
        Assert.Equal("run/walk toggle", initial(typeof(atom_movable_screen_mov_intent), (atom_movable_screen p) => p.name));
        Assert.Equal("running", initial(typeof(atom_movable_screen_mov_intent), (atom_movable_screen p) => p.icon_state));
        Assert.Equal("EAST-2:26,SOUTH:5", initial(typeof(atom_movable_screen_mov_intent), (atom_movable_screen p) => p.screen_loc));
        Assert.True(Dm.eq(initial(typeof(atom_movable_screen_mov_intent), (atom_movable_screen p) => p.plane), HUD_PLANE));
        Assert.Equal("health0", initial(typeof(atom_movable_screen_healths), (atom_movable_screen p) => p.icon_state));
        Assert.Equal(ui_health, initial(typeof(atom_movable_screen_healths), (atom_movable_screen p) => p.screen_loc));
        Assert.Equal("health doll", initial(typeof(atom_movable_screen_healthdoll), (atom_movable_screen p) => p.name));
        Assert.Equal(ui_healthdoll, initial(typeof(atom_movable_screen_healthdoll), (atom_movable_screen p) => p.screen_loc));
        Assert.Equal("health doll", initial(typeof(atom_movable_screen_healthdoll_living), (atom_movable_screen p) => p.name));
        Assert.Equal("fullhealth0", initial(typeof(atom_movable_screen_healthdoll_living), (atom_movable_screen p) => p.icon_state));
        Assert.Equal(ui_living_healthdoll, initial(typeof(atom_movable_screen_healthdoll_living), (atom_movable_screen p) => p.screen_loc));
        Assert.True(Dm.eq(initial(typeof(atom_movable_screen_healthdoll_living), (atom_movable_screen_healthdoll_living p) => p.hovering), FALSE));
        Assert.Equal(ui_healthdoll, initial(typeof(atom_movable_screen_healthdoll_human), (atom_movable_screen p) => p.screen_loc));
        Assert.Null(initial(typeof(atom_movable_screen_healthdoll_human), (atom_movable_screen_healthdoll_human p) => p.limbs));
    }

    [Fact]
    public void FullscreenDefaults()
    {
        // fullscreen.dm:75-119
        Assert.Equal("default", initial(typeof(atom_movable_screen_fullscreen), (atom_movable_screen p) => p.icon_state));
        Assert.Equal("CENTER-7,CENTER-7", initial(typeof(atom_movable_screen_fullscreen), (atom_movable_screen p) => p.screen_loc));
        Assert.True(Dm.eq(initial(typeof(atom_movable_screen_fullscreen), (atom_movable_screen p) => p.layer), FULLSCREEN_LAYER));
        Assert.True(Dm.eq(initial(typeof(atom_movable_screen_fullscreen), (atom_movable_screen p) => p.plane), FULLSCREEN_PLANE));
        Assert.True(Dm.eq(initial(typeof(atom_movable_screen_fullscreen), (atom_movable_screen p) => p.mouse_opacity), MOUSE_OPACITY_TRANSPARENT));
        Assert.True(Dm.eq(initial(typeof(atom_movable_screen_fullscreen), (atom_movable_screen_fullscreen p) => p.view), 7));
        Assert.True(Dm.eq(initial(typeof(atom_movable_screen_fullscreen), (atom_movable_screen_fullscreen p) => p.show_when_dead), FALSE));
        Assert.True(Dm.eq(initial(typeof(atom_movable_screen_fullscreen), (atom_movable_screen_fullscreen p) => p.needs_offsetting), TRUE));
        Assert.Equal("brutedamageoverlay", initial(typeof(atom_movable_screen_fullscreen_brute), (atom_movable_screen p) => p.icon_state));
        Assert.True(Dm.eq(initial(typeof(atom_movable_screen_fullscreen_brute), (atom_movable_screen p) => p.layer), UI_DAMAGE_LAYER));
        Assert.Equal("oxydamageoverlay", initial(typeof(atom_movable_screen_fullscreen_oxy), (atom_movable_screen p) => p.icon_state));
        Assert.Equal("passage", initial(typeof(atom_movable_screen_fullscreen_crit), (atom_movable_screen p) => p.icon_state));
        Assert.True(Dm.eq(initial(typeof(atom_movable_screen_fullscreen_crit), (atom_movable_screen p) => p.layer), CRIT_LAYER));
        Assert.Equal("oxydamageoverlay", initial(typeof(atom_movable_screen_fullscreen_crit_vision), (atom_movable_screen p) => p.icon_state));
        Assert.True(Dm.eq(initial(typeof(atom_movable_screen_fullscreen_crit_vision), (atom_movable_screen p) => p.layer), BLIND_LAYER));
        Assert.True(Dm.eq(initial(typeof(atom_movable_screen_fullscreen_crit_vision), (atom_movable_screen p) => p.plane), FULLSCREEN_PLANE));
    }

    [Fact]
    public void AlertDefaults()
    {
        // alert.dm:98-120
        Assert.Equal("Alert", initial(typeof(atom_movable_screen_alert), (atom_movable_screen p) => p.name));
        Assert.Equal("template", initial(typeof(atom_movable_screen_alert), (atom_movable_screen p) => p.icon_state));
        Assert.True(Dm.eq(initial(typeof(atom_movable_screen_alert), (atom_movable_screen p) => p.mouse_opacity), MOUSE_OPACITY_ICON));
        Assert.True(Dm.eq(initial(typeof(atom_movable_screen_alert), (atom_movable_screen_alert p) => p.timeout), 0));
        Assert.True(Dm.eq(initial(typeof(atom_movable_screen_alert), (atom_movable_screen_alert p) => p.click_master), TRUE));
        Assert.True(Dm.eq(initial(typeof(atom_movable_screen_alert), (atom_movable_screen_alert p) => p.use_user_hud_icon), USER_HUD_STYLE_IGNORE));
        Assert.True(Dm.eq(initial(typeof(atom_movable_screen_alert), (atom_movable_screen_alert p) => p.overlay_icon),
            Dm.resource("icons/hud/screen_alert.dmi")));
        Assert.Null(initial(typeof(atom_movable_screen_alert), (atom_movable_screen_alert p) => p.overlay_state));

        // :204-232
        Assert.Equal("Choking (No O2)", initial(typeof(atom_movable_screen_alert_not_enough_oxy), (atom_movable_screen p) => p.name));
        Assert.Equal(ALERT_NOT_ENOUGH_OXYGEN, initial(typeof(atom_movable_screen_alert_not_enough_oxy), (atom_movable_screen p) => p.icon_state));
        Assert.Equal("Choking (CO2)", initial(typeof(atom_movable_screen_alert_too_much_co2), (atom_movable_screen p) => p.name));
        Assert.Equal(ALERT_TOO_MUCH_CO2, initial(typeof(atom_movable_screen_alert_too_much_co2), (atom_movable_screen p) => p.icon_state));

        // :578-584
        Assert.Equal("Succumb", initial(typeof(atom_movable_screen_alert_succumb), (atom_movable_screen p) => p.name));
        Assert.True(Dm.eq(initial(typeof(atom_movable_screen_alert_succumb), (atom_movable_screen_alert p) => p.use_user_hud_icon), USER_HUD_STYLE_INHERIT));
        Assert.True(Dm.eq(initial(typeof(atom_movable_screen_alert_succumb), (atom_movable_screen_alert p) => p.overlay_icon),
            Dm.resource("icons/mob/simple/mob.dmi")));
        Assert.True(Dm.eq(initial(typeof(atom_movable_screen_alert_succumb), (atom_movable_screen_alert p) => p.overlay_state), "ghost"));
        Assert.True(Dm.eq(initial(typeof(atom_movable_screen_alert_succumb), (atom_movable_screen_alert p) => p.clickable_glow), TRUE));
    }

    [Fact]
    public void SuccumbDeathTitlesIsAPerWorldStatic()
    {
        // alert.dm:585: var/static/list/death_titles (rules 10.6)
        var w = new World();
        using (w.Enter())
        {
            DmList titles = atom_movable_screen_alert_succumb.death_titles;
            Assert.Equal(8, titles.Count);
            Assert.True(Dm.eq(titles[1], "Goodnight, Sweet Prince"));
            Assert.True(Dm.eq(titles[8], "All Good Things Must End"));
        }
    }
}
