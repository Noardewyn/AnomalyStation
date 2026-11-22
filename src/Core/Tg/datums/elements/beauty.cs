// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/beauty.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI2 (D326): the beauty element, whole (the statues', the engravings' and the other decorations' area beauty). The var block is
// generated (D111).

/**
 * Beauty element. It makes the indoor area the parent is in prettier or uglier depending on the beauty var value.
 * Clean and well decorated areas lead to positive moodlets for passerbies;
 * Shabbier, dirtier ones lead to negative moodlets EXCLUSIVE to characters with the snob quirk.
 */
// tg: code/datums/elements/beauty.dm:6
public partial class datum_element_beauty : datum_element
{
    // beauty_counter (generated as an object: LAZYINITLIST) read as the list it is
    private DmList counter => beauty_counter as DmList;

    // tg: code/datums/elements/beauty.dm:16
    [DmParams("target", "beauty")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object beauty = Dm.arg(__extra, 0);
        object dot = await base.Attach(target, __extra);
        if (!isatom(target) || isarea(target))
            return ELEMENT_INCOMPATIBLE;

        this.beauty = Dm.num(beauty);
        beauty_counter ??= Dm.list(); // LAZYINITLIST(beauty_counter)

        area current_area = get_area((atom)target) as area;
        bool beauty_active = true;
        if (target is atom_movable mov_target)
        {
            bool is_item = isitem(mov_target);
            beauty_active = !is_item || isturf(mov_target.loc);
            if (!Dm.truthy(counter[target]))
            {
                if (is_item)
                    RegisterSignal(mov_target, COMSIG_MOVABLE_MOVED, nameof(on_item_moved));
                if (beauty_active)
                {
                    mov_target.become_area_sensitive(BEAUTY_ELEMENT_TRAIT);
                    RegisterSignal(mov_target, COMSIG_ENTER_AREA, nameof(enter_area));
                    RegisterSignal(mov_target, COMSIG_EXIT_AREA, nameof(exit_area));
                }
            }
        }

        counter[target] = Dm.num(counter[target]) + 1;

        if (current_area != null && !Dm.truthy(current_area.outdoors) && beauty_active)
        {
            current_area.totalbeauty += this.beauty;
            current_area.update_beauty();
        }
        return dot;
    }

    // tg: code/datums/elements/beauty.dm:44
    [SignalHandler]
    public virtual void enter_area(datum source = null, area new_area = null)
    {
        if (Dm.truthy(new_area.outdoors) || HAS_TRAIT(source, TRAIT_BEAUTY_APPLIED))
            return;
        new_area.totalbeauty += beauty * Dm.num(counter[source]);
        new_area.update_beauty();
        ADD_TRAIT(source, TRAIT_BEAUTY_APPLIED, INNATE_TRAIT);
    }

    // tg: code/datums/elements/beauty.dm:53
    [SignalHandler]
    public virtual void exit_area(datum source = null, area old_area = null)
    {
        if (Dm.truthy(old_area.outdoors) || !HAS_TRAIT(source, TRAIT_BEAUTY_APPLIED))
            return;
        old_area.totalbeauty -= beauty * Dm.num(counter[source]);
        old_area.update_beauty();
        REMOVE_TRAIT(source, TRAIT_BEAUTY_APPLIED, INNATE_TRAIT);
    }

    ///Items only contribute to beauty while not inside other objects or mobs (e.g on the floor, on a table etc.).
    // tg: code/datums/elements/beauty.dm:63
    [SignalHandler]
    public virtual void on_item_moved(obj_item source = null, atom old_loc = null, object direction = null, object forced = null)
    {
        bool is_old_turf = isturf(old_loc);
        if (!is_old_turf && isturf(source.loc))
        {
            source.become_area_sensitive(BEAUTY_ELEMENT_TRAIT);
            RegisterSignal(source, COMSIG_ENTER_AREA, nameof(enter_area), TRUE);
            RegisterSignal(source, COMSIG_EXIT_AREA, nameof(exit_area), TRUE);
            enter_area(source, get_area(source.loc) as area);
        }
        else if (is_old_turf && !isturf(source.loc))
        {
            source.lose_area_sensitivity(BEAUTY_ELEMENT_TRAIT);
            UnregisterSignal(source, Dm.list(COMSIG_ENTER_AREA, COMSIG_EXIT_AREA));
            exit_area(source, get_area(old_loc) as area);
        }
    }

    // tg: code/datums/elements/beauty.dm:77
    public override object Detach(datum source = null, params object[] __extra)
    {
        if (!Dm.truthy(counter[source]))
            return base.Detach(source, __extra);

        atom source_atom = (atom)source;
        area current_area = (!isitem(source) || isturf(source_atom.loc)) ? get_area(source_atom) as area : null;
        if (!QDELETED(source)) //lower the 'counter' down by one, update the area, and call parent if it's reached zero.
        {
            counter[source] = Dm.num(counter[source]) - 1;
            if (current_area != null && !Dm.truthy(current_area.outdoors))
            {
                current_area.totalbeauty -= beauty;
                current_area.update_beauty();
            }
            if (Dm.truthy(counter[source]))
                return null;
        }
        else if (current_area != null)
            exit_area(source, current_area);

        UnregisterSignal(source, Dm.list(COMSIG_ENTER_AREA, COMSIG_EXIT_AREA, COMSIG_MOVABLE_MOVED));
        DmList list = counter;
        Dm.remove(ref list, source);
        if (source is atom_movable movable_source)
            movable_source.lose_area_sensitivity(BEAUTY_ELEMENT_TRAIT);
        return null;
    }
}
