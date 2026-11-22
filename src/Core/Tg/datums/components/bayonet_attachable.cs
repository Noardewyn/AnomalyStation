// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/bayonet_attachable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J6 (D298): the file whole: a survival knife on the kinetic accelerator (and any other gun with a bayonet point).

// tg: code/datums/components/bayonet_attachable.dm:7
public partial class datum_component_bayonet_attachable : datum_component
{
    // tg: code/datums/components/bayonet_attachable.dm:28
    [DmParams("starting_bayonet", "offset_x", "offset_y", "removable", "bayonet_icon_state", "bayonet_overlay", "bayonet_overlay_icon", "allow_sawnoff")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        obj_item starting_bayonet = Dm.arg<obj_item>(__extra, 0);
        offset_x = Dm.num(Dm.arg(__extra, 1) ?? 0f);
        offset_y = Dm.num(Dm.arg(__extra, 2) ?? 0f);
        removable = Dm.num(Dm.arg(__extra, 3) ?? TRUE);
        bayonet_icon_state = Dm.arg(__extra, 4);
        bayonet_overlay = Dm.arg(__extra, 5) ?? "bayonet";
        bayonet_overlay_icon = Dm.arg(__extra, 6) ?? Dm.resource("icons/obj/weapons/guns/bayonets.dmi");
        allow_sawnoff = Dm.num(Dm.arg(__extra, 7) ?? FALSE);

        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;

        if (istype(starting_bayonet, typeof(obj_item)))
            await add_bayonet(starting_bayonet);
        return null;
    }

    // tg: code/datums/components/bayonet_attachable.dm:53
    public override object Destroy(float? force = null)
    {
        if (bayonet != null)
            Waiting.NoSleep(remove_bayonet());
        return base.Destroy(force);
    }

