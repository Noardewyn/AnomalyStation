// Translated from tgstation (snapshot 1d99bd7f): code/modules/manufactorio/machines/sorter_filters.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E16: the conveyor sort-router's filters, whole: input is a stack, ore, mail, tagged with a destination, has a name that
// contains a text, is a specific item (the next item the sorter is hit with) or a specific kind of item. The var blocks are generated
// but for `value` below.
//
// GAP(types): tg's `var/value = ""` (:7) holds a text, a destination's index (is_tagged) or a typepath (is_path_specific); the
// generator types it `string` from its default, so it is declared here as the DM value it is.

// tg: code/modules/manufactorio/machines/sorter_filters.dm:1
public partial class datum_sortrouter_filter : datum
{
    /// value of our filter, checked by us
    // tg: code/modules/manufactorio/machines/sorter_filters.dm:7  (hand-written: see the GAP(types) above)
    public object value = "";

    // tg: code/modules/manufactorio/machines/sorter_filters.dm:13
    [DmParams("sorter")]
    public override async DmTask New(params object[] __extra)
    {
        object sorter = Dm.arg<object>(__extra, 0);
        await base.New(__extra);
        if (sorter == null)
            return;
        this.sorter = sorter as obj_machinery_power_manufacturing_sorter;
    }

    // tg: code/modules/manufactorio/machines/sorter_filters.dm:19
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        if (sorter == null)
            return dot;
        sorter = null;
        return dot;
    }

    // tg: code/modules/manufactorio/machines/sorter_filters.dm:27
    public virtual object return_name()
    {
        return name;
    }

    // tg: code/modules/manufactorio/machines/sorter_filters.dm:30
    public virtual async DmTask edit(mob user = null)
    {
        to_chat(user, "This filter is not editable.");
    }

    // tg: code/modules/manufactorio/machines/sorter_filters.dm:33
    public virtual object meets_conditions(atom checking = null)
    {
        return null;
    }
}

// tg: code/modules/manufactorio/machines/sorter_filters.dm:35
public partial class datum_sortrouter_filter_is_stack : datum_sortrouter_filter
{
    // tg: code/modules/manufactorio/machines/sorter_filters.dm:38
    public override object meets_conditions(atom checking = null)
    {
        return Dm.num(isstack(checking));
    }
}

// tg: code/modules/manufactorio/machines/sorter_filters.dm:41
public partial class datum_sortrouter_filter_is_ore : datum_sortrouter_filter
{
    // tg: code/modules/manufactorio/machines/sorter_filters.dm:44
    public override object meets_conditions(atom checking = null)
    {
        return Dm.num(Dm.istype(checking, typeof(obj_item_stack_ore)));
    }
}

// tg: code/modules/manufactorio/machines/sorter_filters.dm:47
public partial class datum_sortrouter_filter_is_mail : datum_sortrouter_filter
{
    // tg: code/modules/manufactorio/machines/sorter_filters.dm:50
    public override object meets_conditions(atom checking = null)
    {
        return Dm.num(Dm.istype(checking, typeof(obj_item_mail)));
    }
}

// tg: code/modules/manufactorio/machines/sorter_filters.dm:53
public partial class datum_sortrouter_filter_is_tagged : datum_sortrouter_filter
{
    // tg: code/modules/manufactorio/machines/sorter_filters.dm:56
    public override async DmTask edit(mob user = null)
    {
        object target = await tgui_input_list(user, "Select a tag", "Tag", (DmList)sort_list(GLOB.TAGGERLOCATIONS));
        if (target == null || !Dm.truthy(await user.can_perform_action(sorter, ALLOW_SILICON_REACH)))
            return;
        value = GLOB.TAGGERLOCATIONS.Find(target);
    }

    // tg: code/modules/manufactorio/machines/sorter_filters.dm:62
    public override object return_name()
    {
        return Dm.text("input is tagged []", Dm.truthy(value) ? GLOB.TAGGERLOCATIONS[value] : "");
    }

    // tg: code/modules/manufactorio/machines/sorter_filters.dm:65
    public override object meets_conditions(atom checking = null)
    {
        object sort_tag = null;
        if (checking is obj_item_delivery delivery)
            sort_tag = delivery.sort_tag;
        else if (checking is obj_item_mail mail) // tg reads the mail's sort_tag through its delivery-typed local
            sort_tag = mail.sort_tag;

        return Dm.num(Dm.eq(value, sort_tag));
    }
}

// tg: code/modules/manufactorio/machines/sorter_filters.dm:73
public partial class datum_sortrouter_filter_name_contains : datum_sortrouter_filter
{
    // tg: code/modules/manufactorio/machines/sorter_filters.dm:76
    public override async DmTask edit(mob user = null)
    {
        object target = await tgui_input_text(user, "What should it contain?", "Name", value, 12);
        if (target == null || !Dm.truthy(await user.can_perform_action(sorter, ALLOW_SILICON_REACH)))
            return;
        value = target;
    }

    // tg: code/modules/manufactorio/machines/sorter_filters.dm:82
    public override object return_name()
    {
        return Dm.text("input's name contains []", value);
    }

    // tg: code/modules/manufactorio/machines/sorter_filters.dm:85
    public override object meets_conditions(atom checking = null)
    {
        return findtext(LOWER_TEXT(checking.name), value);
    }
}

// tg: code/modules/manufactorio/machines/sorter_filters.dm:88
public partial class datum_sortrouter_filter_is_path_specific : datum_sortrouter_filter
{
    // tg: code/modules/manufactorio/machines/sorter_filters.dm:94
    public override async DmTask edit(mob user = null)
    {
        name = Dm.initial(this, (datum_sortrouter_filter_is_path_specific p) => p.name);
        if (!Dm.truthy(currently_listening))
        {
            name = "awaiting item";
            to_chat(user, "Hit the sorter with the item of choice to set the filter.");
            sorter.balloon_alert(user, "awaiting item!");
            currently_listening = TRUE;
            RegisterSignal(sorter, COMSIG_ATOM_ATTACKBY, nameof(sorter_hit));
        }
        else
        {
            currently_listening = FALSE;
            UnregisterSignal(sorter, COMSIG_ATOM_ATTACKBY);
        }
    }

    // tg: code/modules/manufactorio/machines/sorter_filters.dm:106
    public virtual object sorter_hit(datum source = null, obj_item attacking_item = null, object user = null, object @params = null)
    {
        currently_listening = FALSE;
        value = attacking_item.type;
        name = attacking_item.name;
        sorter.balloon_alert(user as mob, "filter set");
        UnregisterSignal(sorter, COMSIG_ATOM_ATTACKBY);
        return COMPONENT_NO_AFTERATTACK;
    }

    // tg: code/modules/manufactorio/machines/sorter_filters.dm:114
    public override object meets_conditions(atom checking = null)
    {
        return Dm.num(Dm.eq(checking.type, value));
    }
}

// tg: code/modules/manufactorio/machines/sorter_filters.dm:116
public partial class datum_sortrouter_filter_is_path_specific_subtypes : datum_sortrouter_filter_is_path_specific
{
    // tg: code/modules/manufactorio/machines/sorter_filters.dm:119
    public override object meets_conditions(atom checking = null)
    {
        // tg's istype(checking.type, value): istype() of a typepath is always false in DM (ispath would test it), so this filter never matches
        return Dm.num(Dm.istype(checking.type, value as Type));
    }
}
