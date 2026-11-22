// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/noticable_organ.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C5).

/**
 * noticable organ element; which makes organs have a special description added to the person with the organ, if certain body zones aren't covered.
 *
 * Used for infused mutant organs
 */
// tg: code/datums/elements/noticable_organ.dm:7
public partial class datum_element_noticable_organ : datum_element
{
    // tg: code/datums/elements/noticable_organ.dm:15
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);

        if (target is not obj_item_organ organ)
            return ELEMENT_INCOMPATIBLE;

        this.infused_desc = Dm.arg(__extra, 0);
        this.body_zone = Dm.arg(__extra, 1);

        RegisterSignal(target, COMSIG_ORGAN_IMPLANTED, nameof(enable_description));
        RegisterSignal(target, COMSIG_ORGAN_REMOVED, nameof(on_removed));
        if (organ.owner != null)
            enable_description(organ, organ.owner);
        return dot;
    }

    // tg: code/datums/elements/noticable_organ.dm:29
    public override object Detach(datum source = null, params object[] __extra)
    {
        obj_item_organ target = source as obj_item_organ;
        UnregisterSignal(target, Dm.list(COMSIG_ORGAN_IMPLANTED, COMSIG_ORGAN_REMOVED));
        if (target.owner != null)
            UnregisterSignal(target.owner, COMSIG_ATOM_EXAMINE);
        return base.Detach(source, __extra);
    }

    /// Proc that returns true or false if the organ should show its examine check.
    // tg: code/datums/elements/noticable_organ.dm:36
    public virtual object should_show_text(mob_living_carbon examined = null)
    {
        return examined.is_location_accessible(body_zone);
    }

    // tg: code/datums/elements/noticable_organ.dm:39
    public virtual void enable_description(obj_item_organ target = null, mob_living_carbon receiver = null)
    {
        RegisterSignal(receiver, COMSIG_ATOM_EXAMINE, nameof(on_receiver_examine));
    }

    // tg: code/datums/elements/noticable_organ.dm:44
    public virtual void on_removed(obj_item_organ target = null, mob_living_carbon loser = null)
    {
        UnregisterSignal(loser, COMSIG_ATOM_EXAMINE);
    }

    // tg: code/datums/elements/noticable_organ.dm:49
    public virtual void on_receiver_examine(mob_living_carbon examined = null, mob user = null, DmList examine_list = null)
    {
        if (!Dm.truthy(should_show_text(examined)))
            return;

        object examine_text = REPLACE_PRONOUNS(infused_desc, examined);

        Dm.append(ref examine_list, examine_text);
    }
}

/**
 * Subtype of noticable organs for AI control, that will make a few more ai status checks before forking over the examine.
 */
// tg: code/datums/elements/noticable_organ.dm:66
public partial class datum_element_noticable_organ_ai_control : datum_element_noticable_organ
{
    // tg: code/datums/elements/noticable_organ.dm:69
    public override object should_show_text(mob_living_carbon examined = null)
    {
        object dot = base.should_show_text(examined);
        if (!Dm.truthy(dot))
            return FALSE;
        if (examined.ai_controller is datum_ai_controller brain && Dm.eq(brain.ai_status, AI_STATUS_ON))
        {
            if (!Dm.truthy(((datum_species)examined.dna.species).ai_controlled_species))
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/datums/elements/noticable_organ.dm:75
    public override void on_removed(obj_item_organ target = null, mob_living_carbon loser = null)
    {
        Detach(target);
    }
}
