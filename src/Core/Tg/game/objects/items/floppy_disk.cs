// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/floppy_disk.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The file whole (engineering E16 the manipulator task disk's two procs, gaps J5 the disk's own procs, gaps DV2 the rest): the floppy
// disk's write-protect tab, its sticker written with a pen or picked from a radial of tg's stickers, and the stacks of disks (a disk
// used on a disk, up to ten, drawn as the disks piled two pixels apart; the top one pulled off by the right hand, the stack spilling
// when thrown), the flat parcel with a data disk inside, and the disk's reskin menu (the `/datum/atom_skin/floppy_disk` colours). The var
// blocks are generated (D111).

// tg: code/game/objects/items/floppy_disk.dm:362
public partial class obj_item_disk_manipulator : obj_item_disk
{
    // tg: code/game/objects/items/floppy_disk.dm:368
    public virtual object set_tasks(DmList new_tasks_data = null)
    {
        if (Dm.truthy(read_only))
            return FALSE;
        tasks_data = new_tasks_data != null ? new_tasks_data : Dm.list();
        return TRUE;
    }

    // tg: code/game/objects/items/floppy_disk.dm:374
    public virtual object get_tasks()
    {
        return tasks_data?.Copy() is DmList copy && Dm.truthy(copy) ? copy : Dm.list();
    }
}

// tg: code/game/objects/items/floppy_disk.dm:41
public partial class obj_item_disk : obj_item
{
    private const string STARTING_STICKER = "o_empty"; // tg: floppy_disk.dm:4
    private const int FLOPPY_MAX_TEXT_LENGTH = 30; // tg: floppy_disk.dm:3  MAX_TEXT_LENGTH

    // tg: code/game/objects/items/floppy_disk.dm:54  Sticker icons to choose from (as icon states)
    public static DmList sticker_variants => TgState.Current.obj_item_disk__sticker_variants ??= Dm.list(
        Dm.kv("One", "o_one"),
        Dm.kv("Two", "o_two"),
        Dm.kv("Three", "o_three"),
        Dm.kv("Four", "o_four"),
        Dm.kv("Five", "o_five"),
        Dm.kv("Six", "o_six"),
        Dm.kv("Seven", "o_seven"),
        Dm.kv("Eight", "o_eight"),
        Dm.kv("Nine", "o_nine"),
        Dm.kv("Zero", "o_zero"),
        Dm.kv("A", "o_A"),
        Dm.kv("B", "o_B"),
        Dm.kv("C", "o_C"),
        Dm.kv("D", "o_D"),
        Dm.kv("E", "o_E"),
        Dm.kv("F", "o_F"),
        Dm.kv("Code", "o_code"),
        Dm.kv("DNA — Green", "o_dna1"),
        Dm.kv("DNA — Red", "o_dna2"),
        Dm.kv("Medical", "o_medical"),
        Dm.kv("Holographic", "o_holo"));

    // tg: code/game/objects/items/floppy_disk.dm:87
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/game/objects/items/floppy_disk.dm:91
    public override async DmTask setup_reskins()
    {
        await AddComponent(typeof(datum_component_reskinable_item), typeof(datum_atom_skin_floppy_disk), Dm.named("infinite", TRUE));
    }

