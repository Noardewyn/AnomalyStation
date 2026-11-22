// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/floor_placeable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 lighting slice L1 (D168): the whole file (candles carry it). The var block is generated.

/**
 * Simple behaviour for allowing the item to be placed on a specific position on an open turf by clicking there.
 */
// tg: code/datums/elements/floor_placeable.dm:4
public partial class datum_element_floor_placeable : datum_element
{
    // tg: code/datums/elements/floor_placeable.dm:6
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;

        RegisterSignal(target, COMSIG_ATOM_EXAMINE_TAGS, nameof(get_examine_tags));
        RegisterSignal(target, COMSIG_ITEM_INTERACTING_WITH_ATOM, nameof(on_interact_with_atom));
        return dot;
    }

    // tg: code/datums/elements/floor_placeable.dm:14
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, Dm.list(
            COMSIG_ATOM_EXAMINE_TAGS,
            COMSIG_ITEM_INTERACTING_WITH_ATOM
        ));
        return dot;
    }

    // tg: code/datums/elements/floor_placeable.dm:21
    [SignalHandler]
    public virtual void get_examine_tags(atom source = null, mob user = null, DmList examine_list = null)
    {
        examine_list["floor placeable"] = "This item can be placed directly on the floor.";
    }

    // tg: code/datums/elements/floor_placeable.dm:25
    [SignalHandler]
    public virtual object on_interact_with_atom(obj_item source = null, mob_living user = null, atom interacting_with = null, DmList modifiers = null)
    {
        if (!isopenturf(interacting_with))
            return NONE;
        if (Dm.truthy(user.combat_mode))
            return NONE;
        if (Dm.truthy(Dm.bitand(source.item_flags, ABSTRACT)))
            return NONE;

        float x_offset = 0;
        float y_offset = 0;
        // Items are centered by default, but we move them if click ICON_X and ICON_Y are available
        if (Dm.truthy(LAZYACCESS(modifiers, ICON_X)) && Dm.truthy(LAZYACCESS(modifiers, ICON_Y)))
        {
            // Clamp it so that the icon never moves more than 16 pixels in either direction (thus leaving the turf)
            x_offset = Dm.num(clamp(Dm.num(text2num(LAZYACCESS(modifiers, ICON_X))) - 16, -(ICON_SIZE_X * 0.5f), ICON_SIZE_X * 0.5f));
            y_offset = Dm.num(clamp(Dm.num(text2num(LAZYACCESS(modifiers, ICON_Y))) - 16, -(ICON_SIZE_Y * 0.5f), ICON_SIZE_Y * 0.5f));
        }

        if (!Dm.truthy(Waiting.NoSleep(user.transfer_item_to_turf(source, interacting_with as turf, x_offset, y_offset, silent: FALSE))))
            return ITEM_INTERACT_BLOCKING;
        return ITEM_INTERACT_SUCCESS;
    }
}
