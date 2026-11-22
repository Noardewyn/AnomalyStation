// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/wizarditis.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111). DV560: a spell the disease picks whose own code is not
// ported yet (the wizard's spells wait with the antagonists, D333) is not cast: its cast is a Dm.Deferred, so an inert spell never
// runs the base spell's code where tg runs its own (D112). The ported ones (the teleports, blink, smoke, repulse) cast as in tg.

// tg: code/datums/diseases/wizarditis.dm:1
public partial class datum_disease_wizarditis : datum_disease
{
    /// DV560: whether a spell's own code is ported (its class is not [DmInert]).
    private static bool spell_is_ported(datum spell) => spell != null && !spell.GetType().IsDefined(typeof(DmInertAttribute), false);

    // tg: code/datums/diseases/wizarditis.dm:31
    public override async DmTask after_add()
    {
        object sw__1 = pick(Dm.list("blue", "red", "yellow"));
        if (Dm.eq(sw__1, "blue"))
        {
            hat_type = typeof(obj_item_clothing_head_wizard);
            robe_type = typeof(obj_item_clothing_suit_wizrobe);
        }
        else if (Dm.eq(sw__1, "red"))
        {
            hat_type = typeof(obj_item_clothing_head_wizard_red);
            robe_type = typeof(obj_item_clothing_suit_wizrobe_red);
        }
        else if (Dm.eq(sw__1, "yellow"))
        {
            hat_type = typeof(obj_item_clothing_head_wizard_yellow);
            robe_type = typeof(obj_item_clothing_suit_wizrobe_yellow);
        }

        await init_spells();
    }

    // tg: code/datums/diseases/wizarditis.dm:45
    public override object Destroy(float? force = null)
    {
        QDEL_LIST(random_spells);
        QDEL_LIST(random_targeted_spells);
        return base.Destroy(force);
    }

