// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/supermatter/supermatter_hit_procs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M7 supermatter slice (G5, D163): the whole file. The emitter's beam energises the crystal (eat_bullets); touching and
// bumping it dust through /datum/component/supermatter_crystal (datums/components/supermatter_crystal.cs). The logs and admin
// messages are deferred (M10).

// tg: code/modules/power/supermatter/supermatter_hit_procs.dm:2
public partial class obj_machinery_power_supermatter_crystal : obj_machinery_power
{
    /// Consume things that run into the supermatter from the tram. The tram calls forceMove (doesn't call Bump/ed) and not Move, and I'm afraid changing it will do something chaotic
    // tg: code/modules/power/supermatter/supermatter_hit_procs.dm:2
    [SignalHandler]
    public virtual void tram_contents_consume(datum source = null, DmList tram_contents = null)
    {
        foreach (atom thing_to_consume in Dm.each_cast<atom>(tram_contents))
            Bumped(thing_to_consume as atom_movable);
    }

    // tg: code/modules/power/supermatter/supermatter_hit_procs.dm:8
    [SignalHandler]
    public virtual object eat_bullets(datum source = null, obj_projectile projectile = null)
    {
        turf local_turf = loc as turf;
        if (!(local_turf is turf))
            return NONE;

        float kiss_power = 0;
        if (projectile is obj_projectile_kiss_death)
            kiss_power = 20000;
        else if (projectile is obj_projectile_kiss)
            kiss_power = 60;


        if (!(projectile.firer is obj_machinery_power_emitter))
        {
            investigate_log(Dm.text("has been hit by [] fired by []", projectile, key_name(projectile.firer)), INVESTIGATE_ENGINE);
        }
        if (!Dm.eq(projectile.armor_flag, BULLET) || Dm.truthy(kiss_power))
        {
            if (Dm.truthy(kiss_power))
                psy_coeff = 1;
            log_activation(who: projectile.firer, how: projectile.fired_from);
        }
        else
        {
            external_damage_immediate += Dm.num(projectile.damage) * bullet_energy * 0.1f;
            // Stop taking damage at emergency point, yell to players at danger point.
            // This isn't clean and we are repeating [/obj/machinery/power/supermatter_crystal/proc/calculate_damage], sorry for this.
            float damage_to_be = Dm.num(damage) + external_damage_immediate * clamp((emergency_point - Dm.num(damage)) / emergency_point, 0, 1);
            if (damage_to_be > danger_point)
                visible_message(span_notice(I18n.T("[] compresses under stress, resisting further impacts!", this)));
            playsound(this, Dm.resource("sound/effects/supermatter.ogg"), 50, TRUE);
        }
        if (projectile is obj_projectile_beam_emitter_hitscan)
        {
            obj_projectile_beam_emitter_hitscan mahlaser = (obj_projectile_beam_emitter_hitscan)projectile;
            if (Dm.truthy(mahlaser?.integrity_heal))
                damage = max(0, Dm.num(damage) - Dm.num(mahlaser?.integrity_heal));
            if (Dm.truthy(mahlaser?.energy_reduction))
                internal_energy = max(0, internal_energy - Dm.num(mahlaser?.energy_reduction));
            if (Dm.truthy(mahlaser?.psi_change))
                psy_coeff = clamp(psy_coeff + Dm.num(mahlaser?.psi_change), 0, 1);
        }
        external_power_immediate += Dm.num(projectile.damage) * bullet_energy + kiss_power;
        if (projectile is obj_projectile_beam_emitter_hitscan_magnetic)
            absorption_ratio = clamp(absorption_ratio + 0.05f, 0.15f, 1);

        qdel(projectile);
        return COMPONENT_BULLET_BLOCKED;
    }

    // tg: code/modules/power/supermatter/supermatter_hit_procs.dm:57
    public override async DmTask<object> singularity_act()
    {
        float gain = 100;
        investigate_log("was consumed by a singularity.", INVESTIGATE_ENGINE);
        message_admins("Singularity has consumed a supermatter shard and can now become stage six.");
        visible_message(span_userdanger(I18n.T("[] is consumed by the singularity!", this)));
        turf sm_turf = get_turf(this);
        foreach (mob hearing_mob in Dm.each_cast<mob>(GLOB.player_list))
        {
            if (!Dm.truthy(is_valid_z_level(get_turf(hearing_mob), sm_turf)))
                continue;
            Dm.output_to(hearing_mob, Dm.resource("sound/effects/supermatter.ogg")); //everyone goan know bout this; SEND_SOUND (DIRECT_OUTPUT, rules 12)
            to_chat(hearing_mob, span_bolddanger(I18n.T("A horrible screeching fills your ears, and a wave of dread washes over you...")));
        }
        qdel(this);
        return gain;
    }

