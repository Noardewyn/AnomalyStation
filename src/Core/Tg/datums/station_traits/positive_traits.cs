// Translated from tgstation (snapshot 1d99bd7f): code/datums/station_traits/positive_traits.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R2 (D327): the whole file: the positive station traits, the repair technician's ID and its trim (the loaner shuttle's and the
// wise cows' events came with gaps E3 and E4). What some of them hand out is partly not ported yet and arrives as props
// (not-translated.md section 183). The var blocks are generated (D111).

// tg: code/datums/station_traits/positive_traits.dm:4
public partial class datum_station_trait_lucky_winner : datum_station_trait
{
    // tg: code/datums/station_traits/positive_traits.dm:1
    private const float PARTY_COOLDOWN_LENGTH_MIN = 6 * MINUTES;
    // tg: code/datums/station_traits/positive_traits.dm:2
    private const float PARTY_COOLDOWN_LENGTH_MAX = 12 * MINUTES;

    // tg: code/datums/station_traits/positive_traits.dm:13
    public override void on_round_start()
    {
        base.on_round_start();
        party_cooldown = world.time + rand(PARTY_COOLDOWN_LENGTH_MIN, PARTY_COOLDOWN_LENGTH_MAX); // COOLDOWN_START
    }

    // tg: code/datums/station_traits/positive_traits.dm:17
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(seconds_per_tick), null); // the slot's set waitfor = FALSE (processing.dm:48)
    }

    private async DmTask<object> process__body(object seconds_per_tick)
    {
        if (!(party_cooldown <= world.time)) // COOLDOWN_FINISHED
            return null;

        party_cooldown = world.time + rand(PARTY_COOLDOWN_LENGTH_MIN, PARTY_COOLDOWN_LENGTH_MAX); // COOLDOWN_START

        DmList landing_turfs = Dm.list();
        foreach (turf_open_floor bar_turf in Dm.each<turf_open_floor>(get_area_turfs(typeof(area_station_service_bar), subtypes: TRUE)))
            Dm.append(ref landing_turfs, bar_turf);
        if (!Dm.truthy(length(landing_turfs)))
            return null;

        object pizza_type_to_spawn = pick(Dm.list(
            typeof(obj_item_pizzabox_margherita),
            typeof(obj_item_pizzabox_mushroom),
            typeof(obj_item_pizzabox_meat),
            typeof(obj_item_pizzabox_vegetable),
            typeof(obj_item_pizzabox_pineapple)));

        await podspawn(Dm.list(
            Dm.kv("target", pick(landing_turfs)),
            Dm.kv("path", typeof(obj_structure_closet_supplypod_centcompod)),
            Dm.kv("spawn", Dm.list(
                pizza_type_to_spawn,
                Dm.kv(typeof(obj_item_reagent_containers_cup_glass_bottle_beer), 6)))));
        return null;
    }
}

// tg: code/datums/station_traits/positive_traits.dm:49
public partial class datum_station_trait_galactic_grant : datum_station_trait
{
    // tg: code/datums/station_traits/positive_traits.dm:56
    public override void on_round_start()
    {
        datum_bank_account cargo_bank = SSeconomy.get_dep_account(ACCOUNT_CAR);
        cargo_bank.adjust_money(rand(2000, 5000));
    }
}

// tg: code/datums/station_traits/positive_traits.dm:68
public partial class datum_station_trait_bountiful_bounties : datum_station_trait
{
    // tg: code/datums/station_traits/positive_traits.dm:76
    public override void on_round_start()
    {
        SSeconomy.bounty_modifier *= 1.2f;
    }
}

