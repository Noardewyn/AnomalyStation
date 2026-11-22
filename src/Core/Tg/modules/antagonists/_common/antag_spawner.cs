// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/_common/antag_spawner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (syndicate package A1, D316): the abstract base /obj/item/antag_spawner; syndicate G4c: the loadout beacons (their base, the
// contractor's support beacon's greeting and the monkey agent beacon, with its outfit); syndicate N2c: the nuclear operatives'
// reinforcement and overwatch beacons. The other subtypes (the wizard's contract, the clown operatives' beacon, the cyborg teleporters,
// the slaughter demon's bottle) come with their packages (N3, the silicons; not-translated.md section 130).

// tg: code/modules/antagonists/_common/antag_spawner.dm:1
public partial class obj_item_antag_spawner : obj_item
{
    // tg: code/modules/antagonists/_common/antag_spawner.dm:8
    // kind is any value (decided, syndicate G4c: the loadout beacons pass their user there, antag_spawner.dm:361)
    public virtual async DmTask spawn_antag(client C = null, turf T = null, object kind = null, datum_mind user = null)
    {
        kind ??= "";
        return;
    }

    // tg: code/modules/antagonists/_common/antag_spawner.dm:11
    public virtual void equip_antag(mob target = null)
    {
        return;
    }
}

/**
 * Device to request reinforcments from ghost pop
 */
// tg: code/modules/antagonists/_common/antag_spawner.dm:103
public partial class obj_item_antag_spawner_nuke_ops : obj_item_antag_spawner
{
    // tg: code/modules/antagonists/_common/antag_spawner.dm:121
    public virtual object check_usability(mob user = null)
    {
        if (Dm.truthy(used))
        {
            to_chat(user, span_warning(I18n.T("[] is out of power!", this)));
            return FALSE;
        }
        if (!Dm.truthy(user.mind.has_antag_datum(typeof(datum_antagonist_nukeop), TRUE)))
        {
            to_chat(user, span_danger(I18n.T("AUTHENTICATION FAILURE. ACCESS DENIED.")));
            return FALSE;
        }
        return TRUE;
    }

    /// Creates the drop pod the nukie will be dropped by
    // tg: code/modules/antagonists/_common/antag_spawner.dm:131
    public virtual obj_structure_closet_supplypod setup_pod()
    {
        obj_structure_closet_supplypod pod = Dm.@new<obj_structure_closet_supplypod>(null, pod_style);
        pod.explosionSize = Dm.list(0, 0, 0, 0);
        pod.bluespace = TRUE;
        return pod;
    }

    // tg: code/modules/antagonists/_common/antag_spawner.dm:137
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(check_usability(user)))
            return null;

        to_chat(user, span_notice(I18n.T("You activate [] and wait for confirmation.", this)));
        mob chosen_one = await SSpolling.poll_ghost_candidates(
            Dm.text("Do you want to play as a reinforcement []?", special_role_name),
            check_jobban: ROLE_OPERATIVE_MIDROUND,
            role: ROLE_OPERATIVE_MIDROUND,
            poll_time: 15 * SECONDS,
            ignore_category: POLL_IGNORE_SYNDICATE,
            alert_pic: this,
            role_name_text: special_role_name,
            amount_to_pick: 1) as mob;
        if (chosen_one != null)
        {
            if (QDELETED(this) || !Dm.truthy(check_usability(user)))
                return null;
            used = TRUE;
            await spawn_antag(chosen_one.client, get_turf(this), "nukeop", user.mind);
            do_sparks(4, TRUE, this);
            qdel(this);
        }
        else
            to_chat(user, span_warning(I18n.T("Unable to connect to Syndicate command. Please wait and try again later or use the beacon on your uplink to get your points refunded.")));
        return null;
    }

    // tg: code/modules/antagonists/_common/antag_spawner.dm:153
    [DmParams("our_client", "T", "kind", "user")]
    public override async DmTask spawn_antag(client C = null, turf T = null, object kind = null, datum_mind user = null)
    {
        client our_client = C;
        mob_living_carbon_human nukie = Dm.@new<mob_living_carbon_human>();
        our_client.prefs.safe_transfer_prefs_to(nukie, is_antag: TRUE);
        nukie.ckey = our_client.key;
        datum_mind op_mind = nukie.mind;
        if (Dm.truthy(length(GLOB.newplayer_start))) // needed as hud code doesn't render huds if the atom (in this case the nukie) is in nullspace, so just move the nukie somewhere safe
            await nukie.forceMove(pick(GLOB.newplayer_start) as atom);
        else
            await nukie.forceMove(locate(1, 1, 1));

        object new_datum = Dm.@new(antag_datum);

        var creator_op = user.has_antag_datum(typeof(datum_antagonist_nukeop), TRUE) as datum_antagonist_nukeop;
        await op_mind.add_antag_datum(new_datum, creator_op != null ? creator_op.get_team() : null);
        LAZYADD(ref op_mind.special_roles, special_role_name);

        if (outfit != null)
        {
            var nukie_datum = op_mind.has_antag_datum(antag_datum) as datum_antagonist_nukeop;
            nukie_datum.nukeop_outfit = Dm.truthy(use_subtypes) ? pick(subtypesof(outfit)) as Type : outfit;
        }

        obj_structure_closet_supplypod pod = setup_pod();
        await nukie.forceMove(pod);
        Dm.@new<obj_effect_pod_landingzone>(spawn_location ?? get_turf(this), pod);
    }
}