    // tg: code/modules/power/supermatter/supermatter_hit_procs.dm:71  (the /atom root, atom_act.dm, is not translated: the
    // slot's first declaration on this branch of the tree, as the live-partial stubs are)
    public override async DmTask<object> attack_tk(mob user = null)
    {
        if (!iscarbon(user))
            return null;
        mob_living_carbon jedi = (mob_living_carbon)user;
        to_chat(jedi, span_userdanger(I18n.T("That was a really dense idea.")));
        jedi.investigate_log(Dm.text("had [] brain dusted by touching [] with telekinesis.", jedi.p_their(), this), INVESTIGATE_DEATHS);
        jedi.ghostize();
        obj_item_organ_brain rip_u = Dm.locate<obj_item_organ_brain>(jedi.organs);
        if (Dm.truthy(rip_u))
        {
            await rip_u.Remove(jedi);
            qdel(rip_u);
        }
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/power/supermatter/supermatter_hit_procs.dm:84
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_scalpel_supermatter)
        {
            obj_item_scalpel_supermatter scalpel = (obj_item_scalpel_supermatter)tool;
            to_chat(user, span_notice(I18n.T("You carefully begin to scrape \\the [] with \\the []...", this, scalpel)));
            if (!Dm.truthy(await scalpel.use_tool(this, user, 60, volume: 100)))
                return ITEM_INTERACT_BLOCKING;
            if (!Dm.truthy(scalpel.usesLeft))
            {
                to_chat(user, span_warning(I18n.T("You fail to extract a sliver from \\the []! \\The [] isn't sharp enough anymore.", this, scalpel)));
                return ITEM_INTERACT_BLOCKING;
            }
            to_chat(user, span_danger(I18n.T("You extract a sliver from \\the []. \\The [] begins to react violently!", this, this)));
            Dm.@new<obj_item_nuke_core_supermatter_sliver>(this.drop_location());
            supermatter_sliver_removed = TRUE;
            external_power_trickle += 800;
            log_activation(who: user, how: scalpel);
            scalpel.usesLeft = Dm.num(scalpel.usesLeft) - 1;
            if (!Dm.truthy(scalpel.usesLeft))
                to_chat(user, span_notice(I18n.T("A tiny piece of \\the [] falls off, rendering it useless!", scalpel)));
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_hemostat_supermatter)
        {
            to_chat(user, span_warning(I18n.T("You poke [] with []'s hyper-noblium tips. Nothing happens.", this, tool)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (tool is obj_item_destabilizing_crystal)
        {
            obj_item_destabilizing_crystal destabilizing_crystal = (obj_item_destabilizing_crystal)tool;

            if (!Dm.truthy(is_main_engine))
            {
                to_chat(user, span_warning(I18n.T("You can't use \\the [] on \\a [].", destabilizing_crystal, name)));
                return ITEM_INTERACT_BLOCKING;
            }

            if (Dm.num(get_integrity_percent()) < SUPERMATTER_CASCADE_PERCENT)
            {
                to_chat(user, span_warning(I18n.T("You can only apply \\the [] to \\a [] that is at least []% intact.", destabilizing_crystal, name, SUPERMATTER_CASCADE_PERCENT)));
                return ITEM_INTERACT_BLOCKING;
            }

            to_chat(user, span_warning(I18n.T("You begin to attach \\the [] to \\the []...", destabilizing_crystal, this)));
            if (!Dm.truthy(await do_after(user, 3 * SECONDS, this)))
                return ITEM_INTERACT_BLOCKING;

            message_admins(Dm.text("[] attached [] to the supermatter at [].", ADMIN_LOOKUPFLW(user), destabilizing_crystal, ADMIN_VERBOSEJMP(this)));
            user.log_message(Dm.text("attached [] to the supermatter", destabilizing_crystal), LOG_GAME);
            user.investigate_log(Dm.text("attached [] to a supermatter crystal.", destabilizing_crystal), INVESTIGATE_ENGINE);
            to_chat(user, span_danger(I18n.T("\\The [] snaps onto \\the [].", destabilizing_crystal, this)));
            set_delam(SM_DELAM_PRIO_IN_GAME, typeof(datum_sm_delam_cascade));
            external_damage_immediate += 10;
            external_power_trickle += 500;
            log_activation(who: user, how: destabilizing_crystal);
            qdel(destabilizing_crystal);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    //Do not blow up our internal radio
    // tg: code/modules/power/supermatter/supermatter_hit_procs.dm:130
    public override async DmTask contents_explosion(object severity = null, object target = null)
    {
        return;
    }

    // tg: code/modules/power/supermatter/supermatter_hit_procs.dm:133
    public virtual async DmTask wrench_act_callback(mob user = null, obj_item tool = null)
    {
        if (Dm.truthy(moveable))
            await default_unfasten_wrench(user, tool);
    }

    // tg: code/modules/power/supermatter/supermatter_hit_procs.dm:137
    public virtual void consume_callback(object matter_increase = null, object damage_increase = null)
    {
        external_power_trickle += Dm.num(matter_increase);
        external_damage_immediate += Dm.num(damage_increase);
    }
}
