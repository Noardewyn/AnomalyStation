// Translated from tgstation (snapshot 1d99bd7f): code/modules/religion/honorbound/honorbound_rites.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the honorbound sect's rites (forgiving the guilty, the holy writ of the rules, the crusade's deaconize). The var
// blocks are generated (D111).

///Mostly useless funny rite for forgiving someone, making them innocent once again.
// tg: code/modules/religion/honorbound/honorbound_rites.dm:2
public partial class datum_religion_rites_forgive : datum_religion_rites
{
    // tg: code/modules/religion/honorbound/honorbound_rites.dm:8
    public override async DmTask<object> perform_rite(mob_living user = null, atom religious_tool = null)
    {
        if (!ishuman(user))
            return FALSE;
        datum_brain_trauma_special_honorbound honor = ((mob_living_carbon_human)user).has_trauma_type(typeof(datum_brain_trauma_special_honorbound)) as datum_brain_trauma_special_honorbound;
        if (honor == null)
            return FALSE;
        if (!Dm.truthy(length(honor.guilty)))
        {
            to_chat(user, span_warning(I18n.T("[] is holding no grudges to forgive.", GLOB.deity)));
            return FALSE;
        }
        object forgiven_choice = await tgui_input_list(user, I18n.T("Choose one of []'s guilty to forgive", GLOB.deity), I18n.T("Forgive"), honor.guilty);
        if (forgiven_choice == null)
            return FALSE;
        who = forgiven_choice as mob_living;
        return await base.perform_rite(user, religious_tool);
    }

    // tg: code/modules/religion/honorbound/honorbound_rites.dm:23
    public override async DmTask<object> invoke_effect(mob_living user = null, atom religious_tool = null)
    {
        await base.invoke_effect(user, religious_tool);
        if (in_range(user, religious_tool))
            return FALSE;
        datum_brain_trauma_special_honorbound honor = ((mob_living_carbon_human)user).has_trauma_type(typeof(datum_brain_trauma_special_honorbound)) as datum_brain_trauma_special_honorbound;
        if (honor == null) //edge case
            return FALSE;
        Dm.remove(ref honor.guilty, who);
        who = null;
        playsound(get_turf(religious_tool), Dm.resource("sound/effects/pray.ogg"), 50, TRUE);
        return TRUE;
    }
}

// tg: code/modules/religion/honorbound/honorbound_rites.dm:35
public partial class datum_religion_rites_summon_rules : datum_religion_rites
{
    // tg: code/modules/religion/honorbound/honorbound_rites.dm:42
    public override async DmTask<object> perform_rite(mob_living user = null, atom religious_tool = null)
    {
        foreach (obj_item_paper could_writ in Dm.each<obj_item_paper>(get_turf(religious_tool)))
        {
            if (could_writ is obj_item_paper_holy_writ)
                continue;
            if (Dm.truthy(could_writ.get_total_length())) //blank paper pls
                continue;
            writ_target = could_writ; //PLEASE SIGN MY AUTOGRAPH
            return await base.perform_rite(user, religious_tool);
        }
        to_chat(user, span_warning(I18n.T("You need to place blank paper on [] to do this!", religious_tool)));
        return FALSE;
    }

    // tg: code/modules/religion/honorbound/honorbound_rites.dm:53
    public override async DmTask<object> invoke_effect(mob_living user = null, atom religious_tool = null)
    {
        await base.invoke_effect(user, religious_tool);
        obj_item_paper autograph = writ_target;
        turf tool_turf = get_turf(religious_tool);
        writ_target = null;
        if (QDELETED(autograph) || !(tool_turf == autograph.loc)) //check if the paper is still there
        {
            to_chat(user, span_warning(I18n.T("Your target left the altar!")));
            return FALSE;
        }
        autograph.visible_message(span_notice(I18n.T("Words magically form on []!", autograph)));
        playsound(tool_turf, Dm.resource("sound/effects/pray.ogg"), 50, TRUE);
        Dm.@new<obj_item_paper_holy_writ>(tool_turf);
        qdel(autograph);
        return TRUE;
    }
}

