// Translated from tgstation (snapshot 1d99bd7f): code/modules/religion/deaconize.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the deaconize rite, which invites the person buckled to the altar and makes them a deacon when they accept. The
// var block is generated (D111).

/**
 * Deaconize
 * Makes a sentient, non-cult member of the station into a Holy person, able to use bibles & other chap gear.
 * Is a one-time use ability, given to all sects that don't have their own variation of it.
 */
// tg: code/modules/religion/deaconize.dm:6
public partial class datum_religion_rites_deaconize : datum_religion_rites
{
    // tg: code/modules/religion/deaconize.dm:24
    public override object Destroy(float? force = null)
    {
        potential_deacon = null;
        return base.Destroy(force);
    }

    // tg: code/modules/religion/deaconize.dm:28
    public override async DmTask<object> perform_rite(mob_living user = null, atom religious_tool = null)
    {
        if (!ismovable(religious_tool))
        {
            to_chat(user, span_warning(I18n.T("This rite requires a religious device that individuals can be buckled to.")));
            return FALSE;
        }
        atom_movable movable_reltool = religious_tool as atom_movable;
        if (movable_reltool == null)
            return FALSE;
        mob_living_carbon_human possible_deacon = Dm.locate<mob_living_carbon_human>(movable_reltool.buckled_mobs);
        if (possible_deacon == null)
        {
            to_chat(user, span_warning(I18n.T("Nothing is buckled to the []!", movable_reltool)));
            return FALSE;
        }
        if (!Dm.truthy(is_valid_for_deacon(possible_deacon, user)))
            return FALSE;
        //no one invited or this is not the invited person
        if (potential_deacon == null || (possible_deacon != potential_deacon))
        {
            Waiting.Detach(invite_deacon(possible_deacon));
            to_chat(user, span_notice(I18n.T("They have been offered the opportunity to join our ranks. Wait for them to decide and try again.")));
            return FALSE;
        }
        return await base.perform_rite(user, religious_tool);
    }

    // tg: code/modules/religion/deaconize.dm:48
    public override async DmTask<object> invoke_effect(mob_living user = null, atom religious_tool = null)
    {
        object dot = null;
        dot = await base.invoke_effect(user, religious_tool);
        if (!Dm.@in(potential_deacon, ((atom_movable)religious_tool).buckled_mobs)) //checks one last time if the right corpse is still buckled
        {
            to_chat(user, span_warning(I18n.T("[] is no longer on the altar!", potential_deacon)));
            return FALSE;
        }
        if (IS_UNCONSCIOUS_OR_CRIT(potential_deacon))
        {
            to_chat(user, span_warning(I18n.T("[] has to be conscious for the rite to work!", potential_deacon)));
            return FALSE;
        }
        if (potential_deacon.mind == null)
        {
            to_chat(user, span_warning(I18n.T("[]'s mind appears to be elsewhere!", potential_deacon)));
            return FALSE;
        }
        if (Dm.truthy(IS_CULTIST(potential_deacon)))//what the fuck?!
        {
            to_chat(user, span_warning(I18n.T("[] has seen a true, dark evil in []'s heart, and they have been smitten!", GLOB.deity, potential_deacon)));
            playsound(get_turf(religious_tool), Dm.resource("sound/effects/pray.ogg"), 50, TRUE);
            await potential_deacon.gib(Dm.bitor(DROP_ORGANS, DROP_BODYPARTS));
            return FALSE;
        }
        datum_brain_trauma_special_honorbound honor = (user as mob_living_carbon)?.has_trauma_type(typeof(datum_brain_trauma_special_honorbound)) as datum_brain_trauma_special_honorbound;
        if (honor != null && Dm.@in(potential_deacon, honor.guilty))
            Dm.remove(ref honor.guilty, potential_deacon);
        to_chat(user, span_notice(I18n.T("[] has bound [] to the code! They are now a holy role (albeit the lowest level of such)!", GLOB.deity, potential_deacon)));
        potential_deacon.mind.set_holy_role(HOLY_ROLE_DEACON);
        await GLOB.religious_sect.on_conversion(potential_deacon);
        playsound(get_turf(religious_tool), Dm.resource("sound/effects/pray.ogg"), 50, TRUE);
        return TRUE;
    }

    ///Helper if the passed possible_deacon is valid to become a deacon or not.
    // tg: code/modules/religion/deaconize.dm:74
    public virtual object is_valid_for_deacon(mob_living_carbon_human possible_deacon = null, mob_living user = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(possible_deacon))
        {
            to_chat(user, span_warning(I18n.T("[] needs to be alive and conscious to join!", possible_deacon)));
            return FALSE;
        }
        if (possible_deacon.mind != null && Dm.truthy(possible_deacon.mind.holy_role))
        {
            to_chat(user, span_warning(I18n.T("[] is already a member of the religion!", possible_deacon)));
            return FALSE;
        }
        return TRUE;
    }

    /**
     * Async proc that waits for a response on joining the sect.
     * If they accept, the deaconize rite can now recruit them instead of just offering more invites.
     */
    // tg: code/modules/religion/deaconize.dm:87
    public virtual async DmTask invite_deacon(mob_living_carbon_human invited = null)
    {
        object ask = await tgui_alert(invited, I18n.T("Join []? You will be expected to follow the chaplain's order.", GLOB.deity), I18n.T("Invitation"), Dm.list(I18n.T("Yes"), I18n.T("No")), 60 * SECONDS);
        if (!Dm.eq(ask, I18n.T("Yes")))
            return;
        potential_deacon = invited;
    }
}
