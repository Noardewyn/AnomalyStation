// Translated from tgstation (snapshot 1d99bd7f): code/modules/manufactorio/_manufacturing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E16: the manufacturing machines' base. A machine takes what a conveyor pushes into it (Bumped), passes what it makes
// to the machine or the tile in a direction (send_resource), merges stacks in its contents, turns with the simple rotation element,
// anchors with a wrench (and then joins the cable under it), opens with a screwdriver and deconstructs with a crowbar; the in and out
// arrows (generate_io_overlays) are the airlock's unrestricted-side arrows, tinted. The examine is translated (X1)
// and the arrows' emissive copies (DEVIATION DV12). The var blocks are generated.
//
// The slot of send_resource types its second parameter `atom` (tg's `atom/what_or_dir`), but every caller passes a direction
// number and the crafter, sorter and unloader a direction only: it is `object` (rules 3.5's "-" cell; translation-rules Appendix A).
// tg's `contents - circuit` is `contents - circuit_value` here: the machine's board instance (circuit is generated as its type,
// _machinery.cs's GAP(types)).

public static partial class GlobalProcs
{
    // tg: code/modules/manufactorio/_manufacturing.dm:1
    public const float MANUFACTURING_FAIL = 0;
    // tg: code/modules/manufactorio/_manufacturing.dm:2
    public const float MANUFACTURING_SUCCESS = 1;
    // tg: code/modules/manufactorio/_manufacturing.dm:4
    public const float MANUFACTURING_TURF_LAG_LIMIT = 10; // max items on a turf before we consider it full
}

