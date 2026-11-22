// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/surgery_tray.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps G3 (D291) had the spawner; gaps P1 (D298): the whole file. The tray and its cart: drawing a random tool by hand, the
// tools drawn on it (the best of each kind), folding it into a tray and deploying it as a cart, taking it apart. The screentip
// (add_context, register_context) stays a live-partial row (D264). The var blocks are generated.

/**
 * Surgery Trays
 * A storage object that displays tools in its contents based on tier, better tools are more visible.
 * Can be folded up and carried. Click it to draw a random tool.
 */
// tg: code/modules/surgery/surgery_tray.dm:6
public partial class obj_item_surgery_tray : obj_item
{
    /// Fills the tray with items it should contain on creation
    // tg: code/modules/surgery/surgery_tray.dm:25
    public virtual async DmTask populate_contents()
    {
        foreach (object obj in Dm.each(starting_items))
            Dm.@new((Type)obj, this);
        update_appearance(UPDATE_ICON);
        return;
    }

    // tg: code/modules/surgery/surgery_tray.dm:31
    [DmParams("mapload", "effect_spawner")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object effect_spawner = Dm.arg(__extra, 0) ?? FALSE;
        object dot = base.Initialize(mapload, Dm.forward(__extra, effect_spawner));
        Waiting.NoSleep(AddElement(typeof(datum_element_drag_pickup)));
        Waiting.NoSleep(create_storage(storage_type: typeof(datum_storage_surgery_tray)));
        Waiting.NoSleep(populate_contents());
        Dm.Deferred("code/modules/surgery/surgery_tray.dm:36", "Initialize: register_context(): screentip contexts (M10+)");
        Waiting.NoSleep(set_tray_mode(is_portable));
        return dot;
    }