    // tg: code/datums/diseases/wizarditis.dm:50
    public override async DmTask<object> stage_act(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.stage_act(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;

        if (Dm.truthy(affected_mob.can_block_magic(charge_cost: 0)))
        {
            await update_stage(1);
            return dot;
        }

        if (stage >= 3 && SPT_PROB(0.15f * stage, seconds_per_tick))
        {
            datum_action_cooldown_spell picked = (datum_action_cooldown_spell)pick(random_spells);
            if (!spell_is_ported(picked))
            {
                Dm.Deferred("code/datums/diseases/wizarditis.dm:61", "stage_act: casting a wizard's spell whose code is not ported (DV560, D333)");
                return dot;
            }
            if (!Dm.truthy(await picked.try_invoke(affected_mob, feedback: FALSE)))
            {
                to_chat(affected_mob, span_danger(I18n.T("You feel something building up inside... but the feeling passes.")));
                return dot;
            }

            await picked.spell_feedback(affected_mob);
            return dot;
        }

        if (stage <= 3 && SPT_PROB(0.33f * stage, seconds_per_tick))
            affected_mob.manual_emote(I18n.T("sniffles."));

        if (stage == 2)
        {
            if (SPT_PROB(1, seconds_per_tick))
                to_chat(affected_mob, span_danger(I18n.T("You feel [].", I18n.T(Dm.str(pick(Dm.list("that you don't have enough mana", "that the winds of magic are gone", "an urge to summon familiar")))))));
        }

        else if (stage == 3)
        {
            if (SPT_PROB(1, seconds_per_tick))
            {
                to_chat(affected_mob, span_danger(I18n.T("You feel [].", I18n.T(Dm.str(pick(Dm.list("the magic bubbling in your veins", "that this location gives you a +1 to INT", "an urge to summon familiar")))))));
                await spawn_wizard_clothes(10);
            }
        }

        else if (stage == 4)
        {
            if (SPT_PROB(1, seconds_per_tick))
            {
                to_chat(affected_mob, span_danger(I18n.T("You feel [].", I18n.T(Dm.str(pick(Dm.list("the tidal wave of raw power building inside", "that this location gives you a +2 to INT and +1 to WIS", "an urge to teleport")))))));
                await spawn_wizard_clothes(50);
            }

            if (SPT_PROB(0.2f, seconds_per_tick))
            {
                if (prob(15))
                {
                    DmList targets = Dm.list();
                    datum_action_cooldown_spell target_picked = (datum_action_cooldown_spell)pick(random_targeted_spells);
                    foreach (mob_living potential_target in Dm.each<mob_living>(view(affected_mob)))
                    {
                        if (potential_target == affected_mob)
                            continue;
                        Dm.append(targets, potential_target);
                    }

                    if (Dm.truthy(length(targets)))
                    {
                        if (spell_is_ported(target_picked))
                            await target_picked.Activate(pick(targets) as atom);
                        else
                            Dm.Deferred("code/datums/diseases/wizarditis.dm:96", "stage_act: casting a wizard's targeted spell whose code is not ported (DV560, D333)");
                        await affected_mob.emote("cough");
                        return dot;
                    }
                }

                datum_action_cooldown_spell picked = (datum_action_cooldown_spell)pick(random_spells);
                if (spell_is_ported(picked))
                    await picked.Activate(affected_mob);
                else
                    Dm.Deferred("code/datums/diseases/wizarditis.dm:101", "stage_act: casting a wizard's spell whose code is not ported (DV560, D333)");
                await affected_mob.emote("sneeze");
                return dot;
            }
        }
        return dot;
    }

    // tg: code/datums/diseases/wizarditis.dm:105
    public virtual async DmTask spawn_wizard_clothes(float? chance = null)
    {
        chance ??= 0;
        if (prob(Dm.num(chance)) && affected_mob.usable_hands >= 1)
        {
            obj_item_staff funny_staff = Dm.@new<obj_item_staff>(affected_mob);
            if (!Dm.truthy(await affected_mob.put_in_hands(funny_staff)))
                qdel(funny_staff);
        }

        if (!ishuman(affected_mob))
            return;


        mob_living_carbon_human human_mob = (mob_living_carbon_human)affected_mob;
        if (prob(Dm.num(chance)) && !HAS_TRAIT(human_mob.get_item_by_slot(ITEM_SLOT_HEAD), TRAIT_CASTING_CLOTHING))
        {
            if (Dm.truthy(await human_mob.dropItemToGround(human_mob.head)))
                await human_mob.equip_to_slot_or_del(Dm.@new(hat_type, human_mob) as obj_item, ITEM_SLOT_HEAD);
        }

        if (prob(Dm.num(chance)) && !HAS_TRAIT(human_mob.get_item_by_slot(ITEM_SLOT_OCLOTHING), TRAIT_CASTING_CLOTHING))
        {
            if (Dm.truthy(await human_mob.dropItemToGround(human_mob.wear_suit)))
                await human_mob.equip_to_slot_or_del(Dm.@new(robe_type, human_mob) as obj_item, ITEM_SLOT_OCLOTHING);
        }

        if (prob(Dm.num(chance)) && !(human_mob.shoes is obj_item_clothing_shoes_sandal_magic))
        {
            if (Dm.truthy(await human_mob.dropItemToGround(human_mob.shoes)))
                await human_mob.equip_to_slot_or_del(Dm.@new<obj_item_clothing_shoes_sandal_magic>(human_mob), ITEM_SLOT_FEET);
        }
    }

    // tg: code/datums/diseases/wizarditis.dm:128
    public virtual async DmTask init_spells()
    {
        // Some self cast spells
        datum_action_cooldown_spell_teleport_area_teleport_wizard sneeze_teleport = await Dm.new_async<datum_action_cooldown_spell_teleport_area_teleport_wizard>(this);
        sneeze_teleport.randomise_selection = TRUE;
        Dm.append(random_spells, sneeze_teleport);

        datum_action_cooldown_spell_emp_disable_tech sneeze_emp = await Dm.new_async<datum_action_cooldown_spell_emp_disable_tech>(this);
        sneeze_emp.emp_heavy = 1;
        sneeze_emp.emp_light = 2;
        Dm.append(random_spells, sneeze_emp);

        datum_action_cooldown_spell_apply_mutations_mutate sneeze_mutate = await Dm.new_async<datum_action_cooldown_spell_apply_mutations_mutate>(this);
        sneeze_mutate.mutation_duration = 3 * SECONDS;
        Dm.append(random_spells, sneeze_mutate);

        datum_action_cooldown_spell_aoe_knock sneeze_knock = await Dm.new_async<datum_action_cooldown_spell_aoe_knock>(this);
        Dm.append(random_spells, sneeze_knock);

        datum_action_cooldown_spell_forcewall sneeze_forcewall = await Dm.new_async<datum_action_cooldown_spell_forcewall>(this);
        Dm.append(random_spells, sneeze_forcewall);

        datum_action_cooldown_spell_teleport_radius_turf_blink sneeze_blink = await Dm.new_async<datum_action_cooldown_spell_teleport_radius_turf_blink>(this);
        sneeze_blink.inner_tele_radius = 1;
        sneeze_blink.outer_tele_radius = 3;
        Dm.append(random_spells, sneeze_blink);

        datum_action_cooldown_spell_smoke sneeze_smoke = await Dm.new_async<datum_action_cooldown_spell_smoke>(this);
        sneeze_smoke.smoke_amt = 2;
        Dm.append(random_spells, sneeze_smoke);

        datum_action_cooldown_spell_spacetime_dist sneeze_spacetime = await Dm.new_async<datum_action_cooldown_spell_spacetime_dist>(this);
        sneeze_spacetime.scramble_radius = 2;
        sneeze_spacetime.duration = 5 * SECONDS;
        Dm.append(random_spells, sneeze_spacetime);

        datum_action_cooldown_spell_timestop sneeze_timestop = await Dm.new_async<datum_action_cooldown_spell_timestop>(this);
        sneeze_timestop.timestop_range = 0; // heh
        sneeze_timestop.timestop_duration = 5 * SECONDS;
        sneeze_timestop.owner_is_immune_to_all_timestop = FALSE;
        sneeze_timestop.owner_is_immune_to_self_timestop = FALSE;
        Dm.append(random_spells, sneeze_timestop);

        datum_action_cooldown_spell_aoe_repulse sneeze_repulse = await Dm.new_async<datum_action_cooldown_spell_aoe_repulse>(this);
        sneeze_repulse.aoe_radius = 2;
        sneeze_repulse.max_throw = 3;
        Dm.append(random_spells, sneeze_repulse);

        // Some targeted spells
        datum_action_cooldown_spell_pointed_blind cough_blind = await Dm.new_async<datum_action_cooldown_spell_pointed_blind>(this);
        cough_blind.eye_blind_duration = 2 * SECONDS;
        cough_blind.eye_blur_duration = 10 * SECONDS;
        Dm.append(random_targeted_spells, cough_blind);

        datum_action_cooldown_spell_pointed_projectile_lightningbolt cough_zap = await Dm.new_async<datum_action_cooldown_spell_pointed_projectile_lightningbolt>(this);
        cough_zap.bolt_range = Dm.num(Dm.div(cough_zap.bolt_range, 3));
        cough_zap.bolt_power = Dm.num(Dm.div(cough_zap.bolt_power, 3));
        Dm.append(random_targeted_spells, cough_zap);

        datum_action_cooldown_spell_pointed_swap cough_swap = await Dm.new_async<datum_action_cooldown_spell_pointed_swap>(this);
        Dm.append(random_targeted_spells, cough_swap);
    }
}