    // tg: code/datums/components/bayonet_attachable.dm:58
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_OBJ_DECONSTRUCT, nameof(on_parent_deconstructed));
        RegisterSignal(parent, COMSIG_ATOM_EXITED, nameof(on_item_exit));
        RegisterSignal(parent, COMSIG_ATOM_TOOL_ACT(TOOL_SCREWDRIVER), nameof(on_screwdriver));
        RegisterSignal(parent, COMSIG_ATOM_UPDATE_ICON_STATE, nameof(on_update_icon_state));
        RegisterSignal(parent, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(on_update_overlays));
        RegisterSignal(parent, COMSIG_ATOM_ATTACKBY, nameof(on_attackby));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(parent, COMSIG_QDELETING, nameof(on_parent_deleted));
        RegisterSignal(parent, COMSIG_ITEM_PRE_ATTACK, nameof(on_pre_attack));
        RegisterSignal(parent, COMSIG_GUN_BEING_SAWNOFF, nameof(on_being_sawnoff));
        RegisterSignal(parent, COMSIG_GUN_SAWN_OFF, nameof(on_sawn_off));
    }

    // tg: code/datums/components/bayonet_attachable.dm:71
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(
            COMSIG_OBJ_DECONSTRUCT,
            COMSIG_ATOM_EXITED,
            COMSIG_ATOM_TOOL_ACT(TOOL_SCREWDRIVER),
            COMSIG_ATOM_UPDATE_ICON_STATE,
            COMSIG_ATOM_UPDATE_OVERLAYS,
            COMSIG_ATOM_ATTACKBY,
            COMSIG_ATOM_EXAMINE,
            COMSIG_QDELETING,
            COMSIG_ITEM_PRE_ATTACK,
            COMSIG_GUN_BEING_SAWNOFF,
            COMSIG_GUN_SAWN_OFF));
    }

    // tg: code/datums/components/bayonet_attachable.dm:86
    public virtual void on_examine(obj_item source = null, mob examiner = null, DmList examine_list = null)
    {
        if (bayonet == null)
        {
            Dm.append(ref examine_list, I18n.T("It has a <b>bayonet</b> lug on it."));
            return;
        }

        Dm.append(ref examine_list, I18n.T("It has \\a [] []affixed to it.", bayonet, Dm.truthy(removable) ? "" : "permanently "));
        if (Dm.truthy(removable))
            Dm.append(ref examine_list, span_info(I18n.T("[] looks like it can be <b>unscrewed</b> from [].", bayonet, bayonet)));
    }

    // tg: code/datums/components/bayonet_attachable.dm:97
    public virtual object on_pre_attack(obj_item source = null, atom target = null, mob_living user = null, DmList modifiers = null, params object[] __extra)
    {
        if (bayonet == null || !Dm.truthy(user.combat_mode))
            return NONE;

        // RMB is used for butchering, and bayonets are usually are knives
        Waiting.Detach(bayonet.melee_attack_chain(user, target, Dm.sub(modifiers, RIGHT_CLICK) as DmList));
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/datums/components/bayonet_attachable.dm:107
    public virtual object on_attackby(obj_item source = null, obj_item attacking_item = null, mob attacker = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(is_type_in_typecache(attacking_item, TgState.Current.datum_component_bayonet_attachable__valid_bayonets)))
            return null;

        if (bayonet != null)
        {
            source.balloon_alert(attacker, I18n.T("already has \\a []!", bayonet));
            return null;
        }

        if (!Dm.truthy(Waiting.NoSleep(attacker.transferItemToLoc(attacking_item, source))))
            return null;

        Waiting.NoSleep(add_bayonet(attacking_item, attacker));
        source.balloon_alert(attacker, I18n.T("attached"));
        return COMPONENT_NO_AFTERATTACK;
    }

    // tg: code/datums/components/bayonet_attachable.dm:124
    public virtual async DmTask add_bayonet(obj_item new_bayonet = null, mob attacher = null)
    {
        if (bayonet != null)
            CRASH(Dm.text("[] tried to add a new bayonet when it already had one.", type));

        bayonet = new_bayonet;
        if (bayonet.loc != parent)
            await bayonet.forceMove(parent as atom);
        (parent as obj_item).update_appearance();
    }

    // tg: code/datums/components/bayonet_attachable.dm:134
    public virtual async DmTask remove_bayonet()
    {
        bayonet = null;
        (parent as obj_item).update_appearance();
    }

    // tg: code/datums/components/bayonet_attachable.dm:139
    public virtual void on_item_exit(obj_item source = null, atom_movable gone = null, object direction = null)
    {
        if (gone == bayonet)
            Waiting.NoSleep(remove_bayonet());
    }

    // tg: code/datums/components/bayonet_attachable.dm:145
    public virtual void on_parent_deconstructed(obj_item source = null, object disassembled = null)
    {
        if (QDELETED(bayonet) || !Dm.truthy(removable))
        {
            Waiting.NoSleep(remove_bayonet());
            return;
        }

        Waiting.NoSleep(bayonet.forceMove(source.drop_location() as atom));
    }

    // tg: code/datums/components/bayonet_attachable.dm:154
    public virtual object on_screwdriver(obj_item source = null, mob user = null, obj_item tool = null)
    {
        if (bayonet == null || !Dm.truthy(removable))
            return null;

        Waiting.Detach(unscrew_bayonet(source, user, tool));
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/datums/components/bayonet_attachable.dm:163
    public virtual async DmTask unscrew_bayonet(obj_item source = null, mob user = null, obj_item tool = null)
    {
        if (tool != null)
            tool.play_tool_sound(source);
        source.balloon_alert(user, I18n.T("unscrewed []", bayonet));

        obj_item to_remove = bayonet;
        await to_remove.forceMove(source.drop_location() as atom);
        if (Dm.truthy(Waiting.NoSleep(source.Adjacent(user))) && !issilicon(user))
            await user.put_in_hands(to_remove);
    }

    // tg: code/datums/components/bayonet_attachable.dm:172
    public virtual void on_update_overlays(obj_item source = null, DmList overlays = null)
    {
        if (!Dm.truthy(bayonet_overlay) || !Dm.truthy(bayonet_overlay_icon))
            return;

        if (bayonet == null)
            return;

        mutable_appearance bayonet_appearance = mutable_appearance(bayonet_overlay_icon, bayonet_overlay as string) as mutable_appearance;
        bayonet_appearance.pixel_w = offset_x;
        bayonet_appearance.pixel_z = offset_y;
        Dm.append(ref overlays, bayonet_appearance);
    }

    // tg: code/datums/components/bayonet_attachable.dm:186
    public virtual void on_update_icon_state(obj_item source = null)
    {
        if (!Dm.truthy(bayonet_icon_state))
            return;

        string base_state = Dm.truthy(source.base_icon_state) ? (string)source.base_icon_state : Dm.str(Dm.initial(source.type, (obj_item i) => i.icon_state));
        if (bayonet != null)
            source.icon_state = Dm.text("[]-[]", base_state, bayonet_icon_state);
        else if (!Dm.eq(source.icon_state, base_state))
            source.icon_state = base_state;
    }

    // tg: code/datums/components/bayonet_attachable.dm:198
    public virtual void on_parent_deleted(obj_item source = null, params object[] __extra)
    {
        qdel(bayonet);
        bayonet = null;
    }

    // tg: code/datums/components/bayonet_attachable.dm:202
    public virtual object on_being_sawnoff(obj_item source = null, mob user = null)
    {
        if (bayonet == null || Dm.truthy(allow_sawnoff))
            return null;
        source.balloon_alert(user, I18n.T("bayonet must be removed!"));
        return COMPONENT_CANCEL_SAWING_OFF;
    }

    // tg: code/datums/components/bayonet_attachable.dm:210
    public virtual void on_sawn_off(obj_item source = null, mob user = null)
    {
        if (!Dm.truthy(allow_sawnoff))
            qdel(this);
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/bayonet_attachable.dm:26  var/static/list/valid_bayonets
    internal DmList datum_component_bayonet_attachable__valid_bayonets = typecacheof(Dm.list(typeof(obj_item_knife_combat))) as DmList;
}