// tg: code/modules/religion/honorbound/honorbound_rites.dm:67
public partial class obj_item_paper_holy_writ : obj_item_paper
{
    //info set in here because we need GLOB.deity
    // tg: code/modules/religion/honorbound/honorbound_rites.dm:74
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        add_filter("holy_outline", 9, Dm.list(Dm.kv("type", "outline"), Dm.kv("color", "#fdff6c")));
        name = I18n.T("[]'s honorbound rules", GLOB.deity);
        default_raw_text = I18n.T("[]'s honorbound rules:\n\t<br>\n\t1.) Thou shalt not attack the unready!<br>\n\tThose who are not ready for battle should not be wrought low. The evil of this world must lose\n\tin a fair battle if you are to conquer them completely. Lesser creatures are given the benefit of\n\tbeing unready, keep that in mind.\n\t<br>\n\t<br>\n\t2.) Thou shalt not attack the just!<br>\n\tThose who fight for justice and good must not be harmed. Security is uncorruptable and must\n\tbe respected. Healers are mostly uncorruptable, and if you are truly sure medical has fallen\n\tto the scourge of evil, use a declaration of evil.\n\t<br>\n\t<br>\n\t3.) Thou shalt not attack the innocent!<br>\n\tThere is no honor on a pre-emptive strike, unless they are truly evil vermin.\n\tThose who are guilty will either lay a hand on you first, or you may declare their evil. Mindless, lesser\n\tcreatures cannot be considered innocent, nor evil. They are beings of passion and function, and\n\tmay be dispatched as such if their passions misalign with the pursuits of a better world.\n\t<br>\n\t<br>\n\t4.) Thou shalt not use profane magicks!<br>\n\tYou are not a warlock, you are an honorable warrior. There is nothing more corruptive than\n\tthe vile magicks used by witches, warlocks, and necromancers. There are exceptions to this rule.<br>\n\tYou may use holy magic, and (if you recruit one) a mime may use holy mimery. Restoration has also\n\tbeen allowed as it is a school focused on the light and mending of this world.\n\t", GLOB.deity);
        return base.Initialize(mapload, __extra);
    }
}

/**
 * Crusader deaconize
 * Along with making the person holy & being an infinite-use type, it comes with the cost
 * of enforcing an honorbound code onto convertees.
 * Earns the church favor per conversion, but convincing others to uphold the code is not easy.
 * Geneless species are not welcome for reasons
 * (The actual reason is because the honorbound trauma used to be a mutation which they couldn't get,
 * maybe it's time we let them join? idk)
 */
// tg: code/modules/religion/honorbound/honorbound_rites.dm:118
public partial class datum_religion_rites_deaconize_crusader : datum_religion_rites_deaconize
{
    /// how much favor is gained when someone is deaconized
    // tg: code/modules/religion/honorbound/honorbound_rites.dm:107  #define DEACONIZE_FAVOR_GAIN 300 (file-local, #undef at :149)
    private const int DEACONIZE_FAVOR_GAIN = 300;

    // tg: code/modules/religion/honorbound/honorbound_rites.dm:133
    public override async DmTask post_invoke_effects(mob_living user = null, atom religious_tool = null)
    {
        await base.post_invoke_effects(user, religious_tool);
        GLOB.religious_sect.adjust_favor(DEACONIZE_FAVOR_GAIN, user);
    }

    // tg: code/modules/religion/honorbound/honorbound_rites.dm:137
    public override object is_valid_for_deacon(mob_living_carbon_human possible_deacon = null, mob_living user = null)
    {
        if (Dm.@in(TRAIT_GENELESS, ((datum_species)possible_deacon.dna.species).inherent_traits))
        {
            to_chat(user, span_warning(I18n.T("This species disgusts []! They would never be allowed to join the crusade!", GLOB.deity)));
            return FALSE;
        }
        return base.is_valid_for_deacon(possible_deacon, user);
    }

    // tg: code/modules/religion/honorbound/honorbound_rites.dm:143
    public override async DmTask invite_deacon(mob_living_carbon_human invited = null)
    {
        object ask = await tgui_alert(invited, I18n.T("Join []? You will be bound to a code of honor.", GLOB.deity), I18n.T("Invitation"), Dm.list(I18n.T("Yes"), I18n.T("No")), 60 * SECONDS);
        if (!Dm.eq(ask, I18n.T("Yes")))
            return;
        potential_deacon = invited;
    }
}