    // tg: code/modules/surgery/surgery_tray.dm:45
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(is_portable) ? "tray" : "medicart";
        return dot;
    }

    // tg: code/modules/surgery/surgery_tray.dm:49
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        if (Dm.truthy(is_portable))
            desc = I18n.T("The wheels and bottom storage of this medical cart have been stowed away, leaving a cumbersome tray in its place.");
        else
            desc = (string)Dm.initial(this, (obj_item_surgery_tray o) => o.desc);
        return dot;
    }

    // tg: code/modules/surgery/surgery_tray.dm:57
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, Dm.truthy(is_portable)
            ? span_notice(I18n.T("You can click and drag it to yourself to pick it up, then use it in your hand to make it a cart!"))
            : span_notice(I18n.T("You can click and drag it to yourself to turn it into a tray!")));
        Dm.append(ref dot, span_notice(I18n.T("The top is <b>screwed</b> on.")));
        return dot;
    }

    // tg: code/modules/surgery/surgery_tray.dm:64
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        // assoc list of all overlays, key = the item generating the overlay, value = the overlay string
        DmList surgery_overlays = Dm.list();
        // assoc list of tool behaviors to fastest toolspeed of that type we already have
        // easy way for us to check if there are any lower quality tools within
        DmList recorded_tool_speeds = Dm.list();
        // handle drapes separately so they're always on the bottom
        if (Dm.truthy(Dm.locate<obj_item_surgical_drapes>(contents)))
            Dm.append(ref dot, "drapes");
        // compile all the overlays from items inside us
        foreach (obj_item surgery_tool in Dm.each<obj_item>(this))
        {
            // the overlay we will use if we want to display this one
            object actual_overlay = surgery_tool.get_surgery_tool_overlay(tray_extended: Dm.num(!Dm.truthy(is_portable)));
            if (actual_overlay == null)
                continue; // nothing to see here

            // if we don't have tool behaviour then just record the overlay
            if (!Dm.truthy(length(surgery_tool.get_all_tool_behaviours())))
            {
                surgery_overlays[surgery_tool] = actual_overlay;
                continue;
            }

            // if we have at least one tool behaviour, check if we already recorded a faster one
            foreach (object surgery_tool_type in Dm.each(surgery_tool.get_all_tool_behaviours()))
            {
                float highest_speed = LAZYACCESS(recorded_tool_speeds, surgery_tool_type) is var hs && Dm.truthy(hs) ? Dm.num(hs) : INFINITY; // bigger number = slower
                if (Dm.num(surgery_tool.toolspeed) > highest_speed)
                    continue;
                // the existing tool was worse than us, ditch it
                Dm.remove(ref surgery_overlays, surgery_tool_type);
                LAZYSET(ref recorded_tool_speeds, surgery_tool_type, surgery_tool.toolspeed);
                surgery_overlays[surgery_tool_type] = actual_overlay;
            }
        }

        foreach (object surgery_tool in Dm.each(surgery_overlays))
            Dm.combine(ref dot, surgery_overlays[surgery_tool]);
        return dot;
    }

    ///Sets the surgery tray's deployment state. Silent if user is null.
    // tg: code/modules/surgery/surgery_tray.dm:100
    public virtual async DmTask set_tray_mode(object new_mode = null, mob user = null)
    {
        is_portable = Dm.num(new_mode);
        density = Dm.num(!Dm.truthy(is_portable));
        if (user != null)
            user.visible_message(span_notice(I18n.T("[] [] []'s wheels.", user, Dm.truthy(is_portable) ? I18n.T("retracts") : I18n.T("extends"), this)), span_notice(I18n.T("You [] []'s wheels.", Dm.truthy(is_portable) ? I18n.T("retract") : I18n.T("extend"), this)));

        if (Dm.truthy(is_portable))
        {
            Dm.combine(ref interaction_flags_item, INTERACT_ITEM_ATTACK_HAND_PICKUP);
            Dm.combine(ref pass_flags, PASSTABLE);
            RemoveElement(typeof(datum_element_noisy_movement));
        }
        else
        {
            Dm.mask(ref interaction_flags_item, Dm.bitnot(INTERACT_ITEM_ATTACK_HAND_PICKUP));
            Dm.mask(ref pass_flags, Dm.bitnot(PASSTABLE));
            await AddElement(typeof(datum_element_noisy_movement));
        }

        update_appearance();
    }

    // tg: code/modules/surgery/surgery_tray.dm:117
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!Dm.truthy(is_portable))
            await set_tray_mode(TRUE, user);
    }

    // tg: code/modules/surgery/surgery_tray.dm:122
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        turf placement_turf = get_turf(user);
        if (isgroundlessturf(placement_turf) || isclosedturf(placement_turf))
        {
            balloon_alert(user, I18n.T("can't deploy!"));
            return TRUE;
        }
        if (!Dm.truthy(await user.transferItemToLoc(this, placement_turf)))
        {
            balloon_alert(user, I18n.T("tray stuck!"));
            return TRUE;
        }
        await set_tray_mode(FALSE, user);
        return dot;
    }

    // tg: code/modules/surgery/surgery_tray.dm:136
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        mob_living living_user = (mob_living)user;
        if (!Dm.truthy(await living_user.can_perform_action(this, NEED_HANDS)))
            return await base.attack_hand(user, modifiers, __extra);
        if (!Dm.truthy(length(contents)))
            balloon_alert(user, I18n.T("empty!"));
        else
        {
            obj_item grabbies = pick(contents) as obj_item;
            if (Dm.truthy(await atom_storage.remove_single(user, grabbies, drop_location() as atom)))
                await living_user.put_in_hands(grabbies);
        }
        return TRUE;
    }

    // tg: code/modules/surgery/surgery_tray.dm:147
    public override async DmTask<object> screwdriver_act_secondary(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.screwdriver_act_secondary(user, tool);
        tool.play_tool_sound(this);
        to_chat(user, span_notice(I18n.T("You begin taking apart [].", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, 1 * SECONDS)))
            return dot;
        await deconstruct(TRUE);
        to_chat(user, span_notice(I18n.T("[] has been taken apart.", this)));
        return dot;
    }

    // tg: code/modules/surgery/surgery_tray.dm:156
    public override async DmTask dump_contents(params object[] __extra)
    {
        atom drop_point = drop_location() as atom;
        foreach (atom_movable tool in Dm.each_cast<atom_movable>(contents))
            await tool.forceMove(drop_point);
    }

    // tg: code/modules/surgery/surgery_tray.dm:161
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        await dump_contents();
        Dm.@new<obj_item_stack_rods>(drop_location(), 2);
        Dm.@new<obj_item_stack_sheet_mineral_silver>(drop_location());
    }
}

// tg: code/modules/surgery/surgery_tray.dm:228
public partial class obj_effect_spawner_surgery_tray : obj_effect_spawner
{
    // tg: code/modules/surgery/surgery_tray.dm:239
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (prob(toolbox_chance))
        {
            Dm.@new(rare_toolbox_replacement, loc);
            return dot;
        }
        Dm.@new(tray_to_spawn, loc, TRUE);
        return dot;
    }
}