///A positive station trait that scatters a bunch of lit glowsticks throughout maintenance
// tg: code/datums/station_traits/positive_traits.dm:80
public partial class datum_station_trait_glowsticks : datum_station_trait
{
    // tg: code/datums/station_traits/positive_traits.dm:87
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        RegisterSignal(SSticker, COMSIG_TICKER_ENTER_PREGAME, nameof(on_pregame));
    }

    // tg: code/datums/station_traits/positive_traits.dm:91  SIGNAL_HANDLER
    public virtual void on_pregame(datum source = null)
    {
        Waiting.Detach(light_up_the_night()); // INVOKE_ASYNC
    }

    // tg: code/datums/station_traits/positive_traits.dm:95
    public virtual async DmTask light_up_the_night()
    {
        DmList glowsticks = Dm.list(
            typeof(obj_item_flashlight_glowstick),
            typeof(obj_item_flashlight_glowstick_red),
            typeof(obj_item_flashlight_glowstick_blue),
            typeof(obj_item_flashlight_glowstick_cyan),
            typeof(obj_item_flashlight_glowstick_orange),
            typeof(obj_item_flashlight_glowstick_yellow),
            typeof(obj_item_flashlight_glowstick_pink));
        foreach (area_station_maintenance maint in Dm.each<area_station_maintenance>(GLOB.areas))
        {
            DmList turfs = get_area_turfs(maint) as DmList;
            float picks = round(length(turfs) * 0.115f);
            for (float i = 1; i <= picks; i++)
            {
                CHECK_TICK();
                var chosen = pick_n_take(turfs) as turf_open;
                if (!istype(chosen, typeof(turf_open)))
                    continue;
                bool skip_this = false;
                foreach (atom_movable mov in Dm.each_cast<atom_movable>(chosen)) //stop glowing sticks from spawning on windows
                {
                    if (Dm.truthy(mov.density) && !Dm.truthy(Dm.bitand(mov.pass_flags_self, LETPASSTHROW)))
                    {
                        skip_this = true;
                        break;
                    }
                }
                if (skip_this)
                    continue;
                if (prob(3.4f)) ///Rare, but this is something that can survive past the lifespawn of glowsticks.
                {
                    Dm.@new<obj_machinery_light_floor>(chosen);
                    continue;
                }
                var stick_type = (Type)pick(glowsticks);
                var stick = Dm.@new(stick_type, chosen, rand(10, 45)) as obj_item_flashlight_glowstick;
                ///we want a wider range, otherwise they'd all burn out in about 20 minutes.
                await stick.turn_on();
            }
        }
    }
}

// tg: code/datums/station_traits/positive_traits.dm:127
public partial class datum_station_trait_strong_supply_lines : datum_station_trait
{
    // tg: code/datums/station_traits/positive_traits.dm:135
    public override void on_round_start()
    {
        SSeconomy.pack_price_modifier *= 0.8f;
    }
}

// tg: code/datums/station_traits/positive_traits.dm:160
public partial class datum_station_trait_deathrattle_department : datum_station_trait
{
    // tg: code/datums/station_traits/positive_traits.dm:171
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        deathrattle_group = Dm.@new<datum_deathrattle_group>(Dm.text("[] group", department_name));
        Dm.append(ref blacklist, Dm.sub(subtypesof(typeof(datum_station_trait_deathrattle_department)), type)); //All but ourselves
        report_message = I18n.T("All members of [] have received an implant to notify each other if one of them dies. This should help improve job-safety!", department_name);
        RegisterSignal(SSdcs, COMSIG_GLOB_JOB_AFTER_SPAWN, nameof(on_job_after_spawn));
    }

    // tg: code/datums/station_traits/positive_traits.dm:179  SIGNAL_HANDLER
    public virtual void on_job_after_spawn(datum source = null, datum_job job = null, mob_living spawned = null, client player_client = null)
    {
        if (!Dm.truthy(Dm.bitand(job.departments_bitflags, department_to_apply_to)))
            return;

        var implant_to_give = Dm.@new<obj_item_implant_deathrattle>();
        deathrattle_group.register(implant_to_give);
        Waiting.NoSleep(implant_to_give.implant(spawned, spawned, TRUE, TRUE));
    }
}

// tg: code/datums/station_traits/positive_traits.dm:232
public partial class datum_station_trait_deathrattle_all : datum_station_trait
{
    // tg: code/datums/station_traits/positive_traits.dm:240
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        deathrattle_group = Dm.@new<datum_deathrattle_group>("station group");
        blacklist = subtypesof(typeof(datum_station_trait_deathrattle_department));
        RegisterSignal(SSdcs, COMSIG_GLOB_JOB_AFTER_SPAWN, nameof(on_job_after_spawn));
    }

    // tg: code/datums/station_traits/positive_traits.dm:246  SIGNAL_HANDLER
    public virtual void on_job_after_spawn(datum source = null, datum_job job = null, mob_living spawned = null, client player_client = null)
    {
        var implant_to_give = Dm.@new<obj_item_implant_deathrattle>();
        deathrattle_group.register(implant_to_give);
        Waiting.NoSleep(implant_to_give.implant(spawned, spawned, TRUE, TRUE));
    }
}

// tg: code/datums/station_traits/positive_traits.dm:253
public partial class datum_station_trait_cybernetic_revolution : datum_station_trait
{
    /// List of all job types with the cybernetics they should receive.
    // tg: code/datums/station_traits/positive_traits.dm:261  var/static/list/job_to_cybernetic
    public static ref DmList job_to_cybernetic => ref TgState.Current.datum_station_trait_cybernetic_revolution__job_to_cybernetic;

