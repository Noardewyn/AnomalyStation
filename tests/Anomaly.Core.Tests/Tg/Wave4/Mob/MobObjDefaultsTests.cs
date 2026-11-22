using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Wave4.TurfArea;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using TgMob = Anomaly.Core.Tg.mob;

namespace Anomaly.Core.Tests.Tg.Wave4.Mob;

/// <summary>
/// The var blocks of /mob (modules/mob/mob_defines.dm) and /obj (game/objects/objs.dm) with tg's overrides of BYOND's
/// defaults, read from the DM text. The objects are made in a world whose SSatoms has not initialized, so no Initialize has
/// touched them (rules 10.2: BYOND's own defaults first, then tg's overrides, then the object-creating defaults).
/// </summary>
public class MobObjDefaultsTests
{
    private static TurfAreaWorld NewWorld() => new TurfAreaWorld(1, 1, 1);

    private static void AssertNumber(float expected, object actual, string what)
    {
        Assert.True(actual != null && Dm.eq(actual, expected), what + ": expected " + expected + " but got " + (actual ?? "null"));
    }

    // ---- /mob ----

    [Fact]
    public void ABareMobHasBYONDsMobDefaultsAndTgsOverridesOfThem()
    {
        var w = NewWorld();
        using (w.World.Enter())
        {
            var m = Dm.@new<TgMob>(); // SSatoms has not initialized: nothing ran Initialize
            Assert.False(Dm.truthy(Dm.bitand(m.flags_1, INITIALIZED_1)));
            AssertNumber(TRUE, m.density, "density");
            AssertNumber(MOB_LAYER, m.layer, "layer");
            Assert.Same(typeof(TgMob), m.abstract_type);
            AssertNumber(SLIDE_STEPS, m.animate_movement, "animate_movement");
            AssertNumber(8, m.pressure_resistance, "pressure_resistance");
            AssertNumber(MOUSE_ACTIVE_POINTER, m.mouse_drag_pointer, "mouse_drag_pointer");
            AssertNumber(10, m.throwforce, "throwforce");
            AssertNumber(EMISSIVE_BLOCK_GENERIC, m.blocks_emissive, "blocks_emissive");
            AssertNumber(PASSMOB, m.pass_flags_self, "pass_flags_self");
            AssertNumber(1e6f, m.see_in_dark, "see_in_dark"); // "we never want to hide a turf because it's not lit"
            Assert.Equal(new object[] { ANTAG_HUD }, Dm.each(m.hud_possible).Cast<object>().ToArray());
            Assert.Equal(new object[] { FACTION_NEUTRAL }, Dm.each(m.faction).Cast<object>().ToArray());
        }
    }

    [Fact]
    public void ABareMobHasTheVarBlockOfMobDefinesDm()
    {
        var w = NewWorld();
        using (w.World.Enter())
        {
            var m = Dm.@new<TgMob>();
            Assert.Null(m.canon_client);
            Assert.Null(m.persistent_client);
            AssertNumber(TRUE, m.shift_to_open_context_menu, "shift_to_open_context_menu");
            AssertNumber(LIGHTING_CUTOFF_VISIBLE, m.lighting_cutoff, "lighting_cutoff");
            AssertNumber(0, m.lighting_cutoff_red, "lighting_cutoff_red");
            AssertNumber(0, m.lighting_cutoff_green, "lighting_cutoff_green");
            AssertNumber(0, m.lighting_cutoff_blue, "lighting_cutoff_blue");
            Assert.Null(m.lighting_color_cutoffs);
            Assert.Null(m.mind);
            Assert.Null(m.movespeed_modification);
            Assert.Null(m.movespeed_mod_immunities);
            Assert.Null(m.cached_multiplicative_slowdown);
            Assert.Null(m.actionspeed_modification);
            Assert.Null(m.actionspeed_mod_immunities);
            Assert.Null(m.cached_multiplicative_actions_slowdown);
            Assert.Null(m.actions);
            Assert.True(Dm.eq(m.examine_cursor_icon, Dm.resource("icons/effects/mouse_pointers/examine_pointer.dmi")));
            AssertNumber(NONE, m.mob_flags, "mob_flags");
            AssertNumber(STABLE, m.stat, "stat");
            AssertNumber(NONE, m.incapacitated, "incapacitated");
            Assert.Equal(BODY_ZONE_CHEST, m.zone_selected);
            Assert.Null(m.computer_id);
            Assert.NotNull(m.logging);
            Assert.Equal(0f, m.logging.len);
            Assert.Null(m.next_move);
            Assert.Null(m.real_name);
            AssertNumber(BODYTEMP_NORMAL, m.bodytemperature, "bodytemperature");
            AssertNumber(0, m.old_bodytemperature, "old_bodytemperature");
            AssertNumber(NUTRITION_LEVEL_START_MIN, m.nutrition, "nutrition");
            AssertNumber(0, m.satiety, "satiety");
            AssertNumber(0, m.overeatduration, "overeatduration");
            Assert.Null(m.lastKnownIP);
            Assert.Null(m.buckled);
            AssertNumber(1, m.active_hand_index, "active_hand_index");
            Assert.NotNull(m.held_items);
            Assert.Equal(0f, m.held_items.len);
            AssertNumber(2, m.default_hand_amount, "default_hand_amount");
            Assert.Null(m.active_storage);
            Assert.Null(m.hud_used);
            AssertNumber(THROW_MODE_DISABLED, m.throw_mode, "throw_mode");
            Assert.Null(m.job);
            AssertNumber(CANSTUN | CANKNOCKDOWN | CANUNCONSCIOUS | CANPUSH, m.status_flags, "status_flags");
            Assert.Null(m.remote_control);
            Assert.Null(m.listed_turf);
            Assert.Null(m.observers);
            Assert.Null(m.progressbars);
            Assert.Null(m.do_afters);
            Assert.Null(m.click_intercept);
            Assert.Null(m.registered_z);
            AssertNumber(0, m.memory_throttle_time, "memory_throttle_time");
            foreach (var (name, list) in new[] { ("alerts", m.alerts), ("screens", m.screens), ("client_colours", m.client_colours), ("color_filter_store", m.color_filter_store) })
            {
                Assert.True(list != null, name + " is a list");
                Assert.Equal(0f, list.len);
            }
            Assert.Same(typeof(datum_hud), m.hud_type);
            Assert.Null(m.focus);
            Assert.Null(m.emotes_used);
            AssertNumber(TRUE, m.updating_glide_size, "updating_glide_size");
            AssertNumber(SOUND_ENVIRONMENT_NONE, m.sound_environment_override, "sound_environment_override");
            Assert.Null(m.mock_client);
            AssertNumber(0, m.interaction_range, "interaction_range");
            Assert.Null(m.active_typing_indicator);
            Assert.Null(m.active_thinking_indicator);
            Assert.Null(m.ambience_tracked_area);
            Assert.Null(m.sound_tokens);
        }
    }