// tg: code/modules/manufactorio/_manufacturing.dm:6
public partial class obj_machinery_power_manufacturing : obj_machinery_power
{
    // tg: code/modules/manufactorio/_manufacturing.dm:16
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(may_be_moved))
            Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation)));
        if (Dm.truthy(anchored))
            connect_to_network();
        return dot;
    }

    // tg: code/modules/manufactorio/_manufacturing.dm:23
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(may_be_moved))
            Dm.append(ref dot, I18n.T("It receives power via cable, but certain buildings do not need power."));
        Dm.append(ref dot, Dm.truthy(length(Dm.sub(contents, circuit_value))) ? I18n.T("It contains:") : I18n.T("It contains no items."));
        foreach (atom_movable thing in Dm.each_cast<atom_movable>(Dm.sub(contents, circuit_value)))
        {
            string text = thing.name;
            obj_item_stack possible_stack = thing as obj_item_stack;
            if (possible_stack != null)
                text = I18n.T("[] []", possible_stack.amount, text);
            Dm.append(ref dot, text);
        }
        return dot;
    }

    // tg: code/modules/manufactorio/_manufacturing.dm:35
    public override object Bumped(atom_movable bumped_atom = null) //attempt to put in whatever is pushed into us via conveyor
    {
        object dot = base.Bumped(bumped_atom);
        if ((!Dm.truthy(allow_mob_bump_intake) && ismob(bumped_atom)) || !Dm.truthy(anchored)) //only uncomment if youre brave
            return dot;
        obj_machinery_conveyor conveyor = Dm.locate<obj_machinery_conveyor>(bumped_atom.loc);
        if (conveyor == null)
            return dot;
        Waiting.NoSleep(receive_resource(bumped_atom, bumped_atom.loc, get_dir(this, bumped_atom)));
        return dot;
    }

    // tg: code/modules/manufactorio/_manufacturing.dm:44
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.wrench_act(user, tool);
        if (!Dm.truthy(may_be_moved))
            return dot;
        await default_unfasten_wrench(user, tool);
        if (Dm.truthy(anchored))
            connect_to_network();
        else
            disconnect_from_network();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/manufactorio/_manufacturing.dm:55
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/manufactorio/_manufacturing.dm:58
    public override async DmTask setDir(object newdir = null)
    {
        await base.setDir(newdir);
        update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/modules/manufactorio/_manufacturing.dm:62
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/manufactorio/_manufacturing.dm:65
    public virtual object generate_io_overlays(object direction = null, object color = null, object offsets_override = null)
    {
        DmList dir_offset;
        if (offsets_override is DmList)
            dir_offset = (DmList)offsets_override;
        else
        {
            dir_offset = (DmList)dir2offset(direction);
            dir_offset[1] = Dm.num(dir_offset[1]) * 32;
            dir_offset[2] = Dm.num(dir_offset[2]) * 32;
        }
        image nonemissive = image(icon: Dm.resource("icons/obj/doors/airlocks/station/overlays.dmi"), icon_state: Dm.text("unres_[]", direction));
        nonemissive.pixel_w = Dm.num(dir_offset[1]);
        nonemissive.pixel_z = Dm.num(dir_offset[2]);
        nonemissive.color = color;
        // DEVIATION(DV12): the emissive copy, emissive_appearance(nonemissive.icon, nonemissive.icon_state, offset_spokesman = src,
        // alpha = nonemissive.alpha) at the same offsets, is not created (the emissive plane is not ported); the list keeps only the arrow.
        Dm.Deferred("code/modules/manufactorio/_manufacturing.dm:76", "generate_io_overlays: emissive_appearance of the arrow (DEVIATION DV12)");
        return Dm.list(nonemissive);
    }

    /// Returns whatever object it may output, or null if it cant do that
    // tg: code/modules/manufactorio/machines/storagebox.dm:9  (tg defines /obj/machinery/power/manufacturing/request_resource twice,
    // empty at _manufacturing.dm:82 and here, the later definition, which DM keeps: "returns last inserted item")
    public virtual object request_resource() //returns last inserted item
    {
        DmList real_contents = (DmList)Dm.sub(contents, circuit_value);
        if (!Dm.truthy(Dm.length(real_contents)))
            return null;
        return real_contents[Dm.length(real_contents)];
    }

    // tg: code/modules/manufactorio/_manufacturing.dm:86
    public virtual async DmTask<object> receive_resource(atom_movable receiving = null, atom from = null, object receive_dir = null)
    {
        throw new DmRuntimeException("Unimplemented!"); //check can_receive_resource here
    }

    //use dir please
    // tg: code/modules/manufactorio/_manufacturing.dm:90
    public virtual async DmTask<object> send_resource(atom_movable sending = null, object what_or_dir = null)
    {
        if (isobj(what_or_dir))
        {
            obj_machinery_power_manufacturing target = what_or_dir as obj_machinery_power_manufacturing;
            return await target.receive_resource(sending, this, get_step(this, Dm.num(what_or_dir)));
        }
        turf next_turf = isturf(what_or_dir) ? (turf)what_or_dir : get_step(this, Dm.num(what_or_dir));
        obj_machinery_power_manufacturing manufactury = Dm.locate<obj_machinery_power_manufacturing>(next_turf);
        if (manufactury != null)
        {
            if (!Dm.truthy(manufactury.anchored))
                return MANUFACTURING_FAIL;
            return await manufactury.receive_resource(sending, this, isturf(what_or_dir) ? get_dir(this, what_or_dir) : what_or_dir);
        }
        if (Dm.truthy(await next_turf.is_blocked_turf(exclude_mobs: TRUE, source_atom: sending)) && !ischasm(next_turf))
            return MANUFACTURING_FAIL;
        if (Dm.length(get_overfloor_objects(next_turf)) >= MANUFACTURING_TURF_LAG_LIMIT)
            return MANUFACTURING_FAIL;
        if (sending == null)
            return MANUFACTURING_SUCCESS; // for the sake of being used as a check
        if (sending.loc == null || !Dm.truthy(await sending.Move(next_turf, get_dir(this, next_turf))))
            await sending.forceMove(next_turf);
        return MANUFACTURING_SUCCESS;
    }

    /// Checks if this stack (if not a stack does not do anything) can merge WITHOUT creating two stacks in contents
    // tg: code/modules/manufactorio/_manufacturing.dm:111
    public virtual object may_merge_in_contents(obj_item_stack stack = null)
    {
        if (stack == null)
            return null;
        foreach (obj_item_stack other in Dm.each<obj_item_stack>(Dm.sub(contents, circuit_value)))
        {
            if (!Dm.truthy(stack.can_merge(other)))
                continue;
            if (Dm.num(other.amount) + Dm.num(stack.amount) <= Dm.num(other.max_amount))
                return other;
        }
        return null;
    }

    // tg: code/modules/manufactorio/_manufacturing.dm:120
    public virtual async DmTask<object> may_merge_in_contents_and_do_so(obj_item_stack stack = null)
    {
        object merging_into = may_merge_in_contents(stack);
        if (merging_into == null)
            return null;
        return await stack.merge((obj_item_stack)merging_into);
    }

    // tg: code/modules/manufactorio/_manufacturing.dm:126
    public virtual object get_overfloor_objects(turf target = null)
    {
        DmList dot = Dm.list();
        if (target == null)
            target = get_turf(this);
        foreach (atom_movable thing in Dm.each_cast<atom_movable>(target.contents))
        {
            if (thing == this || isliving(thing) || iseffect(thing) || thing.invisibility >= INVISIBILITY_ABSTRACT || HAS_TRAIT(thing, TRAIT_UNDERFLOOR))
                continue;
            Dm.append(dot, thing);
        }
        return dot;
    }
}
