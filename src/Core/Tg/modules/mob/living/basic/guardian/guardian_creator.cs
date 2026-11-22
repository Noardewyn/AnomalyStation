// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/guardian/guardian_creator.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X2 (D316): the whole file but the logs and the wizard's team (the wizard's spawn_guardian gives its guardian the
// wizard's minion datum, which waits for the wizard: nobody is a wizard yet). The var blocks are generated.

public partial class datum_controller_global_vars
{
    // tg: code/modules/mob/living/basic/guardian/guardian_creator.dm:1  GLOBAL_LIST_INIT(guardian_radial_images, setup_guardian_radial())
    public DmList guardian_radial_images;

    // tg: code/modules/mob/living/basic/guardian/guardian_creator.dm:1  (GLOBAL_MANAGED(guardian_radial_images, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalguardian_radial_images()
    {
        guardian_radial_images = setup_guardian_radial() as DmList;
        Dm.append(ref gvars_datum_init_order, nameof(guardian_radial_images));
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/mob/living/basic/guardian/guardian_creator.dm:3
    public static object setup_guardian_radial()
    {
        DmList dot = Dm.list();
        foreach (Type guardian_path in Dm.each_cast<Type>(subtypesof(typeof(mob_living_basic_guardian))))
        {
            datum_radial_menu_choice option = Dm.@new<datum_radial_menu_choice>();
            option.name = Dm.initial(guardian_path, (mob_living_basic_guardian p) => p.creator_name);
            option.image = image(icon: Dm.resource("icons/hud/guardian.dmi"), icon_state: Dm.initial(guardian_path, (mob_living_basic_guardian p) => p.creator_icon));
            option.info = span_boldnotice(Dm.initial(guardian_path, (mob_living_basic_guardian p) => p.creator_desc));
            dot[guardian_path] = option;
        }
        return dot;
    }
}

/// An item which grants you your very own soul buddy
// tg: code/modules/mob/living/basic/guardian/guardian_creator.dm:13
public partial class obj_item_guardian_creator : obj_item
{
    // tg: code/modules/mob/living/basic/guardian/guardian_creator.dm:63
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        datum_guardian_fluff using_theme = GLOB.guardian_themes[theme] as datum_guardian_fluff;
        mob_name = using_theme.name;
        RegisterSignal(this, COMSIG_ITEM_TC_REIMBURSED, nameof(on_reimbursed));
        return dot;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_creator.dm:69
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        mob_living living_user = user as mob_living;
        if (isguardian(user) && !Dm.truthy(allow_guardian))
        {
            balloon_alert(user, "can't do that!");
            return null;
        }
        DmList guardians = living_user.get_all_linked_holoparasites();
        if (Dm.truthy(length(guardians)) && !Dm.truthy(allow_multiple))
        {
            balloon_alert(user, "already have one!");
            return null;
        }
        if (Dm.truthy(IS_CHANGELING(user)) && !Dm.truthy(allow_changeling))
        {
            to_chat(user, ling_failure);
            return null;
        }
        if (Dm.truthy(used))
        {
            to_chat(user, used_message);
            return null;
        }
        DmList radial_options = GLOB.guardian_radial_images.Copy();
        foreach (object possible_guardian in Dm.each(radial_options.Copy()))
        {
            if (Dm.@in(possible_guardian, possible_guardians))
                continue;
            Dm.remove(radial_options, possible_guardian);
        }
        Type guardian_path;
        if (Dm.truthy(random))
            guardian_path = pick(possible_guardians) as Type;
        else
        {
            guardian_path = await show_radial_menu(user, this, radial_options, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), living_user), radius: 42, require_near: TRUE) as Type;
            if (guardian_path == null)
                return null;
        }
        used = TRUE;
        to_chat(user, use_message);
        string guardian_type_name = Dm.truthy(random) ? "Random" : Dm.str(capitalize(Dm.initial(guardian_path, (mob_living_basic_guardian p) => p.creator_name)));
        mob chosen_one = await SSpolling.poll_ghost_candidates(
            Dm.text("Do you want to play as [] []?", span_danger(Dm.text("[]'s", user.real_name)), span_notice(Dm.text("[] []", guardian_type_name, mob_name))),
            check_jobban: ROLE_PAI,
            poll_time: 10 * SECONDS,
            ignore_category: POLL_IGNORE_HOLOPARASITE,
            alert_pic: guardian_path,
            jump_target: this,
            role_name_text: guardian_type_name,
            amount_to_pick: 1
        ) as mob;

        if (Dm.truthy(was_refunded))
            return null;

        if (chosen_one != null)
        {
            await spawn_guardian(living_user, chosen_one as mob_dead, guardian_path);
            used = TRUE;
            // SEND_SIGNAL(src, COMSIG_TRAITOR_ITEM_USED(type)) (rules 11.1 (e), a statement)
            string used_signal = Dm.text("traitor_item_used_[]", type);
            if (Dm.truthy(_listen_lookup?[used_signal]))
                _SendSignal(used_signal, Dm.list(this));
        }
        else
        {
            to_chat(user, failure_message);
            used = FALSE;
        }
        return null;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_creator.dm:120
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(used))
            Dm.append(dot, span_holoparasite(used_description));
        else
            Dm.append(dot, span_holoparasite(unused_description));
        return dot;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_creator.dm:127
    [SignalHandler]
    public virtual void on_reimbursed(datum source = null)
    {
        was_refunded = TRUE;
    }

    /// Actually create our guy
    // tg: code/modules/mob/living/basic/guardian/guardian_creator.dm:132
    public virtual async DmTask<object> spawn_guardian(mob_living user = null, mob_dead candidate = null, object guardian_path = null)
    {
        if (QDELETED(user) || user.stat == DEAD)
            return null;
        DmList guardians = user.get_all_linked_holoparasites();
        if (Dm.truthy(length(guardians)) && !Dm.truthy(allow_multiple))
        {
            balloon_alert(user, "already got one!");
            used = FALSE;
            return null;
        }
        datum_guardian_fluff guardian_theme = GLOB.guardian_themes[theme] as datum_guardian_fluff;
        mob_living_basic_guardian summoned_guardian = Dm.@new((Type)guardian_path, user, guardian_theme) as mob_living_basic_guardian;
        await summoned_guardian.set_summoner(user, different_person: TRUE);
        summoned_guardian.PossessByPlayer(candidate.key);
        user.log_message(Dm.text("has summoned [], a [] holoparasite.", key_name(summoned_guardian), summoned_guardian.creator_name), LOG_GAME);
        summoned_guardian.log_message(Dm.text("was summoned as a [] holoparasite.", summoned_guardian.creator_name), LOG_GAME);
        to_chat(user, guardian_theme.get_fluff_string(summoned_guardian.guardian_type));
        to_chat(user, replacetext(success_message, "%GUARDIAN", mob_name));
        summoned_guardian.client?.init_verbs();
        await summoned_guardian.updatehealth(); // Set the initial health hud
        return summoned_guardian;
    }

    /// Checks to ensure we're still capable of using the radial selector
    // tg: code/modules/mob/living/basic/guardian/guardian_creator.dm:153
    public virtual object check_menu(mob_living user = null)
    {
        if (user == null)
            return FALSE;
        if (Dm.truthy(user.incapacitated) || !Dm.truthy(user.is_holding(this)) || Dm.truthy(used))
            return FALSE;
        return TRUE;
    }
}