    [Fact]
    public void EveryMobGetsItsOwnListsFromTheObjectCreatingDefaults()
    {
        var w = NewWorld();
        using (w.World.Enter())
        {
            var a = Dm.@new<TgMob>();
            var b = Dm.@new<TgMob>();
            Assert.NotSame(a.logging, b.logging);
            Assert.NotSame(a.held_items, b.held_items);
            Assert.NotSame(a.alerts, b.alerts);
            Assert.NotSame(a.hud_possible, b.hud_possible);
            Assert.NotSame(a.faction, b.faction);
            a.held_items.Add(1);
            a.faction.Add("syndicate");
            Assert.Equal(0f, b.held_items.len);
            Assert.Equal(1f, b.faction.len);
        }
    }

    [Fact]
    public void TheMobCountersStartAtTheDviewMobInAFreshWorldAndMobsAreTheirOwnAbstractType()
    {
        var w = NewWorld();
        using (w.World.Enter())
        {
            Assert.Equal(1f, TgMob.next_mob_id); // the first tag, mob_0, is GLOB.dview_mob (mobs.dm:661), made with the globals as in tg
            Assert.True(Dm.eq(Dm.initial(typeof(TgMob), (atom p) => p.abstract_type), typeof(TgMob)));
            // its own too since the M2 var block of /mob/living (living_defines.dm:3, abstract_type = /mob/living)
            Assert.True(Dm.eq(Dm.initial(typeof(mob_living), (atom p) => p.abstract_type), typeof(mob_living)));
        }
    }

    // ---- /obj ----

    [Fact]
    public void ABareObjHasTgsObjDefaults()
    {
        var w = NewWorld();
        using (w.World.Enter())
        {
            var o = Dm.@new<obj>();
            Assert.False(Dm.truthy(Dm.bitand(o.flags_1, INITIALIZED_1)));
            Assert.Same(typeof(obj), o.abstract_type);
            AssertNumber(SLIDE_STEPS, o.animate_movement, "animate_movement");
            Assert.Equal(SPAN_ROBOT, o.speech_span);
            AssertNumber(TRUE, o.uses_integrity, "uses_integrity");
            AssertNumber(CAN_BE_HIT, o.obj_flags, "obj_flags");
            Assert.Null(o.desc_controls);
            Assert.Equal("without leaving a mark", o.no_damage_feedback);
            Assert.Null(o.icon_preview);
            Assert.Null(o.icon_state_preview);
            AssertNumber(0, o.anchored_tabletop_offset, "anchored_tabletop_offset");
            Assert.Equal(BRUTE, o.damtype);
            AssertNumber(0, o.force, "force");
            AssertNumber(0, o.wound_bonus, "wound_bonus");
            AssertNumber(0, o.exposed_wound_bonus, "exposed_wound_bonus");
            AssertNumber(1, o.demolition_mod, "demolition_mod");
            Assert.Null(o.custom_fire_overlay);
            Assert.Null(o.burning_particles);
            Assert.Null(o.drag_slowdown);
            Assert.Null(o.id_tag);
            Assert.Null(o.buckle_sound);
            Assert.Null(o.unbuckle_sound);
            AssertNumber(FALSE, o.generate_map_preview, "generate_map_preview");
        }
    }

    [Fact]
    public void TheObjectsByIdTagGlobalStartsAsAnEmptyList()
    {
        var w = NewWorld();
        using (w.World.Enter())
        {
            Assert.NotNull(GLOB.objects_by_id_tag);
            Assert.Equal(0f, GLOB.objects_by_id_tag.len);
        }
    }

    [Fact]
    public void ASubtypesOverrideReplacesTgsDefaultAndKeepsTheRest()
    {
        var w = NewWorld();
        using (w.World.Enter())
        {
            var o = Dm.@new<obj_test_mob_tabletop>();
            AssertNumber(5, o.anchored_tabletop_offset, "anchored_tabletop_offset");
            AssertNumber(TRUE, o.anchored, "anchored");
            AssertNumber(SLIDE_STEPS, o.animate_movement, "animate_movement"); // still /obj's
            Assert.Same(typeof(obj), o.abstract_type);
        }
    }
}
