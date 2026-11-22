// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/bedsheet_bin.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3c, D316: the uplink's syndicate bedsheet): the bedsheets and the linen bins. The context tips are not
// ported (D264). The var blocks are generated (D111).

/*
CONTAINS:
BEDSHEETS
LINEN BINS
*/

// tg: code/game/objects/structures/bedsheet_bin.dm:7
public partial class obj_item_bedsheet : obj_item
{
    // tg: code/game/objects/structures/bedsheet_bin.dm:40
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_surgery_aid), "bedsheet"));
        Waiting.NoSleep(AddElement(typeof(datum_element_bed_tuckable), mapload, 0, 0, 0));
        if (bedsheet_type == BEDSHEET_DOUBLE)
        {
            stack_amount *= 2;
            dying_key = DYE_REGISTRY_DOUBLE_BEDSHEET;
        }
        // register_context(), register_item_context(): the context tips (D264)
        return dot;
    }

    // tg: code/game/objects/structures/bedsheet_bin.dm:50  add_context, :57  add_item_context: the context tips (D264)

    // tg: code/game/objects/structures/bedsheet_bin.dm:64
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isliving(interacting_with))
            return NONE;
        mob_living to_cover = (mob_living)interacting_with;
        if (to_cover.body_position != LYING_DOWN)
            return ITEM_INTERACT_BLOCKING;
        if (!Dm.truthy(await user.transfer_item_to_turf(this, get_turf(to_cover))))
            return ITEM_INTERACT_BLOCKING;

        balloon_alert(user, I18n.T("covered"));
        coverup(to_cover);
        add_fingerprint(user);

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/bedsheet_bin.dm:79
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        // Handle wirecutters here so we still tear it up in combat mode
        if (!Dm.eq(tool.tool_behaviour, TOOL_WIRECUTTER) && !Dm.truthy(tool.get_sharpness()))
            return NONE;

        // We cannot get free cloth from holograms
        if (((int)Dm.num(flags_1) & HOLOGRAM_1) != 0)
            return ITEM_INTERACT_BLOCKING;

        obj_item_stack shreds = await Dm.new_async(stack_type, get_turf(this), stack_amount) as obj_item_stack;
        if (!QDELETED(shreds)) // Stacks merged
        {
            transfer_fingerprints_to(shreds);
            shreds.add_fingerprint(user);
        }
        to_chat(user, span_notice(I18n.T("You tear [] up.", this)));
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/bedsheet_bin.dm:96
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(await IsReachableBy(user))) //No telekinetic grabbing.
            return null;
        if (user is not mob_living living_user || living_user.body_position != LYING_DOWN)
            return null;
        if (!Dm.truthy(await living_user.transfer_item_to_turf(this, get_turf(this))))
            return null;

        coverup(living_user);
        add_fingerprint(user);
        return null;
    }

    // tg: code/game/objects/structures/bedsheet_bin.dm:107
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        await setDir(REVERSE_DIR(dir));
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/structures/bedsheet_bin.dm:111
    public virtual void coverup(mob_living sleeper = null)
    {
        layer = ABOVE_MOB_LAYER;
        pixel_x = 0;
        pixel_y = 0;
        pixel_z = sleeper.pixel_z; // Account for possible mob elevation
        balloon_alert(sleeper, I18n.T("covered"));
        float angle = sleeper.lying_prev;
        dir = Dm.num(angle2dir(angle + 180)); // 180 flips it to be the same direction as the mob

        signal_sleeper = WEAKREF(sleeper) as datum_weakref;
        RegisterSignal(this, COMSIG_ITEM_PICKUP, nameof(on_pickup));
        RegisterSignal(sleeper, COMSIG_MOVABLE_MOVED, nameof(smooth_sheets));
        RegisterSignal(sleeper, COMSIG_LIVING_SET_BODY_POSITION, nameof(smooth_sheets));
        RegisterSignal(sleeper, COMSIG_QDELETING, nameof(smooth_sheets));
    }

    // tg: code/game/objects/structures/bedsheet_bin.dm:126
    [SignalHandler]
    public virtual void smooth_sheets(mob_living sleeper = null)
    {
        UnregisterSignal(this, COMSIG_ITEM_PICKUP);
        UnregisterSignal(sleeper, COMSIG_MOVABLE_MOVED);
        UnregisterSignal(sleeper, COMSIG_LIVING_SET_BODY_POSITION);
        UnregisterSignal(sleeper, COMSIG_QDELETING);
        balloon_alert(sleeper, I18n.T("smoothed sheets"));
        layer = Dm.initial(this, (obj_item_bedsheet b) => b.layer);
        SET_PLANE_IMPLICIT(this, Dm.initial(this, (obj_item_bedsheet b) => b.plane));
        pixel_z = 0;
        signal_sleeper = null;
    }

    // We need to do this in case someone picks up a bedsheet while a mob is covered up
    // otherwise the bedsheet will disappear while in our hands if the sleeper signals get activated by moving
    // tg: code/game/objects/structures/bedsheet_bin.dm:141
    [SignalHandler]
    public virtual void on_pickup(datum source = null, mob grabber = null)
    {
        mob_living sleeper = signal_sleeper?.resolve() as mob_living;
        UnregisterSignal(this, COMSIG_ITEM_PICKUP);
        UnregisterSignal(sleeper, COMSIG_MOVABLE_MOVED);
        UnregisterSignal(sleeper, COMSIG_LIVING_SET_BODY_POSITION);
        UnregisterSignal(sleeper, COMSIG_QDELETING);
        pixel_z = 0;
        signal_sleeper = null;
    }
}