    // tg: code/datums/station_traits/positive_traits.dm:300
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        RegisterSignal(SSdcs, COMSIG_GLOB_JOB_AFTER_SPAWN, nameof(on_job_after_spawn));
    }

    // tg: code/datums/station_traits/positive_traits.dm:304  SIGNAL_HANDLER
    public virtual void on_job_after_spawn(datum source = null, datum_job job = null, mob_living spawned = null, client player_client = null)
    {
        Type body_purist = typeof(datum_quirk_body_purist);
        if (Dm.@in(Dm.initial(body_purist, (datum_quirk p) => p.name), player_client.prefs.all_quirks))
            return;
        var cybernetic_type = job_to_cybernetic[job.type] as Type;
        if (cybernetic_type == null)
        {
            if (isAI(spawned))
            {
                var ai = (mob_living_silicon_ai)spawned;
                ai.eyeobj.relay_speech = TRUE; //surveillance upgrade. the ai gets cybernetics too.
            }
            return;
        }
        // the organs not ported yet (inert: the coroner's bone tongue, the prisoner's shielded eyes, the cargo technician's tier 2 and
        // the quartermaster's tier 3 stomach, the research director's BCI, the shaft miner's rush gland) wait for their mechanics
        if (InertSeam.IsInertType(cybernetic_type))
        {
            Dm.Deferred("code/datums/station_traits/positive_traits.dm:316", "cybernetic_revolution on_job_after_spawn: an organ not ported yet (inert)");
            return;
        }
        var cybernetic = Dm.@new(cybernetic_type) as obj_item_organ;
        Waiting.NoSleep(cybernetic.Insert(spawned as mob_living_carbon, special: TRUE, movement_flags: DELETE_IF_REPLACED));
    }
}

public sealed partial class TgState
{
    // tg: code/datums/station_traits/positive_traits.dm:261
    internal DmList datum_station_trait_cybernetic_revolution__job_to_cybernetic = Dm.list(
        Dm.kv(typeof(datum_job_assistant), typeof(obj_item_organ_heart_cybernetic)), //real cardiac
        Dm.kv(typeof(datum_job_atmospheric_technician), typeof(obj_item_organ_cyberimp_mouth_breathing_tube)),
        Dm.kv(typeof(datum_job_bartender), typeof(obj_item_organ_liver_cybernetic_tier3)),
        Dm.kv(typeof(datum_job_bitrunner), typeof(obj_item_organ_eyes_robotic_thermals)),
        Dm.kv(typeof(datum_job_botanist), typeof(obj_item_organ_cyberimp_chest_nutriment)),
        Dm.kv(typeof(datum_job_captain), typeof(obj_item_organ_heart_cybernetic_tier3)),
        Dm.kv(typeof(datum_job_cargo_technician), typeof(obj_item_organ_stomach_cybernetic_tier2)),
        Dm.kv(typeof(datum_job_chaplain), typeof(obj_item_organ_cyberimp_brain_anti_drop)),
        Dm.kv(typeof(datum_job_chemist), typeof(obj_item_organ_liver_cybernetic_tier2)),
        Dm.kv(typeof(datum_job_chief_engineer), typeof(obj_item_organ_cyberimp_chest_thrusters)),
        Dm.kv(typeof(datum_job_chief_medical_officer), typeof(obj_item_organ_cyberimp_chest_reviver)),
        Dm.kv(typeof(datum_job_clown), typeof(obj_item_organ_cyberimp_brain_anti_stun)), //HONK!
        Dm.kv(typeof(datum_job_cook), typeof(obj_item_organ_cyberimp_chest_nutriment_plus)),
        Dm.kv(typeof(datum_job_coroner), typeof(obj_item_organ_tongue_bone)), //hes got a bone to pick with you
        Dm.kv(typeof(datum_job_curator), typeof(obj_item_organ_cyberimp_brain_connector)),
        Dm.kv(typeof(datum_job_detective), typeof(obj_item_organ_lungs_cybernetic_tier3)),
        Dm.kv(typeof(datum_job_doctor), typeof(obj_item_organ_cyberimp_arm_toolkit_surgery)),
        Dm.kv(typeof(datum_job_geneticist), typeof(obj_item_organ_fly)), //we don't care about implants, we have cancer.
        Dm.kv(typeof(datum_job_head_of_personnel), typeof(obj_item_organ_eyes_robotic)),
        Dm.kv(typeof(datum_job_head_of_security), typeof(obj_item_organ_eyes_robotic_thermals)),
        Dm.kv(typeof(datum_job_human_ai), typeof(obj_item_organ_brain_cybernetic)),
        Dm.kv(typeof(datum_job_janitor), typeof(obj_item_organ_eyes_robotic_xray)),
        Dm.kv(typeof(datum_job_lawyer), typeof(obj_item_organ_heart_cybernetic_tier2)),
        Dm.kv(typeof(datum_job_mime), typeof(obj_item_organ_tongue_robot)), //...
        Dm.kv(typeof(datum_job_paramedic), typeof(obj_item_organ_cyberimp_eyes_hud_medical)),
        Dm.kv(typeof(datum_job_prisoner), typeof(obj_item_organ_eyes_robotic_shield)),
        Dm.kv(typeof(datum_job_psychologist), typeof(obj_item_organ_ears_cybernetic_whisper)),
        Dm.kv(typeof(datum_job_pun_pun), typeof(obj_item_organ_cyberimp_arm_strongarm)),
        Dm.kv(typeof(datum_job_quartermaster), typeof(obj_item_organ_stomach_cybernetic_tier3)),
        Dm.kv(typeof(datum_job_research_director), typeof(obj_item_organ_cyberimp_bci)),
        Dm.kv(typeof(datum_job_roboticist), typeof(obj_item_organ_cyberimp_eyes_hud_diagnostic)),
        Dm.kv(typeof(datum_job_scientist), typeof(obj_item_organ_ears_cybernetic)),
        Dm.kv(typeof(datum_job_security_officer), typeof(obj_item_organ_cyberimp_arm_toolkit_flash)),
        Dm.kv(typeof(datum_job_shaft_miner), typeof(obj_item_organ_monster_core_rush_gland)),
        Dm.kv(typeof(datum_job_station_engineer), typeof(obj_item_organ_cyberimp_arm_toolkit_toolset)),
        Dm.kv(typeof(datum_job_warden), typeof(obj_item_organ_cyberimp_eyes_hud_security)));
}

