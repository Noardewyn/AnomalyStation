// Translated from tgstation (snapshot 1d99bd7f): code/datums/storage/subtypes/others/rped.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package OD3, D333): the RPED's storage, which takes stock parts, machine and computer boards and a few
// stacks (glass, plasteel, cable and up to 30 bluespace crystals) and dumps its lowest-rated parts when emptied; the bluespace
// and the cyborgs' compact kinds. The var blocks are generated (D111).

/**
 *Storage component used for RPEDs. Rather than manually setting everything with a get_part_rating() value, we just check if it has the variable required for insertion.
 */
// tg: code/datums/storage/subtypes/others/rped.dm:9
public partial class datum_storage_rped : datum_storage
{
    //Maximum amount of an specified stack type[see allowed types below] the RPED can carry
    // tg: code/datums/storage/subtypes/others/rped.dm:6  #define MAX_STACK_PICKUP 30
    private const float MAX_STACK_PICKUP = 30;

    /**
     * as of now only these stack components are required to build machines like[thermomachine,crystallizer,electrolyzer]
     * so we limit the rped to pick up only these stack types so players dont cheat and use this as a general storage medium
     */
    // tg: code/datums/storage/subtypes/others/rped.dm:21  var/static/list/allowed_material_types
    public DmList allowed_material_types => TgState.Current.datum_storage_rped__allowed_material_types ??= Dm.list(
        typeof(obj_item_stack_sheet_glass),
        typeof(obj_item_stack_sheet_plasteel),
        typeof(obj_item_stack_cable_coil));

    /**
     * we check if the user is trying to insert any of these bluespace crystal types into the RPED
     * at any point the total sum of all these types in the RPED must be <= MAX_STACK_PICKUP
     */
    // tg: code/datums/storage/subtypes/others/rped.dm:31  var/static/list/allowed_bluespace_types
    public DmList allowed_bluespace_types => TgState.Current.datum_storage_rped__allowed_bluespace_types ??= Dm.list(
        typeof(obj_item_stack_ore_bluespace_crystal),
        typeof(obj_item_stack_sheet_bluespace_crystal));

    // tg: code/datums/storage/subtypes/others/rped.dm:34
    public override object can_insert(obj_item to_insert = null, mob user = null, float? messages = null, float? force = null)
    {
        messages ??= TRUE;
        force ??= FALSE;
        //only stock parts permited
        if (Dm.truthy(to_insert.get_part_rating()))
            return base.can_insert(to_insert, user, messages, force);

        //some exceptions to non stock parts
        // tg: code/datums/storage/subtypes/others/rped.dm:40  var/static/list/obj/item/exceptions (a proc-level static, rules 10.6)
        DmList exceptions = TgState.Current.datum_storage_rped__can_insert__exceptions ??= Dm.list(
            typeof(obj_item_stack),
            typeof(obj_item_circuitboard_machine),
            typeof(obj_item_circuitboard_computer));

        if (!Dm.truthy(is_type_in_list(to_insert, exceptions)))
            return FALSE;

        return base.can_insert(to_insert, user, messages, force);
    }

    // tg: code/datums/storage/subtypes/others/rped.dm:51
    public override object attempt_insert(obj_item to_insert = null, mob user = null, float? @override = null, float? force = null, float? messages = null)
    {
        if (isstack(to_insert))
        {
            //user tried to insert invalid stacktype
            if (!Dm.truthy(is_type_in_list(to_insert, allowed_material_types)) && !Dm.truthy(is_type_in_list(to_insert, allowed_bluespace_types)))
                return FALSE;

            obj_item_stack the_stack = (obj_item_stack)to_insert;
            float present_amount = 0;

            //we try to count & limit how much the user can insert of each type to prevent them from using it as an normal storage medium
            foreach (object o in Dm.each(real_location?.contents))
            {
                if (o is not obj_item_stack stack_content)
                    continue;
                //is user trying to insert any of these listed bluespace stuff
                if (Dm.truthy(is_type_in_list(to_insert, allowed_bluespace_types)))
                {
                    //if yes count total bluespace stuff is the RPED and then compare the total amount to the value the user is trying to insert
                    if (Dm.truthy(is_type_in_list(stack_content, allowed_bluespace_types)))
                        present_amount += Dm.num(stack_content.amount);
                }

                //count other normal stack stuff
                else if (Dm.eq(the_stack.merge_type, stack_content.merge_type))
                {
                    present_amount = Dm.num(stack_content.amount);
                    break;
                }
            }

            float available = MAX_STACK_PICKUP - present_amount;

            //no more storage for this specific stack type
            if (available == 0)
                return FALSE;

            obj_item_stack target = the_stack;
            if (Dm.num(the_stack.amount) > available) //take in only a portion of the stack that can fit in our quota
            {
                target = fast_split_stack(the_stack, available) as obj_item_stack;
                Waiting.NoSleep(target.copy_evidences(the_stack));
            }

            object dot = base.attempt_insert(target, user, @override, force, messages);
            if (!Dm.truthy(dot) && target != the_stack) //in case of failure merge back the split amount into the original
            {
                Waiting.NoSleep(the_stack.add(target.amount));
                qdel(target);
            }

            return dot;
        }

        return base.attempt_insert(to_insert, user, @override, force, messages);
    }

    // tg: code/datums/storage/subtypes/others/rped.dm:93
    public override void mass_empty(datum source = null, mob user = null)
    {
        DmList parts_list = Dm.list();
        foreach (object o in Dm.each(real_location?.contents))
        {
            if (o is obj_item thing)
                Dm.append(ref parts_list, thing);
        }
        if (!Dm.truthy(parts_list.len))
            return;

        float current_lowest_tier = INFINITY;
        parts_list = reverse_range(sortTim(parts_list, "/proc/" + nameof(GlobalProcs.cmp_rped_sort)) as DmList) as DmList;
        current_lowest_tier = Dm.num(((obj_item)parts_list[1]).get_part_rating());
        if (ismob(parent.loc))
            parent.balloon_alert(parent.loc as mob, I18n.T("dropping lowest rated parts..."));

        atom dump_loc = user.drop_location() as atom;
        foreach (object o in Dm.each(parts_list))
        {
            if (o is not obj_item part)
                continue;
            if (Dm.num(part.get_part_rating()) != current_lowest_tier)
                break;
            if (!Dm.truthy(attempt_remove(part, dump_loc, silent: TRUE)))
                continue;
            part.pixel_x = Dm.num(part.base_pixel_x) + rand(-8, 8);
            part.pixel_y = Dm.num(part.base_pixel_y) + rand(-8, 8);
        }
    }
}

public sealed partial class TgState
{
    // tg: code/datums/storage/subtypes/others/rped.dm:21  var/static/list/allowed_material_types (/datum/storage/rped)
    internal DmList datum_storage_rped__allowed_material_types;
    // tg: code/datums/storage/subtypes/others/rped.dm:31  var/static/list/allowed_bluespace_types (/datum/storage/rped)
    internal DmList datum_storage_rped__allowed_bluespace_types;
    // tg: code/datums/storage/subtypes/others/rped.dm:40  var/static/list/obj/item/exceptions (/datum/storage/rped/can_insert)
    internal DmList datum_storage_rped__can_insert__exceptions;
}