// tg: code/game/objects/structures/bedsheet_bin.dm:378
public partial class obj_item_bedsheet_gondola : obj_item_bedsheet
{
    // tg: code/game/objects/structures/bedsheet_bin.dm:391
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        gondola_mouth = Dm.text("sheetgondola_mouth[]", rand(1, 4));
        gondola_eyes = Dm.text("sheetgondola_eyes[]", rand(1, 4));
        add_overlay(gondola_mouth);
        add_overlay(gondola_eyes);
        return dot;
    }

    // tg: code/game/objects/structures/bedsheet_bin.dm:398
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        DmList dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape) as DmList;
        if (!Dm.truthy(isinhands))
        {
            Dm.append(ref dot, mutable_appearance(icon_file, Dm.str(gondola_mouth)));
            Dm.append(ref dot, mutable_appearance(icon_file, Dm.str(gondola_eyes)));
        }
        return dot;
    }
}

// tg: code/game/objects/structures/bedsheet_bin.dm:575
public partial class obj_structure_bedsheetbin : obj_structure
{
    // tg: code/game/objects/structures/bedsheet_bin.dm:600
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // register_context(): the context tips (D264)
        return dot;
    }

    // tg: code/game/objects/structures/bedsheet_bin.dm:604
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (amount < 1)
            Dm.append(ref dot, I18n.T("There are no bed sheets in the bin."));
        else if (amount == 1)
            Dm.append(ref dot, I18n.T("There is one bed sheet in the bin."));
        else
            Dm.append(ref dot, I18n.T("There are [] bed sheets in the bin.", amount));
        return dot;
    }

    // tg: code/game/objects/structures/bedsheet_bin.dm:613  add_context: the context tips (D264)

    // tg: code/game/objects/structures/bedsheet_bin.dm:636
    public override DmTask<object> update_icon_state(params object[] __extra)
    {
        float n = Dm.num(amount);
        if (n == 0)
            icon_state = Dm.text("[]-empty", base_icon_state);
        else if (n >= 1 && n <= 5)
            icon_state = Dm.text("[]-half", base_icon_state);
        else
            icon_state = Dm.text("[]-full", base_icon_state);
        return base.update_icon_state(__extra);
    }

    // tg: code/game/objects/structures/bedsheet_bin.dm:646
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        if (Dm.truthy(amount))
        {
            amount = 0;
            update_appearance();
        }
        await base.fire_act(exposed_temperature, exposed_volume);
        return null;
    }

    // tg: code/game/objects/structures/bedsheet_bin.dm:652
    public override async DmTask<object> screwdriver_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(amount))
        {
            to_chat(user, span_warning(I18n.T("The [] must be empty first!", this)));
            return ITEM_INTERACT_SUCCESS;
        }
        if (Dm.truthy(await tool.use_tool(this, user, 0.5f * SECONDS, volume: 50)))
        {
            to_chat(user, span_notice(I18n.T("You disassemble the [].", this)));
            await Dm.new_async<obj_item_stack_rods>(loc, 2);
            qdel(this);
            return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }

    // tg: code/game/objects/structures/bedsheet_bin.dm:662
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act_secondary(user, tool);
        await default_unfasten_wrench(user, tool, time: 0.5f * SECONDS);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/bedsheet_bin.dm:667
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_bedsheet)
            return await bedsheet_act(user, tool);

        // Everything else we try to hide
        return await hide_item_act(user, tool);
    }

    // tg: code/game/objects/structures/bedsheet_bin.dm:674
    public virtual async DmTask<object> bedsheet_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(await user.transferItemToLoc(tool, this, silent: FALSE)))
            return ITEM_INTERACT_BLOCKING;
        sheets.Add(tool);
        amount++;
        to_chat(user, span_notice(I18n.T("You put [] in [].", tool, this)));
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/bedsheet_bin.dm:683
    public virtual async DmTask<object> hide_item_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;
        if (tool.w_class >= WEIGHT_CLASS_BULKY)
        {
            balloon_alert(user, I18n.T("too big!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(amount))
        {
            balloon_alert(user, I18n.T("nothing to hide under!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (hidden != null)
        {
            balloon_alert(user, I18n.T("already something there!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(tool, this, silent: FALSE)))
        {
            to_chat(user, span_warning(I18n.T("\\The [] is stuck to your hand, you cannot hide it among the sheets!", tool)));
            return ITEM_INTERACT_BLOCKING;
        }
        hidden = tool;
        to_chat(user, span_notice(I18n.T("You hide [] among the sheets.", tool)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/bedsheet_bin.dm:702
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null) => await attack_hand(user, modifiers);

    // tg: code/game/objects/structures/bedsheet_bin.dm:705
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (user is mob_living L && ((int)Dm.num(L.mobility_flags) & MOBILITY_PICKUP) == 0)
            return dot;
        if (amount >= 1)
        {
            amount--;

            obj_item_bedsheet B;
            if (sheets.len > 0)
            {
                B = sheets[sheets.len] as obj_item_bedsheet;
                sheets.Remove(B);
            }
            else
                B = Dm.@new<obj_item_bedsheet>(loc);

            await B.forceMove(drop_location() as atom);
            await user.put_in_hands(B);
            to_chat(user, span_notice(I18n.T("You take [] out of [].", B, this)));
            update_appearance();

            if (hidden != null)
            {
                await hidden.forceMove(drop_location() as atom);
                to_chat(user, span_notice(I18n.T("[] falls out of []!", hidden, B)));
                hidden = null;
            }
        }

        add_fingerprint(user);
        return dot;
    }

    // tg: code/game/objects/structures/bedsheet_bin.dm:737
    public override async DmTask<object> attack_tk(mob user = null)
    {
        if (amount >= 1)
        {
            amount--;

            obj_item_bedsheet B;
            if (sheets.len > 0)
            {
                B = sheets[sheets.len] as obj_item_bedsheet;
                sheets.Remove(B);
            }
            else
                B = Dm.@new<obj_item_bedsheet>(loc);

            await B.forceMove(drop_location() as atom);
            to_chat(user, span_notice(I18n.T("You telekinetically remove [] from [].", B, this)));
            update_appearance();

            if (hidden != null)
            {
                await hidden.forceMove(drop_location() as atom);
                hidden = null;
            }
        }

        add_fingerprint(user);
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }
}