/// Guardian creator available in the wizard spellbook. All but support are available.
// tg: code/modules/mob/living/basic/guardian/guardian_creator.dm:161
public partial class obj_item_guardian_creator_wizard : obj_item_guardian_creator
{
    // tg: code/modules/mob/living/basic/guardian/guardian_creator.dm:176
    public override async DmTask<object> spawn_guardian(mob_living user = null, mob_dead candidate = null, object guardian_path = null)
    {
        mob guardian = await base.spawn_guardian(user, candidate, guardian_path) as mob;
        if (guardian == null)
            return null;
        // Add the wizard team datum
        datum_antagonist antag_datum = user.mind.has_antag_datum(typeof(datum_antagonist_wizard)) as datum_antagonist;
        if (antag_datum == null)
            return guardian;
        // if(!antag_datum.wiz_team) antag_datum.create_wiz_team(); guardian.mind.add_antag_datum(/datum/antagonist/wizard_minion, antag_datum.wiz_team)
        // (:184-186): the wizard's team and minion datum come with the wizard (no package yet); nobody is a wizard before it
        Dm.NotTranslated("code/modules/mob/living/basic/guardian/guardian_creator.dm:184", "wizard spawn_guardian: the wizard's team and its minion datum (the wizard)");
        return guardian;
    }
}

/// Guardian creator available to miners from chests, very limited selection and randomly assigned.
// tg: code/modules/mob/living/basic/guardian/guardian_creator.dm:222
public partial class obj_item_guardian_creator_miner : obj_item_guardian_creator
{
    // tg: code/modules/mob/living/basic/guardian/guardian_creator.dm:245
    public override async DmTask<object> spawn_guardian(mob_living user = null, mob_dead candidate = null, object guardian_path = null)
    {
        mob_living_basic_guardian guardian = await base.spawn_guardian(user, candidate, guardian_path) as mob_living_basic_guardian;
        if (guardian == null)
            return null;
        // Immune to planetary weather effects
        ADD_TRAIT(guardian, TRAIT_ASHSTORM_IMMUNE, INNATE_TRAIT);
        ADD_TRAIT(guardian, TRAIT_SNOWSTORM_IMMUNE, INNATE_TRAIT);
        return guardian;
    }
}
