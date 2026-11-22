// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/alien/larva/larva_defense.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file.

// tg: code/modules/mob/living/carbon/alien/larva/larva_defense.dm:3
public partial class mob_living_carbon_alien_larva : mob_living_carbon_alien
{
    // tg: code/modules/mob/living/carbon/alien/larva/larva_defense.dm:3
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return TRUE;
        float damage = rand(1, 9);
        if (prob(90))
        {
            playsound(loc, SFX_PUNCH, 25, TRUE, -1);
            visible_message(span_danger(I18n.T("[] kicks []!", user, this)),
                            span_userdanger(I18n.T("[] kicks you!", user)), span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")), COMBAT_MESSAGE_RANGE, user);
            to_chat(user, span_danger(I18n.T("You kick []!", this)));
            if ((stat != DEAD) && (damage > 4.9f))
                await Unconscious(rand(100, 200));

            obj_item_bodypart affecting = get_bodypart(get_random_valid_zone(user.zone_selected) as string) as obj_item_bodypart;
            await apply_damage(damage, BRUTE, affecting);
            log_combat(user, this, "attacked");
        }
        else
        {
            playsound(loc, Dm.resource("sound/items/weapons/punchmiss.ogg"), 25, TRUE, -1);
            visible_message(span_danger(I18n.T("[]'s kick misses []!", user, this)),
                            span_danger(I18n.T("You avoid []'s kick!", user)), span_hear(I18n.T("You hear a swoosh!")), COMBAT_MESSAGE_RANGE, user);
            to_chat(user, span_warning(I18n.T("Your kick misses []!", this)));
            log_combat(user, this, "attacked and missed");
        }
        return dot;
    }

    // tg: code/modules/mob/living/carbon/alien/larva/larva_defense.dm:26
    public override async DmTask<object> attack_hulk(mob_living_carbon_human user = null, params object[] __extra)
    {
        object dot = await base.attack_hulk(user, __extra);
        if (!Dm.truthy(dot))
            return dot;
        await user.AddComponent(typeof(datum_component_force_move), get_step_away(user, this, 30));
        return dot;
    }

    // tg: code/modules/mob/living/carbon/alien/larva/larva_defense.dm:32
    [DmParams("A", "visual_effect_icon", "used_item", "no_effect")]
    public override async DmTask do_attack_animation(atom attacked_atom = null, object visual_effect_icon = null, obj_item used_item = null, object no_effect = null, float? fov_effect = null, object item_animation_override = null)
    {
        if (!Dm.truthy(no_effect) && !Dm.truthy(visual_effect_icon))
            visual_effect_icon = ATTACK_EFFECT_BITE;
        await base.do_attack_animation(attacked_atom, visual_effect_icon, used_item, no_effect, fov_effect, item_animation_override);
    }
}
