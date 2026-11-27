// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/watcher/watcher.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// A floating eyeball which keeps its distance and sometimes make you look away.
// tg: code/modules/mob/living/basic/lavaland/watcher/watcher.dm:2
public partial class mob_living_basic_mining_watcher : mob_living_basic_mining
{
    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher.dm:44
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_flying)));
        Waiting.NoSleep(AddElement(typeof(datum_element_content_barfer)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_ai_target_timer)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_basic_ranged_ready_overlay), Dm.named("overlay_state", eye_glow)));
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_ranged_attacks),
            Dm.named("cooldown_time", ranged_cooldown),
            Dm.named("projectile_type", projectile_type),
            Dm.named("projectile_sound", shoot_sound)));
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_magnet),
            Dm.named("attracted_typecache", wanted_objects),
            Dm.named("on_contact", Dm.@new<datum_callback>(this, nameof(consume)))));
        update_appearance(UPDATE_OVERLAYS);

        datum_action_cooldown_mob_cooldown_watcher_gaze gaze = Dm.@new(gaze_attack, this) as datum_action_cooldown_mob_cooldown_watcher_gaze;
        Waiting.NoSleep(gaze.Grant(this));
        datum_ai_controller controller = ai_controller as datum_ai_controller;
        controller.set_blackboard_key(BB_GENERIC_ACTION, gaze);
        Waiting.NoSleep(AddComponent(typeof(datum_component_revenge_ability), gaze, Dm.named("targeting", GET_TARGETING_STRATEGY(controller.blackboard[BB_TARGETING_STRATEGY]))));
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher.dm:68
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (stat == DEAD)
            return dot;
        Dm.Deferred("code/modules/mob/living/basic/lavaland/watcher/watcher.dm:72", "update_overlays: the watcher's emissive_appearance overlays, its glow in the dark (DEVIATION DV12)");
        return dot;
    }

    /// I love eating diamonds yum
    // tg: code/modules/mob/living/basic/lavaland/watcher/watcher.dm:76
    public virtual void consume(atom_movable thing = null)
    {
        visible_message(span_warning(I18n.T("[] seems to vanish into []'s body!", thing, this)));
        Waiting.NoSleep(thing.forceMove(this));
    }
}
