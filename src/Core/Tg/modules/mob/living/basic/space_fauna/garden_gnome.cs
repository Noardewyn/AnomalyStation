// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/garden_gnome.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

// tg: code/modules/mob/living/basic/space_fauna/garden_gnome.dm:1
public partial class mob_living_basic_garden_gnome : mob_living_basic
{
    /// Realistically weighted list of usual gnome hat colours
    private static DmList gnome_hat_colours => TgState.Current.mob_living_basic_garden_gnome__gnome_hat_colours ??= Dm.list(
        Dm.kv(COLOR_GNOME_RED_ONE, 9f), Dm.kv(COLOR_GNOME_RED_TWO, 9f), Dm.kv(COLOR_GNOME_RED_THREE, 9f),
        Dm.kv(COLOR_GNOME_BLUE_ONE, 3f), Dm.kv(COLOR_GNOME_BLUE_TWO, 3f), Dm.kv(COLOR_GNOME_BLUE_THREE, 3f),
        Dm.kv(COLOR_GNOME_GREEN_ONE, 3f), Dm.kv(COLOR_GNOME_GREEN_TWO, 3f), Dm.kv(COLOR_GNOME_ORANGE, 3f), Dm.kv(COLOR_GNOME_BROWN_ONE, 3f),
        Dm.kv(COLOR_GNOME_YELLOW, 2f), Dm.kv(COLOR_GNOME_GREY, 2f), Dm.kv(COLOR_GNOME_PURPLE, 1f), Dm.kv(COLOR_GNOME_WHITE, 1f), Dm.kv(COLOR_GNOME_BLACK, 1f));
    /// Realistically weighted list of usual gnome body colours
    private static DmList gnome_body_colours => TgState.Current.mob_living_basic_garden_gnome__gnome_body_colours ??= Dm.list(
        Dm.kv(COLOR_GNOME_YELLOW, 6f), Dm.kv(COLOR_GNOME_RED_ONE, 3f), Dm.kv(COLOR_GNOME_RED_TWO, 3f), Dm.kv(COLOR_GNOME_RED_THREE, 3f),
        Dm.kv(COLOR_GNOME_BLUE_ONE, 3f), Dm.kv(COLOR_GNOME_BLUE_TWO, 3f), Dm.kv(COLOR_GNOME_BLUE_THREE, 3f),
        Dm.kv(COLOR_GNOME_GREEN_ONE, 3f), Dm.kv(COLOR_GNOME_GREEN_TWO, 3f), Dm.kv(COLOR_GNOME_BROWN_ONE, 3f),
        Dm.kv(COLOR_GNOME_ORANGE, 2f), Dm.kv(COLOR_GNOME_WHITE, 1f), Dm.kv(COLOR_GNOME_GREY, 1f), Dm.kv(COLOR_GNOME_PURPLE, 1f), Dm.kv(COLOR_GNOME_BLACK, 1f));
    /// Realistically weighted list of usual gnome pants colours
    private static DmList gnome_pants_colours => TgState.Current.mob_living_basic_garden_gnome__gnome_pants_colours ??= Dm.list(
        Dm.kv(COLOR_GNOME_BLUE_ONE, 6f), Dm.kv(COLOR_GNOME_BLUE_TWO, 6f), Dm.kv(COLOR_GNOME_BLUE_THREE, 6f), Dm.kv(COLOR_GNOME_GREEN_ONE, 6f), Dm.kv(COLOR_GNOME_GREEN_TWO, 6f),
        Dm.kv(COLOR_GNOME_BROWN_ONE, 3f), Dm.kv(COLOR_GNOME_BROWN_TWO, 3f),
        Dm.kv(COLOR_GNOME_RED_ONE, 1f), Dm.kv(COLOR_GNOME_ORANGE, 1f), Dm.kv(COLOR_GNOME_WHITE, 1f), Dm.kv(COLOR_GNOME_GREY, 1f), Dm.kv(COLOR_GNOME_PURPLE, 1f), Dm.kv(COLOR_GNOME_BLACK, 1f));
    /// Realistically weighted list of usual gnome beard colours
    private static DmList gnome_beard_colours => TgState.Current.mob_living_basic_garden_gnome__gnome_beard_colours ??= Dm.list(
        Dm.kv(COLOR_GNOME_WHITE, 9f), Dm.kv(COLOR_GNOME_GREY, 9f), Dm.kv(COLOR_GNOME_BROWN_ONE, 6f), Dm.kv(COLOR_GNOME_BROWN_TWO, 6f), Dm.kv(COLOR_GNOME_BLACK, 6f),
        Dm.kv(COLOR_GNOME_ORANGE, 3f), Dm.kv(COLOR_GNOME_GREEN_TWO, 1f), Dm.kv(COLOR_GNOME_RED_ONE, 1f), Dm.kv(COLOR_GNOME_PURPLE, 1f));

