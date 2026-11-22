// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/rotation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 5 (windows, grilles, the fire axe): the whole file. Alt-click and alt-right-click rotate a window (or a railing)
// that is not anchored. The var block is generated (D111).

// tg: code/datums/elements/rotation.dm:1
public partial class datum_element_simple_rotation : datum_element
{
    /**
     * Adds the ability to rotate an object by Alt-click or using Right-click verbs.
     *
     * args:
     * * rotation_flags (optional) Bitflags that determine behavior for rotation (defined at the top of this file)
     * * post_rotation (optional) Callback proc that is used after the object is rotated (sound effects, balloon alerts, etc.)
     **/
    // tg: code/datums/elements/rotation.dm:16
    [DmParams("target", "rotation_flags", "post_rotation_proccall")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object rotation_flags = Dm.arg(__extra, 0);
        object post_rotation_proccall = Dm.arg(__extra, 1);
        rotation_flags ??= NONE;
        object dot = await base.Attach(target, Dm.forward(__extra, rotation_flags, post_rotation_proccall));
        if (!ismovable(target))
            return ELEMENT_INCOMPATIBLE;

        atom_movable source = target as atom_movable;
        Dm.combine(ref source.flags_1, HAS_CONTEXTUAL_SCREENTIPS_1);

        this.rotation_flags = rotation_flags;
        this.post_rotation_proccall = post_rotation_proccall;

        RegisterSignal(target, COMSIG_CLICK_ALT, nameof(rotate_left));
        RegisterSignal(target, COMSIG_CLICK_ALT_SECONDARY, nameof(rotate_right));
        RegisterSignal(target, COMSIG_ATOM_EXAMINE, nameof(ExamineMessage));
        RegisterSignal(target, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM, nameof(on_requesting_context_from_item));
        return dot;
    }