    // tg: code/game/objects/items/floppy_disk.dm:94
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (Dm.truthy(sticker_icon_state))
            dot.Add(sticker_icon_state);
        return dot;
    }

    // tg: code/game/objects/items/floppy_disk.dm:99
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        dot.Add(span_notice(I18n.T("The write-protect tab is set to [].", span_bold(Dm.truthy(read_only) ? I18n.T("protected") : I18n.T("unprotected")))));
        if (Dm.truthy(custom_description))
        {
            dot.Add(span_notice(I18n.T("There's something scribbled on the sticker:")));
            dot.Add(span_notice(span_italics(Dm.text("\"[]\"", custom_description))));
        }
        return dot;
    }

    // tg: code/game/objects/items/floppy_disk.dm:107
    public override async DmTask<object> tool_act(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (tool is obj_item_pen)
        {
            if (!Dm.eq(sticker_icon_state, STARTING_STICKER))
            {
                to_chat(user, span_warning(I18n.T("You can't add anything else!")));
                return ITEM_INTERACT_FAILURE;
            }
            object newdescription = sanitize_text(await tgui_input_text(user, I18n.T("What do you want to write?"), I18n.T("Floppy Disk"), max_length: FLOPPY_MAX_TEXT_LENGTH, multiline: TRUE));
            if (!Dm.truthy(newdescription))
                return ITEM_INTERACT_FAILURE;
            if (!Dm.truthy(await can_interact(user)))
                return ITEM_INTERACT_FAILURE;
            playsound(this, SFX_WRITING_PEN, 30);
            to_chat(user, span_notice(I18n.T("You sign the [].", this)));
            custom_description = newdescription;
            set_sticker_icon_state(pick("o_text1", "o_text2", "o_text3"));
            return ITEM_INTERACT_SUCCESS;
        }
        return await base.tool_act(user, tool, modifiers);
    }

    // tg: code/game/objects/items/floppy_disk.dm:129
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(read_only_locked))
        {
            to_chat(user, span_warning(I18n.T("The write-protect tab seems to be stuck in place!")));
            return null;
        }
        read_only = Dm.truthy(read_only) ? FALSE : TRUE;
        to_chat(user, span_notice(I18n.T("You flip the write-protect tab to [].", span_bold(Dm.truthy(read_only) ? I18n.T("protected") : I18n.T("unprotected")))));
        return null;
    }

    // tg: code/game/objects/items/floppy_disk.dm:136
    public override async DmTask<object> click_alt_secondary(mob user = null)
    {
        if (!Dm.eq(sticker_icon_state, STARTING_STICKER))
            return CLICK_ACTION_BLOCKING;

        if (!Dm.truthy(LAZYLEN(sticker_variants)))
            return CLICK_ACTION_BLOCKING;

        DmList items = Dm.list();
        foreach (object variant_name in Dm.each(sticker_variants))
        {
            object icon_state_name = sticker_variants[variant_name];
            image item_image = image(icon: icon, icon_state: icon_state);

            image overlay_preview = image(icon: icon, icon_state: icon_state_name);
            Dm.append(ref item_image.overlays, overlay_preview);
            Dm.append(ref items, Dm.list(Dm.kv(Dm.text("[]", variant_name), item_image)));
        }

        object pick = await show_radial_menu(user, this, items, custom_check: Dm.@new<datum_callback>(this, nameof(check_sticker_menu), user), radius: 38, require_near: TRUE);
        if (!Dm.truthy(pick))
            return CLICK_ACTION_BLOCKING;
        if (!Dm.truthy(sticker_variants[pick]))
            return CLICK_ACTION_BLOCKING;

        set_sticker_icon_state(sticker_variants[pick]);
        to_chat(user, span_notice(I18n.T("You change the sticker on [] to '[]'.", this, pick)));
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/floppy_disk.dm:163  Can we select a new sticker?
    public virtual object check_sticker_menu(mob user = null)
    {
        if (QDELETED(this))
            return FALSE;
        if (!Dm.eq(sticker_icon_state, STARTING_STICKER))
            return FALSE;
        if (user is not mob)
            return FALSE;
        if (Dm.truthy(user.incapacitated))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/objects/items/floppy_disk.dm:175  Sets the sticker icon state and updates the appearance
    public virtual void set_sticker_icon_state(object new_icon_state = null)
    {
        sticker_icon_state = new_icon_state as string;
        update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/game/objects/items/floppy_disk.dm:179
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_disk_stack held_stack)
        {
            obj_item_disk_stack new_stack = Dm.@new<obj_item_disk_stack>(get_turf(this));
            object should_put_in_hand = user.is_holding(this);

            await new_stack.add_to_stack(user, this);
            await new_stack.merge_stacks(user, held_stack);

            if (Dm.truthy(should_put_in_hand))
                await user.put_in_hands(new_stack);
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_disk)
        {
            obj_item_disk_stack new_stack = Dm.@new<obj_item_disk_stack>(get_turf(this));
            new_stack.pixel_x = pixel_x;
            new_stack.pixel_y = pixel_y;

            bool should_put_in_hand = Dm.truthy(user.is_holding(this)) && Dm.truthy(user.is_holding(tool));
            await new_stack.add_to_stack(user, this);
            await new_stack.add_to_stack(user, (obj_item_disk)tool);
            new_stack.update_appearance(UPDATE_OVERLAYS);
            if (should_put_in_hand)
                await user.put_in_hands(new_stack);
            return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }
}

// tg: code/game/objects/items/floppy_disk.dm:205
public partial class obj_item_disk_stack : obj_item
{
    private const int MAX_DISK_STACK_SIZE = 10; // tg: floppy_disk.dm:1
    private const int STACK_PIXEL_STEP = 2; // tg: floppy_disk.dm:2

    // tg: code/game/objects/items/floppy_disk.dm:219
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_MOVABLE_THROW_LANDED, nameof(on_throw_land));
        return dot;
    }

    // tg: code/game/objects/items/floppy_disk.dm:223
    public override object Destroy(float? force = null)
    {
        QDEL_LIST(stacked_disks);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/floppy_disk.dm:227
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("There are [] disks in the stack.", span_bold(Dm.text("[]", length(stacked_disks))))));
        return dot;
    }

    // tg: code/game/objects/items/floppy_disk.dm:231
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_disk_stack other_stack)
            return await merge_stacks(user, other_stack);

        if (tool is obj_item_disk new_disk)
            return await add_to_stack(user, new_disk);
        return null;
    }

    // tg: code/game/objects/items/floppy_disk.dm:238
    public virtual async DmTask<object> add_to_stack(mob_living user = null, obj_item_disk newdisk = null)
    {
        if (Dm.num(length(stacked_disks)) >= MAX_DISK_STACK_SIZE)
        {
            balloon_alert(user, I18n.T("can't add more!"));
            return ITEM_INTERACT_BLOCKING;
        }

        await newdisk.forceMove(this);
        Dm.append(ref stacked_disks, newdisk);
        balloon_alert(user, I18n.T("added to top"));
        update_appearance(UPDATE_OVERLAYS);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/floppy_disk.dm:249
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        float iteration_count = 1;
        foreach (obj_item_disk disk_in_stack in Dm.each<obj_item_disk>(stacked_disks))
        {
            mutable_appearance stacked_disk_appearance = (mutable_appearance)mutable_appearance(disk_in_stack.icon, disk_in_stack.icon_state, disk_in_stack.layer + (0.1f * iteration_count));
            stacked_disk_appearance.pixel_z += iteration_count * STACK_PIXEL_STEP;

            if (Dm.truthy(LAZYLEN(disk_in_stack.overlays)))
                stacked_disk_appearance.overlays = disk_in_stack.overlays.Copy();
            Dm.append(ref dot, stacked_disk_appearance);
            iteration_count++;
        }
        return dot;
    }

    // tg: code/game/objects/items/floppy_disk.dm:261
    public virtual async DmTask<object> pop_top_disk(mob_living user = null)
    {
        if (!Dm.truthy(length(stacked_disks)))
            return FALSE;

        obj_item_disk top = stacked_disks[length(stacked_disks)] as obj_item_disk;
        await user.put_in_hands(top);

        if (Dm.num(length(stacked_disks)) > 1)
        {
            update_appearance(UPDATE_OVERLAYS);
            balloon_alert(user, I18n.T("removed top disk"));
            return TRUE;
        }

        obj_item_disk last_disk = stacked_disks[1] as obj_item_disk;
        object was_in_hand = user.is_holding(this);
        if (Dm.truthy(was_in_hand))
        {
            await last_disk.forceMove(user);
            await user.put_in_hands(last_disk);
        }
        else
        {
            turf T = get_turf(this);
            await last_disk.forceMove(T);
            last_disk.pixel_x = pixel_x;
            last_disk.pixel_y = pixel_y;
        }

        last_disk.balloon_alert(user, I18n.T("removed top disk"));
        qdel(this);
        return TRUE;
    }

    // tg: code/game/objects/items/floppy_disk.dm:288
    public virtual async DmTask<object> merge_stacks(mob user = null, obj_item_disk_stack diskstack = null)
    {
        float amount_counter = 0;
        DmList moved_disks = Dm.list();

        foreach (obj_item_disk each_disk in Dm.each_cast<obj_item_disk>(diskstack.stacked_disks.Copy()))
        {
            if (Dm.num(length(stacked_disks)) >= MAX_DISK_STACK_SIZE)
                break;

            await each_disk.forceMove(this);
            Dm.append(ref stacked_disks, each_disk);
            Dm.append(ref moved_disks, each_disk);
            amount_counter += 1;
        }

        Dm.remove(ref diskstack.stacked_disks, moved_disks);
        diskstack.update_appearance(UPDATE_OVERLAYS);

        if (!Dm.truthy(amount_counter))
        {
            balloon_alert(user, I18n.T("no space!"));
            return ITEM_INTERACT_BLOCKING;
        }

        update_appearance(UPDATE_OVERLAYS);
        to_chat(user, span_notice(I18n.T("You merge two stacks of disks together.")));

        if (!Dm.truthy(length(diskstack.stacked_disks)))
            qdel(diskstack);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/floppy_disk.dm:315
    [SignalHandler]
    public virtual void on_throw_land()
    {
        Waiting.Detach(spread());
    }

    // tg: code/game/objects/items/floppy_disk.dm:319
    public virtual async DmTask spread()
    {
        if (!Dm.truthy(length(stacked_disks)))
            return;

        turf landing = get_turf(this);
        foreach (obj_item_disk each_disk in Dm.each_cast<obj_item_disk>(stacked_disks.Copy()))
        {
            await each_disk.forceMove(landing);
            await each_disk.throw_at(get_step(this, Dm.num(pick(NORTH, NORTHEAST, EAST, SOUTHEAST, SOUTH, SOUTHWEST, WEST, NORTHWEST))), 1, 0.8f);
        }

        visible_message(span_warning(I18n.T("The stack falls apart!")));
        qdel(this);
    }

    // tg: code/game/objects/items/floppy_disk.dm:331
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        if (Dm.truthy(await pop_top_disk(user as mob_living)))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        return await base.attack_hand_secondary(user, modifiers);
    }

    // tg: code/game/objects/items/floppy_disk.dm:337  Handle disks leaving the stack through other means
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone is not obj_item_disk)
            return dot;
        Dm.remove(ref stacked_disks, gone);
        update_appearance(UPDATE_OVERLAYS);
        if (!Dm.truthy(length(stacked_disks)))
            qdel(this);
        return dot;
    }

    // tg: code/game/objects/items/floppy_disk.dm:349
    public override object can_be_package_wrapped()
    {
        return FALSE;
    }
}

// tg: code/game/objects/items/floppy_disk.dm:346  (D325: the package wrapper wraps a disk though it is no storage)
public partial class obj_item_disk : obj_item
{
    public override object can_be_package_wrapped()
    {
        return TRUE;
    }
}

// tg: code/game/objects/items/floppy_disk.dm:352
public partial class obj_item_delivery_small_floppy : obj_item_delivery_small
{
    // tg: code/game/objects/items/floppy_disk.dm:358
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.@new<obj_item_disk_data>(this);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/floppy_disk.dm:54
    internal DmList obj_item_disk__sticker_variants;
}