    // tg: code/modules/mob/living/basic/space_fauna/garden_gnome.dm:99
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        datum_callback retaliate_callback = Dm.@new<datum_callback>(this, nameof(ai_retaliate_behaviour));
        chosen_hat_colour = pick_weight(gnome_hat_colours);
        apply_colour();
        Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), typeof(obj_effect_gibspawner_generic)));
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), FOOTSTEP_MOB_SHOE));
        Waiting.NoSleep(AddComponent(typeof(datum_component_ai_retaliate_advanced), retaliate_callback));
        Waiting.NoSleep(AddComponent(typeof(datum_component_swarming)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_ground_sinking), Dm.named("target_icon_state", icon_state), Dm.named("outline_colour", chosen_hat_colour), Dm.named("sink_callback", Dm.@new<datum_callback>(this, nameof(toggle_sink_damage_res)))));
        Waiting.NoSleep(AddComponent(typeof(datum_component_caltrop), Dm.named("min_damage", 5f), Dm.named("max_damage", 10f), Dm.named("paralyze_duration", 1 * SECONDS), Dm.named("flags", CALTROP_BYPASS_SHOES)));
        add_traits(Dm.list(TRAIT_SPACEWALK, TRAIT_VENTCRAWLER_ALWAYS), INNATE_TRAIT);
        return dot;
    }

    ///Apply extra damage resistances when sunk in the ground.
    // tg: code/modules/mob/living/basic/space_fauna/garden_gnome.dm:113
    public virtual void toggle_sink_damage_res(object has_sunk = null)
    {
        MODIFY_PHYSIOLOGY(this, BRUTE, Dm.truthy(has_sunk) ? 0.5f : 2);
        MODIFY_PHYSIOLOGY(this, BURN, Dm.truthy(has_sunk) ? 0.5f : 2);
        MODIFY_PHYSIOLOGY(this, STAMINA, Dm.truthy(has_sunk) ? 0.1f : 10);
    }

    // tg: code/modules/mob/living/basic/space_fauna/garden_gnome.dm:118
    public virtual void apply_colour()
    {
        if (greyscale_config == null)
            return;
        Waiting.NoSleep(set_greyscale(colors: Dm.list(chosen_hat_colour, pick_weight(gnome_body_colours), pick_weight(gnome_pants_colours), pick_weight(gnome_beard_colours))));
    }

    // tg: code/modules/mob/living/basic/space_fauna/garden_gnome.dm:123
    public virtual void ai_retaliate_behaviour(object attacker_ = null)
    {
        if (attacker_ is not mob_living attacker)
            return;
        foreach (mob_living_basic_garden_gnome potential_gnome in Dm.each<mob_living_basic_garden_gnome>(oview(7, this)))
            (potential_gnome.ai_controller as datum_ai_controller)?.set_blackboard_key_assoc_lazylist(BB_BASIC_MOB_RETALIATE_LIST, attacker, world.time);
    }
}

public partial class TgState
{
    // tg: code/modules/mob/living/basic/space_fauna/garden_gnome.dm:34  (var/static/list)
    internal DmList mob_living_basic_garden_gnome__gnome_hat_colours;
    // tg: code/modules/mob/living/basic/space_fauna/garden_gnome.dm:54
    internal DmList mob_living_basic_garden_gnome__gnome_body_colours;
    // tg: code/modules/mob/living/basic/space_fauna/garden_gnome.dm:72
    internal DmList mob_living_basic_garden_gnome__gnome_pants_colours;
    // tg: code/modules/mob/living/basic/space_fauna/garden_gnome.dm:88
    internal DmList mob_living_basic_garden_gnome__gnome_beard_colours;
}
