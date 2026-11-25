// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/alien/adult/adult_defense.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file.

// tg: code/modules/mob/living/carbon/alien/adult/adult_defense.dm:3
public partial class mob_living_carbon_alien_adult : mob_living_carbon_alien
{
    // tg: code/modules/mob/living/carbon/alien/adult/adult_defense.dm:3
    public override async DmTask<object> attack_hulk(mob_living_carbon_human user = null, params object[] __extra)
    {
        object dot = await base.attack_hulk(user, __extra);
        if (!Dm.truthy(dot))
            return dot;
        await adjust_brute_loss(15);
        string hitverb = "hit";
        if (mob_size < MOB_SIZE_LARGE)
        {
            await safe_throw_at(get_edge_target_turf(this, get_dir(user, this)) as atom, 2, 1, user);
            hitverb = "slam";
        }
        playsound(loc, SFX_PUNCH, 25, TRUE, -1);
        visible_message(span_danger(I18n.T("[] []s []!", user, hitverb, this)),
                        span_userdanger(I18n.T("[] []s you!", user, hitverb)), span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")), COMBAT_MESSAGE_RANGE, user);
        to_chat(user, span_danger(I18n.T("You [] []!", hitverb, this)));
        return dot;
    }

    // tg: code/modules/mob/living/carbon/alien/adult/adult_defense.dm:17
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return TRUE;
        float damage = rand(1, 9);
        if (prob(90))
        {
            playsound(loc, SFX_PUNCH, 25, TRUE, -1);
            visible_message(span_danger(I18n.T("[] punches []!", user, this)),
                            span_userdanger(I18n.T("[] punches you!", user)), span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")), COMBAT_MESSAGE_RANGE, user);
            to_chat(user, span_danger(I18n.T("You punch []!", this)));
            if ((stat != DEAD) && (damage > 9 || prob(5)))//Regular humans have a very small chance of knocking an alien down.
            {
                await Unconscious(40);
                visible_message(span_danger(I18n.T("[] knocks [] down!", user, this)),
                                span_userdanger(I18n.T("[] knocks you down!", user)), span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")), null, user);
                to_chat(user, span_danger(I18n.T("You knock [] down!", this)));
            }
            // DEVIATION(DV591): an aimed melee hit lands on the selected zone; tg's 80% zone roll is not made
            obj_item_bodypart affecting = get_bodypart(get_random_valid_zone(user.zone_selected, 100) as string) as obj_item_bodypart;
            await apply_damage(damage, BRUTE, affecting);
            log_combat(user, this, "attacked");
        }
        else
        {
            playsound(loc, Dm.resource("sound/items/weapons/punchmiss.ogg"), 25, TRUE, -1);
            visible_message(span_danger(I18n.T("[]'s punch misses []!", user, this)),
                            span_danger(I18n.T("You avoid []'s punch!", user)), span_hear(I18n.T("You hear a swoosh!")), COMBAT_MESSAGE_RANGE, user);
            to_chat(user, span_warning(I18n.T("Your punch misses []!", this)));
        }
        return dot;
    }

    // tg: code/modules/mob/living/carbon/alien/adult/adult_defense.dm:41
    [DmParams("A", "visual_effect_icon", "used_item", "no_effect")]
    public override async DmTask do_attack_animation(atom attacked_atom = null, object visual_effect_icon = null, obj_item used_item = null, object no_effect = null, float? fov_effect = null, object item_animation_override = null)
    {
        if (!Dm.truthy(no_effect) && !Dm.truthy(visual_effect_icon))
            visual_effect_icon = ATTACK_EFFECT_CLAW;
        await base.do_attack_animation(attacked_atom, visual_effect_icon, used_item, no_effect, fov_effect, item_animation_override);
    }
}
