// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/ruin_defender/dark_wizard.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the dark wizard, a ruin's guard who wears a dark wizard's look, shoots earth bolts, blinks away from whoever hits
// him and crumbles into remains. The paper wizard's unconscious look that tg repeats here is in paper_wizard.cs. The var blocks and
// the AI controller's behaviour tree are generated.

/// Wizard-looking guy who basically just shoots you
// tg: code/modules/mob/living/basic/ruin_defender/dark_wizard.dm:2
public partial class mob_living_basic_dark_wizard : mob_living_basic
{
    // tg: code/modules/mob/living/basic/ruin_defender/dark_wizard.dm:27
    public override object get_save_vars()
    {
        object dot = base.get_save_vars();
        Dm.remove(ref dot, nameof(icon_state)); // icon_state is applied via apply_dynamic_human_appearance()
        return dot;
    }

    // tg: code/modules/mob/living/basic/ruin_defender/dark_wizard.dm:30
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        apply_dynamic_human_appearance(this, mob_spawn_path: typeof(obj_effect_mob_spawn_corpse_human_wizard_dark), r_hand: typeof(obj_item_staff));
        add_traits(Dm.list(TRAIT_LAVA_IMMUNE, TRAIT_ASHSTORM_IMMUNE, TRAIT_SNOWSTORM_IMMUNE), INNATE_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), typeof(obj_effect_decal_remains_human)));
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), FOOTSTEP_MOB_SHOE));
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));

        Waiting.NoSleep(AddComponent(
            typeof(datum_component_ranged_attacks),
            Dm.named("projectile_type", typeof(obj_projectile_temp_earth_bolt)),
            Dm.named("projectile_sound", Dm.resource("sound/effects/magic/ethereal_enter.ogg")),
            Dm.named("cooldown_time", 2 * SECONDS)));

        var escape_spell = Dm.@new<datum_action_cooldown_spell_teleport_radius_turf_blink_slow>(this);
        Waiting.NoSleep(escape_spell.Grant(this));
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_revenge_ability),
            escape_spell,
            Dm.named("max_range", 3),
            Dm.named("target_self", TRUE)));

        Dm.@new<obj_item_clothing_head_wizard_hood>(this); // Having this hat in our contents allows us to cast wizard spells
        return dot;
    }
}