// tg: code/modules/antagonists/_common/antag_spawner.dm:202  (silicons SI9, D326: the syndicate cyborg beacons)
public partial class obj_item_antag_spawner_nuke_ops_borg_tele : obj_item_antag_spawner_nuke_ops
{
    // tg: code/modules/antagonists/_common/antag_spawner.dm:220
    public override async DmTask spawn_antag(client C = null, turf T = null, object kind = null, datum_mind user = null)
    {
        mob_living_silicon_robot borg = null;
        var creator_op = user.has_antag_datum(typeof(datum_antagonist_nukeop), TRUE) as datum_antagonist_nukeop;
        if (creator_op == null)
            return;
        obj_structure_closet_supplypod pod = setup_pod();
        switch (Dm.str(special_role_name))
        {
            case ROLE_SYNDICATE_MEDBORG:
                borg = Dm.@new<mob_living_silicon_robot_model_syndicate_medical>();
                break;
            case ROLE_SYNDICATE_SABOBORG:
                borg = Dm.@new<mob_living_silicon_robot_model_syndicate_saboteur>();
                break;
            case ROLE_SYNDICATE_ASSAULTBORG:
                borg = Dm.@new<mob_living_silicon_robot_model_syndicate>();
                break;
            default:
                stack_trace(Dm.text("Unknown cyborg type '[]' could not be found by []!", special_role_name, this), "code/modules/antagonists/_common/antag_spawner.dm", 234);
                break;
        }

        object brainfirstname = pick(GLOB.first_names_male);
        if (prob(50))
            brainfirstname = pick(GLOB.first_names_female);
        object brainopslastname = pick(GLOB.last_names);
        if (Dm.truthy(creator_op.nuke_team.syndicate_name))  //the brain inside the syndiborg has the same last name as the other ops.
            brainopslastname = creator_op.nuke_team.syndicate_name;
        string brainopsname = Dm.text("[] []", brainfirstname, brainopslastname);

        borg.mmi.set_name(brainopsname);
        borg.real_name = borg.name;

        borg.PossessByPlayer(C.key);

        await borg.mind.add_antag_datum(antag_datum, creator_op?.get_team());
        LAZYADD(ref borg.mind.special_roles, special_role_name);
        await borg.forceMove(pod);
        Dm.@new<obj_effect_pod_landingzone>(get_turf(this), pod);
    }
}

// tg: code/modules/antagonists/_common/antag_spawner.dm:177
public partial class obj_item_antag_spawner_nuke_ops_overwatch : obj_item_antag_spawner_nuke_ops
{
    // tg: code/modules/antagonists/_common/antag_spawner.dm:186
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(length(GLOB.nukeop_base_overwatch_start))) //Otherwise, it will default to the datum's spawn point anyways
            spawn_location = pick(GLOB.nukeop_base_overwatch_start) as turf;
        return dot;
    }
}

/**
 * A subtype meant for 'normal' antag spawner items so as to reduce the amount of required hardcoding.
 */
// tg: code/modules/antagonists/_common/antag_spawner.dm:306
public partial class obj_item_antag_spawner_loadout : obj_item_antag_spawner
{
    // tg: code/modules/antagonists/_common/antag_spawner.dm:330
    public virtual object check_usability(mob user)
    {
        if (Dm.truthy(used))
        {
            to_chat(user, span_warning(I18n.T("[] is out of power!", this)));
            return FALSE;
        }
        return TRUE;
    }

    /// Creates the drop pod the spawned_mob will be dropped by
    // tg: code/modules/antagonists/_common/antag_spawner.dm:337
    public virtual obj_structure_closet_supplypod setup_pod()
    {
        obj_structure_closet_supplypod pod = Dm.@new<obj_structure_closet_supplypod>(null, pod_style);
        pod.explosionSize = Dm.list(0, 0, 0, 0);
        pod.bluespace = TRUE;
        return pod;
    }