// tg: code/datums/station_traits/positive_traits.dm:337
public partial class datum_station_trait_random_event_weight_modifier_shuttle_loans : datum_station_trait_random_event_weight_modifier
{
}

// tg: code/datums/station_traits/positive_traits.dm:347
public partial class datum_station_trait_random_event_weight_modifier_wise_cows : datum_station_trait_random_event_weight_modifier
{
    // tg: code/datums/station_traits/positive_traits.dm:356
    public override object get_pulsar_message()
    {
        object advisory_string = I18n.T("Advisory Level: <b>Cow Planet</b></center><BR>"); //We're gonna go fast and we're gonna go far.
        advisory_string = Dm.add(advisory_string, I18n.T("Your sector's advisory level is Cow Planet. We don't really know what this one means -- the model we use to create these threat reports hasn't produced this result before. Watch out for cows, I guess? Good luck!"));
        return advisory_string;
    }
}

// tg: code/datums/station_traits/positive_traits.dm:378
public partial class datum_station_trait_missing_wallet : datum_station_trait
{
    // tg: code/datums/station_traits/positive_traits.dm:386
    public override void on_round_start()
    {
        base.on_round_start();

        var locker_to_fill = pick(GLOB.roundstart_station_closets) as obj_structure_closet;

        var new_wallet = Dm.@new<obj_item_storage_wallet>(locker_to_fill);

        Dm.@new<obj_item_stack_spacecash_c500>(new_wallet);
        if (prob(25)) //Jackpot!
            Dm.@new<obj_item_stack_spacecash_c1000>(new_wallet);

        Dm.@new<obj_item_card_id_advanced_technician_id>(new_wallet);
        new_wallet.refreshID();

        if (prob(35))
            report_message = Dm.add(report_message, I18n.T(" The technician reports they last remember having their wallet around [].", get_area_name(new_wallet)));

        message_admins(Dm.text("A missing wallet has been placed in the [] locker, in the [] area.", locker_to_fill, get_area_name(locker_to_fill)));
    }
}

// tg: code/datums/station_traits/positive_traits.dm:428
public partial class datum_station_trait_random_event_weight_modifier_assistant_gimmicks : datum_station_trait_random_event_weight_modifier
{
    // tg: code/datums/station_traits/positive_traits.dm:428
    public override object get_pulsar_message()
    {
        object advisory_string = I18n.T("Advisory Level: <b>Grey Sky</b></center><BR>");
        advisory_string = Dm.add(advisory_string, I18n.T("Your sector's advisory level is Grey Sky. Our sensors detect abnormal activity among the assistants assigned to your station. We advise you to closely monitor the Tool Storage, Bridge, Tech Storage, and Brig for gathering crowds or petty thievery."));
        return advisory_string;
    }
}