    // tg: code/datums/elements/rotation.dm:32
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra); // tg calls ..() here and again in its return
        UnregisterSignal(source, Dm.list(
            COMSIG_CLICK_ALT,
            COMSIG_CLICK_ALT_SECONDARY,
            COMSIG_ATOM_EXAMINE,
            COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM));
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/rotation.dm:42
    [SignalHandler]
    public virtual void ExamineMessage(datum source = null, mob user = null, DmList examine_list = null)
    {
        if (Dm.truthy(Dm.bitand(rotation_flags, ROTATION_REQUIRE_WRENCH)))
            Dm.append(ref examine_list, span_notice(I18n.T("This requires a wrench to be rotated.")));
    }

    // tg: code/datums/elements/rotation.dm:47
    [SignalHandler]
    public virtual object rotate_right(datum source = null, mob user = null)
    {
        Waiting.NoSleep(rotate(user, source as obj, ROTATION_CLOCKWISE));
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/datums/elements/rotation.dm:52
    [SignalHandler]
    public virtual object rotate_left(datum source = null, mob user = null)
    {
        Waiting.NoSleep(rotate(user, source as obj, ROTATION_COUNTERCLOCKWISE));
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/datums/elements/rotation.dm:57
    public virtual async DmTask rotate(mob user = null, obj object_to_rotate = null, object degrees = null)
    {
        if (QDELETED(user))
            throw Dm.CRASH(Dm.text("[] is being rotated [] user", this, (user != null ? "with a qdeleting" : "without a")), "code/datums/elements/rotation.dm", 58);
        if (!istype(user, typeof(mob)))
            throw Dm.CRASH(Dm.text("[] is being rotated without a user of the wrong type: []", this, ((datum)user).type), "code/datums/elements/rotation.dm", 60);
        if (!isnum(degrees))
            throw Dm.CRASH(Dm.text("[] is being rotated without providing the amount of degrees needed", this), "code/datums/elements/rotation.dm", 62);

        if (!Dm.truthy(await can_be_rotated(user, object_to_rotate, degrees)) || !Dm.truthy(await can_user_rotate(user, object_to_rotate, degrees)))
            return;

        await object_to_rotate.setDir(turn(object_to_rotate.dir, Dm.num(degrees)));
        if (Dm.truthy(Dm.bitand(rotation_flags, ROTATION_REQUIRE_WRENCH)))
            playsound(object_to_rotate, Dm.resource("sound/items/tools/ratchet.ogg"), 50, TRUE);

        if (Dm.truthy(post_rotation_proccall))
            await Dm.Call(object_to_rotate, (string)post_rotation_proccall, user, degrees);
    }

    // tg: code/datums/elements/rotation.dm:75
    public virtual async DmTask<object> can_user_rotate(mob user = null, obj object_to_rotate = null, object degrees = null)
    {
        if (isliving(user) && Dm.truthy(await user.can_perform_action(object_to_rotate, NEED_DEXTERITY)))
            return TRUE;
        if (Dm.truthy(Dm.bitand(rotation_flags, ROTATION_GHOSTS_ALLOWED)) && isobserver(user) && Dm.truthy(config.Get(typeof(datum_config_entry_flag_ghost_interaction))))
            return TRUE;
        return FALSE;
    }

    // tg: code/datums/elements/rotation.dm:82
    public virtual async DmTask<object> can_be_rotated(mob user = null, obj object_to_rotate = null, object degrees = null, float? silent = null)
    {
        silent ??= FALSE;
        if (!Dm.truthy(await object_to_rotate.Adjacent(user)))
            silent = TRUE;

        if (Dm.truthy(Dm.bitand(rotation_flags, ROTATION_REQUIRE_WRENCH)))
        {
            if (!isliving(user))
                return FALSE;
            obj_item tool = user.get_active_held_item() as obj_item;
            if (tool == null || !Dm.eq(tool.tool_behaviour, TOOL_WRENCH))
            {
                if (!Dm.truthy(silent))
                    object_to_rotate.balloon_alert(user, I18n.T("need a wrench!"));
                return FALSE;
            }
        }

        if (!Dm.truthy(Dm.bitand(rotation_flags, ROTATION_IGNORE_ANCHORED)) && Dm.truthy(object_to_rotate.anchored))
        {
            if (istype(object_to_rotate, typeof(obj_structure_window)) && !Dm.truthy(silent))
                object_to_rotate.balloon_alert(user, I18n.T("need to unscrew!"));
            else if (!Dm.truthy(silent))
                object_to_rotate.balloon_alert(user, I18n.T("need to unwrench!"));
            return FALSE;
        }

        if (Dm.truthy(Dm.bitand(rotation_flags, ROTATION_NEEDS_ROOM)))
        {
            object target_dir = turn(object_to_rotate.dir, Dm.num(degrees));
            obj_structure_window window_to_rotate = object_to_rotate as obj_structure_window;
            float fulltile = window_to_rotate != null ? window_to_rotate.fulltile : FALSE;
            if (!Dm.truthy(valid_build_direction(object_to_rotate.loc as turf, target_dir, is_fulltile: fulltile)))
            {
                if (!Dm.truthy(silent))
                    object_to_rotate.balloon_alert(user, I18n.T("can't rotate in that direction!"));
                return FALSE;
            }
        }

        if (Dm.truthy(Dm.bitand(rotation_flags, ROTATION_NEEDS_UNBLOCKED)))
        {
            turf rotate_turf = get_turf(object_to_rotate);
            if (Dm.truthy(await rotate_turf.is_blocked_turf(source_atom: object_to_rotate)))
            {
                if (!Dm.truthy(silent))
                    object_to_rotate.balloon_alert(user, I18n.T("rotation is blocked!"));
                return FALSE;
            }
        }

        return TRUE;
    }

    // maybe we don't need the item context proc but instead the hand one? since we don't need to check held_item
    // tg: code/datums/elements/rotation.dm:121
    [SignalHandler]
    public virtual object on_requesting_context_from_item(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        float rotation_screentip = FALSE;

        if (Dm.truthy(Waiting.NoSleep(can_be_rotated(user, source as obj, ROTATION_CLOCKWISE, silent: TRUE))))
        {
            context[SCREENTIP_CONTEXT_ALT_LMB] = "Rotate left";
            rotation_screentip = TRUE;
        }
        if (Dm.truthy(Waiting.NoSleep(can_be_rotated(user, source as obj, ROTATION_COUNTERCLOCKWISE, silent: TRUE))))
        {
            context[SCREENTIP_CONTEXT_ALT_RMB] = "Rotate right";
            rotation_screentip = TRUE;
        }

        if (Dm.truthy(rotation_screentip))
            return CONTEXTUAL_SCREENTIP_SET;

        return NONE;
    }
}