    // tg: code/modules/antagonists/_common/antag_spawner.dm:343
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(check_usability(user)))
            return null;

        to_chat(user, span_notice(I18n.T("You activate [] and wait for confirmation.", this)));
        mob chosen_one = await SSpolling.poll_ghost_candidates(
            check_jobban: role_to_play,
            poll_time: 10 * SECONDS,
            ignore_category: poll_ignore_category,
            alert_pic: this,
            role_name_text: role_to_play,
            amount_to_pick: 1) as mob;
        if (chosen_one == null)
        {
            to_chat(user, span_warning(fail_text));
            return null;
        }
        if (QDELETED(this) || !Dm.truthy(check_usability(user)))
            return null;
        used = TRUE;
        await spawn_antag(chosen_one.client, get_turf(this), user);
        do_sparks(4, TRUE, this);
        qdel(this);
        return null;
    }

    // For subtypes to do special things to the summoned dude.
    // tg: code/modules/antagonists/_common/antag_spawner.dm:366
    public virtual async DmTask do_special_things(mob_living_carbon_human spawned_mob, mob user)
    {
        return;
    }

    // tg: code/modules/antagonists/_common/antag_spawner.dm:369
    [DmParams("our_client", "T", "user")]
    public override async DmTask spawn_antag(client C = null, turf T = null, object kind = null, datum_mind user = null)
    {
        client our_client = C;
        mob user_mob = kind as mob;
        mob_living spawned_mob = Dm.@new(spawn_type) as mob_living;
        obj_structure_closet_supplypod pod = setup_pod();
        our_client.prefs.safe_transfer_prefs_to(spawned_mob as mob_living_carbon_human, is_antag: TRUE);
        spawned_mob.ckey = our_client.key;
        datum_mind op_mind = spawned_mob.mind;
        if (Dm.truthy(length(GLOB.newplayer_start))) // needed as hud code doesn't render huds if the atom (in this case the spawned_mob) is in nullspace, so just move the spawned_mob somewhere safe
            await spawned_mob.forceMove(pick(GLOB.newplayer_start) as atom);
        else
            await spawned_mob.forceMove(locate(1, 1, 1));

        await op_mind.add_antag_datum(antag_datum);

        if (spawned_mob is mob_living_carbon_human human_mob)
        {
            // ignore if it's already the same (tg compares the species datum with the path: never equal, the species is always set)
            if (!Dm.eq(human_mob.dna.species, species_type))
                human_mob.set_species(species_type);

            await human_mob.equipOutfit(outfit);
        }

        LAZYADD(ref op_mind.special_roles, role_to_play);

        await do_special_things(spawned_mob as mob_living_carbon_human, user_mob);

        await spawned_mob.forceMove(pod);
        Dm.@new<obj_effect_pod_landingzone>(get_turf(this), pod);
    }
}

// tg: code/modules/antagonists/_common/antag_spawner.dm:398
public partial class obj_item_antag_spawner_loadout_contractor : obj_item_antag_spawner_loadout
{
    // tg: code/modules/antagonists/_common/antag_spawner.dm:409
    public override async DmTask do_special_things(mob_living_carbon_human contractor_support, mob user)
    {
        to_chat(contractor_support, "\n" + span_alertwarning(I18n.T("[] is your superior. Follow any, and all orders given by them. You're here to support their mission only.", user.real_name)));
        to_chat(contractor_support, span_alertwarning(I18n.T("Should they perish, or be otherwise unavailable, you're to assist other active agents in this mission area to the best of your ability.")));
    }
}

// tg: code/modules/antagonists/_common/antag_spawner.dm:413
public partial class obj_item_antag_spawner_loadout_monkey_man : obj_item_antag_spawner_loadout
{
    // tg: code/modules/antagonists/_common/antag_spawner.dm:427
    public override async DmTask do_special_things(mob_living_carbon_human monkey_man, mob user)
    {
        await monkey_man.fully_replace_character_name(monkey_man.real_name, pick(GLOB.syndicate_monkey_names));

        await monkey_man.crewlike_monkify();

        // fuck you i am no longer playing around. this goes against the entire soul of the item
        RegisterSignal(monkey_man, COMSIG_SPECIES_GAIN, nameof(allergy));

        await monkey_man.mind.enslave_mind_to_creator(user as mob_living);

        obj_item_implant_explosive imp = Dm.@new<obj_item_implant_explosive>(this);
        await imp.implant(monkey_man, user);
    }

    // tg: code/modules/antagonists/_common/antag_spawner.dm:443
    [SignalHandler]
    public virtual void allergy(mob_living second_lifer, datum_species folly_species)
    {
        if (HAS_TRAIT(second_lifer, TRAIT_SIMIAN))
            return;
        // timer is long to let them panic and consider their folly, and because allergies take a while
        second_lifer.visible_message(span_bolddanger(I18n.T("[] starts swelling unhealthily in size. It looks like they had an allergic reaction to becoming a []!", second_lifer, folly_species)), span_userdanger(I18n.T("As your monkey features morph, you feel your allergies coming in. Oh no.")));
        // no brain or items. organs are funny though
        second_lifer.inflate_gib(drop_bitflags: DROP_ORGANS | DROP_BODYPARTS, gib_time: 25 * SECONDS, anim_time: 40 * SECONDS);
    }
}
