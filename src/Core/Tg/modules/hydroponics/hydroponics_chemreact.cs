// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/hydroponics_chemreact.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the whole file.

// tg: code/modules/hydroponics/hydroponics_chemreact.dm:5
public partial class obj_machinery_hydroponics : obj_machinery
{
    /**
     *This is NOW the gradual affects that each chemical applies on every process() proc. Nutrients now use a more robust reagent holder in order to apply less insane
     * stat changes as opposed to 271 lines of individual statline effects. Shoutout to the original comments on chems, I just cleaned a few up.
     */
    // tg: code/modules/hydroponics/hydroponics_chemreact.dm:5
    public virtual async DmTask apply_chemicals(mob user = null)
    {
        ///Contains the reagents within the tray.
        if (myseed != null)
            myseed.on_chem_reaction(reagents as datum_reagents); //In case seeds have some special interactions with special chems, currently only used by vines
        foreach (datum_reagent chem in Dm.each_cast<datum_reagent>(((datum_reagents)reagents).reagent_list))
        {
            if (Dm.num(chem.volume) < 1)
                continue;
            await chem.on_hydroponics_apply(this, user);
        }
    }

    // tg: code/modules/hydroponics/hydroponics_chemreact.dm:14
    [DmParams("reagents", "source", "methods", "volume_modifier", "show_message")]
    public override async DmTask<object> expose_reagents(DmList reagents = null, datum_reagents source = null, float? methods = null, float? show_message = null, params object[] __extra)
    {
        object dot = await base.expose_reagents(reagents, source, methods, show_message, __extra);
        if (Dm.truthy(Dm.bitand(dot, COMPONENT_NO_EXPOSE_REAGENTS)))
            return dot;

        datum_reagents own = this.reagents as datum_reagents;
        if (Dm.truthy(own.holder_full()))
            return dot;

        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(reagents))
        {
            if (reagent is datum_reagent_water)
                adjust_waterlevel(Dm.round(Dm.num(reagents[reagent])));
            else
                await own.add_reagent(reagent.type, reagents[reagent]);
        }
        update_appearance();
        return dot;
    }

    /// Called when a radioactive reagent is applied to the tray
    // tg: code/modules/hydroponics/hydroponics_chemreact.dm:30
    public virtual async DmTask radioactive_exposure(float? modifier = null)
    {
        float mod = modifier ?? 1;
        if (myseed == null)
            return;

        if (prob(Dm.min(75, 25 * mod)))
        {
            myseed.adjust_instability(Dm.round(2 * mod));
            adjust_toxic(Dm.round(1.5f * mod)); // It is still toxic, mind you
            return;
        }

        float roll = rand(0, 50);
        if (roll >= 41 && roll <= 50)
        {
            adjust_plant_health(Dm.round(-5 * mod));
            visible_message(span_warning(I18n.T("\\The [] starts to wilt and burn!", myseed.plantname)));
        }
        else if (roll >= 21 && roll <= 40)
        {
            visible_message(span_notice(I18n.T("\\The [] appears unusually reactive...", myseed.plantname)));
        }
        else if (roll >= 11 && roll <= 20)
        {
            if (mod >= 0.5f)
                await mutateweed();
            else
                adjust_weedlevel(Dm.max(1, Dm.round(mod)));
        }
        else if (roll >= 0 && roll <= 10)
        {
            if (mod >= 0.5f)
                mutatepest();
            else
                adjust_pestlevel(Dm.max(1, Dm.round(mod)));
        }
    }
}
