// Translated from tgstation (snapshot 1d99bd7f): code/modules/plumbing/plumbers/_plumb_machinery.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E15 (plumbing): the whole file but add_context (live-partial, screentips). The var block is generated (D111).
// The plunger (/obj/item/plunger) is not ported yet, so plunger_act is reached by no player.

/**Basic plumbing object.
* It doesn't really hold anything special, YET.
* Objects that are plumbing but not a subtype are as of writing liquid pumps and the reagent_dispenser tank
* Also please note that the plumbing component is toggled on and off by the component using a signal from default_unfasten_wrench, so dont worry about it
*/
// tg: code/modules/plumbing/plumbers/_plumb_machinery.dm:7
public partial class obj_machinery_plumbing : obj_machinery
{
    // tg: code/modules/plumbing/plumbers/_plumb_machinery.dm:22
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(set_anchored(mapload));
        if (Dm.truthy(mapload))
            begin_processing();
        create_reagents(buffer, reagent_flags);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation)));
        register_context();
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/_plumb_machinery.dm:31
    public override void create_reagents(object max_vol = null, object flags = null)
    {
        if (!ispath(reagents))
            qdel(reagents);
        reagents = Dm.@new((Type)reagents, max_vol, flags);
        ((datum_reagents)reagents).my_atom = this;
    }

    // tg: code/modules/plumbing/plumbers/_plumb_machinery.dm:37  add_context: live-partial (screentips).

    // tg: code/modules/plumbing/plumbers/_plumb_machinery.dm:52
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (isobserver(user) || !in_range(this, user))
            return dot;

        datum_reagents holder = (datum_reagents)reagents;
        Dm.append(ref dot, span_notice(I18n.T("The maximum volume display reads: <b>[]u capacity</b>. Contains:", holder.maximum_volume)));
        if (Dm.truthy(holder.total_volume))
        {
            foreach (datum_reagent reg in Dm.each_cast<datum_reagent>(holder.reagent_list))
                Dm.append(ref dot, span_notice(I18n.T("[]u of []", round(Dm.num(reg.volume), CHEMICAL_VOLUME_ROUNDING), reg.name)));
        }
        else
            Dm.append(ref dot, span_notice(I18n.T("Nothing.")));

        if (Dm.truthy(anchored))
            Dm.append(ref dot, span_notice(I18n.T("It's [] in place.", EXAMINE_HINT(I18n.T("anchored")))));
        else
        {
            Dm.append(ref dot, span_warning(I18n.T("Needs to be [] to start operations.", EXAMINE_HINT(I18n.T("anchored")))));
            Dm.append(ref dot, span_notice(I18n.T("It can be [] apart.", EXAMINE_HINT(I18n.T("welded")))));
        }

        Dm.append(ref dot, span_notice(I18n.T("A [] can be used to flush out reagents.", EXAMINE_HINT(I18n.T("plunger")))));
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/_plumb_machinery.dm:70
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;

        object dot = ITEM_INTERACT_BLOCKING;
        if (Dm.eq(await default_unfasten_wrench(user, tool), SUCCESSFUL_UNFASTEN))
        {
            if (Dm.truthy(anchored))
                begin_processing();
            else
                end_processing();
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/_plumb_machinery.dm:81
    [DmParams("user", "I")]
    public override async DmTask<object> welder_act(mob_living user = null, obj_item I = null)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;

        if (Dm.truthy(anchored))
        {
            balloon_alert(user, "unanchor first!");
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(I.tool_start_check(user, amount: 1)))
        {
            to_chat(user, span_notice(I18n.T("You start slicing \\the [] apart.", this)));
            if (Dm.truthy(await I.use_tool(this, user, 1.5f * SECONDS, volume: 50)))
            {
                await deconstruct(TRUE);
                to_chat(user, span_notice(I18n.T("You slice \\the [] apart.", this)));
                return ITEM_INTERACT_SUCCESS;
            }
        }

        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/plumbing/plumbers/_plumb_machinery.dm:101
    public override async DmTask<object> plunger_act(obj_item_plunger attacking_plunger = null, mob_living user = null, object reinforced = null)
    {
        user.balloon_alert_to_viewers("furiously plunging...");
        if (!Dm.truthy(await do_after(user, 3 * SECONDS, target: this)))
            return TRUE;
        user.balloon_alert_to_viewers("finished plunging");
        datum_reagents holder = (datum_reagents)reagents;
        await holder.expose(get_turf(this), TOUCH); //splash on the floor
        await holder.clear_reagents();
        return TRUE;
    }
}
