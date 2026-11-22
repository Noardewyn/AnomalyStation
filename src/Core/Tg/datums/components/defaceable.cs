// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/defaceable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the screentip hover (a live-partial row of the screentips) (gaps package C3).

/// Lets you graffiti on an object
// tg: code/datums/components/defaceable.dm:2
public partial class datum_component_defaceable : datum_component
{
    // tg: code/datums/components/defaceable.dm:14
    [DmParams("icon", "icon_states", "drawing_of", "on_defaced")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;

        this.icon = Dm.arg(__extra, 0);
        this.icon_states = Dm.arg<DmList>(__extra, 1);
        this.drawing_of = Dm.arg(__extra, 2);
        this.on_defaced = Dm.arg<datum_callback>(__extra, 3);
        return null;
    }

    // tg: code/datums/components/defaceable.dm:23
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ATOM_ITEM_INTERACTION, nameof(on_drawn));
        RegisterSignal(parent, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM, nameof(on_hovered));
        atom atom_parent = (atom)parent;
        Dm.combine(ref atom_parent.flags_1, HAS_CONTEXTUAL_SCREENTIPS_1);
    }

    // tg: code/datums/components/defaceable.dm:29
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_ATOM_ITEM_INTERACTION, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM, COMSIG_ATOM_EXAMINE, COMSIG_COMPONENT_CLEAN_ACT, COMSIG_ATOM_UPDATE_OVERLAYS));
        atom atom_parent = (atom)parent;
        atom_parent.update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/datums/components/defaceable.dm:34
    public override object Destroy(float? force = null)
    {
        on_defaced = null;
        return base.Destroy(force);
    }

    /// Inform people that they can mess us up
    // tg: code/datums/components/defaceable.dm:39
    public virtual object on_hovered(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (HAS_TRAIT(source, TRAIT_DEFACED))
        {
            if (held_item is obj_item_reagent_containers_spray or obj_item_soap or obj_item_rag)
                context[SCREENTIP_CONTEXT_LMB] = "Clean";
        }
        else
        {
            if (held_item is obj_item_pen or obj_item_toy_crayon)
                context[SCREENTIP_CONTEXT_LMB] = Dm.text("Draw []", Dm.truthy(drawing_of) ? drawing_of : "on");
        }
        return CONTEXTUAL_SCREENTIP_SET;
    }

    /// See if someone can draw on us
    // tg: code/datums/components/defaceable.dm:49
    [SignalHandler]
    public virtual object on_drawn(atom source = null, mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.combat_mode))
            return null;
        if (tool is obj_item_toy_crayon crayon)
            ink_colour = crayon.paint_color;
        else if (tool is obj_item_pen pen)
            ink_colour = pen.colour;

        if (!Dm.truthy(ink_colour))
            return null;

        ADD_TRAIT(parent, TRAIT_DEFACED, REF(this));

        if (on_defaced != null)
            Waiting.NoSleep(on_defaced.Invoke(ink_colour));

        RegisterSignal(parent, COMSIG_COMPONENT_CLEAN_ACT, nameof(on_cleaned));

        if (Dm.truthy(drawing_of))
            RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examined));

        if (icon != null && icon_states != null)
            RegisterSignal(parent, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(on_update_overlays));
        source.update_appearance(UPDATE_OVERLAYS);

        UnregisterSignal(parent, COMSIG_ATOM_ITEM_INTERACTION);

        return ITEM_INTERACT_SUCCESS;
    }

    /// Render our beautiful drawing
    // tg: code/datums/components/defaceable.dm:78
    [SignalHandler]
    public virtual void on_update_overlays(atom source = null, DmList overlays = null)
    {
        foreach (object state in Dm.each<object>(icon_states))
        {
            mutable_appearance appearance = mutable_appearance(icon, state as string) as mutable_appearance;
            if (!Dm.truthy(icon_states[state]))
                appearance.color = ink_colour as string;
            Dm.append(ref overlays, appearance);
        }
    }

    /// Wash it off
    // tg: code/datums/components/defaceable.dm:87
    [SignalHandler]
    public virtual void on_cleaned(atom source = null, object clean_types = null)
    {
        if (!Dm.truthy(Dm.bitand(clean_types, Dm.bitor(CLEAN_WASH, CLEAN_SCRUB))))
            return;

        REMOVE_TRAIT(parent, TRAIT_DEFACED, REF(this));
        RegisterSignal(parent, COMSIG_ATOM_ITEM_INTERACTION, nameof(on_drawn));
        UnregisterSignal(parent, Dm.list(COMSIG_ATOM_EXAMINE, COMSIG_COMPONENT_CLEAN_ACT, COMSIG_ATOM_UPDATE_OVERLAYS));
        source.update_appearance(UPDATE_OVERLAYS);
    }

    /// See it there
    // tg: code/datums/components/defaceable.dm:98
    [SignalHandler]
    public virtual void on_examined(atom source = null, mob user = null, DmList examine_list = null)
    {
        Dm.append(ref examine_list, span_notice(Dm.text("Someone has crudely drawn [] on [].", drawing_of, source.p_them())));
    }
}
