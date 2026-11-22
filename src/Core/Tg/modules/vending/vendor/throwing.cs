// Translated from tgstation (snapshot 1d99bd7f): code/modules/vending/vendor/throwing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package S2: the whole file.

// tg: code/modules/vending/vendor/_vending.dm:37
public partial class obj_machinery_vending
{
    /**
     * Throw an item from our internal inventory out in front of us
     *
     * This is called when we are hacked, it selects a random product from the records that has an amount > 0
     * This item is then created and tossed out in front of us with a visible message
     */
    // tg: code/modules/vending/vendor/throwing.dm:8
    public virtual async DmTask<object> throw_item()
    {
        mob_living target = Dm.locate<mob_living>(view(7, this));
        if (target == null)
            return FALSE;

        obj thrown_item = null;
        foreach (datum_data_vending_product record in Dm.each<datum_data_vending_product>(shuffle(product_records)))
        {
            if (Dm.num(record.amount) <= 0) //Try to use a record that actually has something to dump.
                continue;
            object dump_path = record.product_path;
            if (!Dm.truthy(dump_path))
                continue;
            // Always throw new stuff that costs before free returned stuff, because of the hacking effort and time between throws involved
            bool only_returned_left = Dm.num(record.amount) <= LAZYLEN(record.returned_products);
            thrown_item = await dispense(record, get_turf(this), silent: TRUE, dispense_returned: only_returned_left ? TRUE : FALSE) as obj;
            break;
        }
        if (thrown_item == null)
            return FALSE;

        await pre_throw(thrown_item as obj_item);

        await thrown_item.throw_at(target, 16, 3);
        visible_message(span_danger(I18n.T("[] launches [] at []!", this, thrown_item, target)));
        return TRUE;
    }

    /**
     * A callback called before an item is tossed out
     *
     * Override this if you need to do any special case handling
     *
     * Arguments:
     * * thrown_item - obj/item being thrown
     */
    // tg: code/modules/vending/vendor/throwing.dm:41
    public virtual async DmTask pre_throw(obj_item thrown_item = null)
    {
        return;
    }

    ///Crush the mob that the vending machine got thrown at
    // tg: code/modules/vending/vendor/throwing.dm:46
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        if (isliving(hit_atom))
            Waiting.NoSleep(tilt(fatty: hit_atom));
        return base.throw_impact(hit_atom, throwingdatum, __extra);
    }

    // tg: code/modules/vending/vendor/throwing.dm:51
    public override async DmTask<object> hitby(atom_movable hitting_atom = null, object skipcatch = null, object hitpush = null, object blocked = null, datum_thrownthing throwingdatum = null)
    {
        object dot = await base.hitby(hitting_atom, skipcatch, hitpush, blocked, throwingdatum);
        mob_living living_mob = hitting_atom as mob_living;
        if (Dm.truthy(tilted) || living_mob == null || !prob(20 * (Dm.num(throwingdatum.speed) - Dm.num(living_mob.throw_speed)))) // hulk throw = +20%, neckgrab throw = +20%
            return dot;

        await tilt(living_mob);
        return dot;
    }
}
