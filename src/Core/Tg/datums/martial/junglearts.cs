// Translated from tgstation (snapshot 1d99bd7f): code/datums/martial/junglearts.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X3b (D316): the Jungle Arts (the tail drag, the tail whip and the bite), whole. Its source, the Monkey Dust
// (the inverse bath salts), is not ported (not-translated.md section 147). The var block is generated (D111).

// tg: code/datums/martial/junglearts.dm:1
public partial class datum_martial_art_jungle_arts : datum_martial_art
{
    // tg: code/datums/martial/junglearts.dm:6
    public override async DmTask<object> disarm_act(mob_living attacker = null, mob_living defender = null)
    {
        return await jungle_attack(attacker, defender);
    }

    // tg: code/datums/martial/junglearts.dm:9
    public override async DmTask<object> grab_act(mob_living attacker = null, mob_living defender = null)
    {
        return await jungle_attack(attacker, defender, TRUE);
    }

    // tg: code/datums/martial/junglearts.dm:12
    public override async DmTask<object> harm_act(mob_living attacker = null, mob_living defender = null)
    {
        return await jungle_attack(attacker, defender);
    }

    // tg: code/datums/martial/junglearts.dm:15
    public virtual async DmTask<object> jungle_attack(mob_living attacker = null, mob_living defender = null, object grab_attack = null)
    {
        string atk_verb = null;
        switch (rand(1, 6))
        {
            case 1:
            {
                atk_verb = "dragged";
                var tail = attacker.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL) as obj_item_organ_tail;
                if (tail == null && IS_UNCONSCIOUS_OR_CRIT(defender) || Dm.truthy(defender.IsParalyzed()))
                    return MARTIAL_ATTACK_INVALID;

                await attacker.do_attack_animation(defender, ATTACK_EFFECT_CLAW);
                await attacker.emote("spin");
                defender.visible_message(
                    span_danger(I18n.T("[]'s tail [] [] down to the ground!", attacker, atk_verb, defender)),
                    span_userdanger(I18n.T("Your body twists as you're [] to the ground by []'s tail!", atk_verb, attacker)),
                    span_hear(I18n.T("You hear a snap, followed by a thud!")),
                    null,
                    attacker);
                to_chat(attacker, span_danger(I18n.T("You latch your tail to [], [] them to the ground!", defender, atk_verb)));
                await defender.apply_damage(rand(5, 10), Dm.str(attacker.get_attack_type()));
                playsound(attacker, "sound/items/weapons/whip.ogg", 50, TRUE, -1);
                await defender.Knockdown(2 * SECONDS);
                if (HAS_TRAIT(attacker, TRAIT_PACIFISM))
                    await attacker.add_mood_event("bypassed_pacifism", typeof(datum_mood_event_pacifism_bypassed));
                break;
            }

            case 6:
            {
                var tail = attacker.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL) as obj_item_organ_tail;
                if (tail == null)
                    return MARTIAL_ATTACK_INVALID;

                atk_verb = Dm.str(pick("whipped", "flogged", "lashed"));
                await attacker.do_attack_animation(defender, ATTACK_EFFECT_CLAW);
                defender.visible_message(
                    span_danger(I18n.T("[]'s tail [] [] in one quick motion!", attacker, atk_verb, defender)),
                    span_userdanger(I18n.T("You feel a sharp sting as you're [] by []!", atk_verb, attacker)),
                    span_hear(I18n.T("You hear a sharp whipping noise!")),
                    null,
                    attacker);
                to_chat(attacker, span_danger(I18n.T("In one motion, you [] [] quickly!", atk_verb, defender)));
                await defender.apply_damage(rand(10, 15), Dm.str(attacker.get_attack_type()));
                playsound(attacker, "sound/items/weapons/whip.ogg", 50, TRUE, -1);
                await defender.drop_all_held_items();
                if (HAS_TRAIT(attacker, TRAIT_PACIFISM))
                    await attacker.add_mood_event("bypassed_pacifism", typeof(datum_mood_event_pacifism_bypassed));
                break;
            }

            default:
            {
                atk_verb = Dm.str(pick("chomp", "gnaw", "chew"));
                if (Dm.truthy(await defender.check_block(attacker, 0, Dm.text("[]'s []", attacker, atk_verb), UNARMED_ATTACK)))
                    return MARTIAL_ATTACK_FAIL;

                await attacker.do_attack_animation(defender, ATTACK_EFFECT_BITE);
                defender.visible_message(
                    span_danger(I18n.T("[] []s [] violently!", attacker, atk_verb, defender)),
                    span_userdanger(I18n.T("You're viciously []ed by []!", atk_verb, attacker)),
                    span_hear(I18n.T("You hear a violent gnawing sound!")),
                    null,
                    attacker);
                to_chat(attacker, span_danger(I18n.T("You [] [] with vicious force!", atk_verb, defender)));
                await defender.apply_damage(rand(10, 20), damagetype: BRUTE, sharpness: SHARP_POINTY, wound_bonus: 50);
                playsound(attacker, "sound/items/weapons/bite.ogg", 50, TRUE, -1);
                if (HAS_TRAIT(attacker, TRAIT_PACIFISM))
                    await attacker.add_mood_event("bypassed_pacifism", typeof(datum_mood_event_pacifism_bypassed));
                break;
            }
        }

        if (atk_verb != null)
        {
            log_combat(attacker, defender, Dm.text("[] (Jungle Arts)", atk_verb));
            return MARTIAL_ATTACK_SUCCESS;
        }

        return MARTIAL_ATTACK_FAIL;
    }
}
