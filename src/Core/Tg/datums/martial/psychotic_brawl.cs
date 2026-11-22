// Translated from tgstation (snapshot 1d99bd7f): code/datums/martial/psychotic_brawl.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X3b (D316): the Psychotic Brawling of the violent psychosis trauma (a random help, cry, grab, headbutt, throw
// or a normal attack), whole. The var block is generated (D111).

// tg: code/datums/martial/psychotic_brawl.dm:1
public partial class datum_martial_art_psychotic_brawling : datum_martial_art
{
    // tg: code/datums/martial/psychotic_brawl.dm:6
    public override async DmTask<object> disarm_act(mob_living attacker = null, mob_living defender = null)
    {
        return await psycho_attack(attacker, defender);
    }

    // tg: code/datums/martial/psychotic_brawl.dm:9
    public override async DmTask<object> grab_act(mob_living attacker = null, mob_living defender = null)
    {
        return await psycho_attack(attacker, defender, TRUE);
    }

    // tg: code/datums/martial/psychotic_brawl.dm:12
    public override async DmTask<object> harm_act(mob_living attacker = null, mob_living defender = null)
    {
        return await psycho_attack(attacker, defender);
    }

    // tg: code/datums/martial/psychotic_brawl.dm:15
    public virtual async DmTask<object> psycho_attack(mob_living attacker = null, mob_living defender = null, object grab_attack = null)
    {
        string atk_verb = null;
        switch (rand(1, 8))
        {
            case 1:
                if (defender is mob_living_carbon helped_defender && attacker is mob_living_carbon carbon_attacker)
                    await helped_defender.help_shake_act(carbon_attacker);
                atk_verb = "helped";
                break;
            case 2:
                await attacker.emote("cry");
                await attacker.Stun(2 * SECONDS);
                atk_verb = "cried looking at";
                break;
            case 3:
                if (Dm.truthy(await defender.check_block(attacker, 0, Dm.text("[]'s grab", attacker), UNARMED_ATTACK)))
                    return MARTIAL_ATTACK_FAIL;
                if (Dm.eq(attacker.body_position, LYING_DOWN))
                    return MARTIAL_ATTACK_INVALID;

                if (Dm.num(attacker.grab_state) >= GRAB_AGGRESSIVE)
                {
                    await defender.grabbedby(attacker, 1);
                }
                else
                {
                    await attacker.start_pulling(defender, supress_message: TRUE);
                    if (attacker.pulling != null)
                    {
                        await defender.drop_all_held_items();
                        await defender.stop_pulling();
                        if (Dm.truthy(grab_attack))
                        {
                            log_combat(attacker, defender, "grabbed", addition: "aggressively");
                            defender.visible_message(
                                span_warning(I18n.T("[] violently grabs []!", attacker, defender)),
                                span_userdanger(I18n.T("You're violently grabbed by []!", attacker)),
                                span_hear(I18n.T("You hear sounds of aggressive fondling!")),
                                null,
                                attacker);
                            to_chat(attacker, span_danger(I18n.T("You violently grab []!", defender)));
                            await attacker.setGrabState(GRAB_AGGRESSIVE); //Instant aggressive grab
                        }
                        else
                        {
                            log_combat(attacker, defender, "grabbed", addition: "passively");
                            await attacker.setGrabState(GRAB_PASSIVE);
                        }
                    }
                }
                break;
            case 4:
            {
                atk_verb = "headbutt";
                float defender_damage = rand(5, 10);
                if (Dm.truthy(await defender.check_block(attacker, defender_damage, Dm.text("[]'s []", attacker, atk_verb), UNARMED_ATTACK)))
                    return MARTIAL_ATTACK_FAIL;

                await attacker.do_attack_animation(defender, ATTACK_EFFECT_PUNCH);
                await attacker.emote("flip");
                defender.visible_message(
                    span_danger(I18n.T("[] []s []!", attacker, atk_verb, defender)),
                    span_userdanger(I18n.T("You're []ed by []!", atk_verb, attacker)),
                    span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")),
                    null,
                    attacker);
                to_chat(attacker, span_danger(I18n.T("You [] []!", atk_verb, defender)));
                playsound(defender, "sound/items/weapons/punch1.ogg", 40, TRUE, -1);
                await defender.apply_damage(defender_damage, Dm.str(attacker.get_attack_type()), BODY_ZONE_HEAD);
                await attacker.apply_damage(rand(5, 10), Dm.str(attacker.get_attack_type()), BODY_ZONE_HEAD);
                if (defender is mob_living_carbon carbon_defender)
                {
                    if (carbon_defender.get_item_by_slot(ITEM_SLOT_HEAD) is not obj_item_clothing_head_helmet && carbon_defender.get_item_by_slot(ITEM_SLOT_HEAD) is not obj_item_clothing_head_utility_hardhat)
                        await carbon_defender.adjust_organ_loss(ORGAN_SLOT_BRAIN, 5);
                }
                await attacker.Stun(rand(1 * SECONDS, 4.5f * SECONDS));
                await defender.Stun(rand(0.5f * SECONDS, 3 * SECONDS));
                if (HAS_TRAIT(attacker, TRAIT_PACIFISM))
                    await attacker.add_mood_event("bypassed_pacifism", typeof(datum_mood_event_pacifism_bypassed));
                break;
            }
            case 5:
            case 6:
            {
                atk_verb = Dm.str(pick("kick", "hit", "slam"));
                if (Dm.truthy(await defender.check_block(attacker, 0, Dm.text("[]'s []", attacker, atk_verb), UNARMED_ATTACK)))
                    return MARTIAL_ATTACK_FAIL;

                await attacker.do_attack_animation(defender, ATTACK_EFFECT_PUNCH);
                defender.visible_message(
                    span_danger(I18n.T("[] []s [] with such inhuman strength that it sends [] flying backwards!", attacker, atk_verb, defender, defender.p_them())),
                    span_userdanger(I18n.T("You're []ed by [] with such inhuman strength that it sends you flying backwards!", atk_verb, attacker)),
                    span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")),
                    null,
                    attacker);
                to_chat(attacker, span_danger(I18n.T("You [] [] with such inhuman strength that it sends [] flying backwards!", atk_verb, defender, defender.p_them())));
                await defender.apply_damage(rand(15, 30), Dm.str(attacker.get_attack_type()));
                playsound(defender, "sound/effects/meteorimpact.ogg", 25, TRUE, -1);
                var throwtarget = get_edge_target_turf(attacker, get_dir(attacker, get_step_away(defender, attacker))) as atom;
                await defender.throw_at(throwtarget, 4, 2, attacker); //So stuff gets tossed around at the same time.
                await defender.Paralyze(6 * SECONDS);
                if (HAS_TRAIT(attacker, TRAIT_PACIFISM))
                    await attacker.add_mood_event("bypassed_pacifism", typeof(datum_mood_event_pacifism_bypassed));
                break;
            }
            case 7:
            case 8:
                return MARTIAL_ATTACK_INVALID; //Resume default behaviour
        }

        if (atk_verb != null)
        {
            log_combat(attacker, defender, Dm.text("[] (Psychotic Brawling)", atk_verb));
            return MARTIAL_ATTACK_SUCCESS;
        }

        return MARTIAL_ATTACK_FAIL;
    }
}
