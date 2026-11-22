// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/anomalock_module.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO2 (D333): the file whole, with the anomaly-locked MOD modules (_module.dm, modules_science.dm, module_kinesis.dm,
// module_holding.dm). A module that needs an anomaly core refuses to work without one: a core of an accepted kind is slotted in by
// hand (or comes with a prebuilt module), shown on the module's icon ("-core") and its examine text, and can be screwed out unless the
// module keeps it. The anomaly cores themselves (research/anomaly/anomaly_core.dm) stay inert: a core is an item here, its signal
// effects are the anomalies' (not-translated.md 222). The var block is generated (D111).

// tg: code/datums/components/anomalock_module.dm:1
public partial class datum_component_anomaly_locked_module : datum_component
{
    // tg: code/datums/components/anomalock_module.dm:14
    [DmParams("anomaly_types", "prebuilt", "removable", "pre_insert_callback", "insert_callback", "remove_callback")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        DmList anomaly_types = Dm.arg(__extra, 0) as DmList;
        object prebuilt = Dm.arg(__extra, 1) ?? FALSE;
        object removable = Dm.arg(__extra, 2) ?? TRUE;
        object pre_insert_callback = Dm.arg(__extra, 3);
        object insert_callback = Dm.arg(__extra, 4);
        object remove_callback = Dm.arg(__extra, 5);
        object dot = await base.Initialize(Dm.forward(__extra, anomaly_types, prebuilt, removable, pre_insert_callback, insert_callback, remove_callback));
        if (!istype(parent, typeof(obj_item_mod_module)))
            return COMPONENT_INCOMPATIBLE;
        accepted_anomalies = typecacheof(anomaly_types) as DmList;
        core_removable = removable;
        this.pre_insert_callback = pre_insert_callback;
        core_insert_callback = insert_callback;
        core_remove_callback = remove_callback;
        if (!Dm.truthy(prebuilt) || Dm.length(anomaly_types) == 0)
            return dot;
        Type core_type = pick(anomaly_types) as Type;
        await insert_core(parent as obj_item_mod_module, null, Dm.@new(core_type, parent) as obj_item, null);
        return dot;
    }

    // tg: code/datums/components/anomalock_module.dm:28
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref core);
        return base.Destroy(force);
    }

    // tg: code/datums/components/anomalock_module.dm:32
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_MODULE_TRIGGERED, nameof(on_module_triggered));
        RegisterSignal(parent, COMSIG_ATOM_ITEM_INTERACTION, nameof(on_item_interact));
        RegisterSignal(parent, COMSIG_ATOM_TOOL_ACT(TOOL_SCREWDRIVER), nameof(on_screwdriver_act));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(parent, COMSIG_ATOM_UPDATE_ICON_STATE, nameof(on_update_icon_state));
    }

    // tg: code/datums/components/anomalock_module.dm:39
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(
            COMSIG_MODULE_TRIGGERED,
            COMSIG_ATOM_ITEM_INTERACTION,
            COMSIG_ATOM_TOOL_ACT(TOOL_SCREWDRIVER),
            COMSIG_ATOM_EXAMINE,
            COMSIG_ATOM_UPDATE_ICON_STATE));
    }

    // tg: code/datums/components/anomalock_module.dm:48
    [SignalHandler]
    public virtual object on_module_triggered(obj_item_mod_module source = null, mob_living wearer = null)
    {
        if (core == null)
        {
            source.balloon_alert(wearer, I18n.T("no core!"));
            return MOD_ABORT_USE;
        }
        return null;
    }

    // tg: code/datums/components/anomalock_module.dm:54
    [SignalHandler]
    public virtual object on_item_interact(obj_item_mod_module source = null, mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (!Dm.truthy(is_type_in_typecache(tool, accepted_anomalies)))
            return 0;
        if (core != null)
        {
            source.balloon_alert(user, I18n.T("already has core!"));
            return ITEM_INTERACT_FAILURE;
        }
        if (Dm.truthy(pre_insert_callback))
        {
            object callback_return;
            if (istype(pre_insert_callback, typeof(datum_callback)))
            {
                datum_callback pre_insert_callback_datum = pre_insert_callback as datum_callback;
                callback_return = Waiting.NoSleep(pre_insert_callback_datum.Invoke(user, tool, modifiers));
            }
            else
                callback_return = Waiting.NoSleep(Dm.Call(source, pre_insert_callback as string, user, tool, modifiers));
            if (Dm.truthy(callback_return))
                return callback_return;
        }
        return Waiting.NoSleep(insert_core(source, user, tool, modifiers));
    }

    // tg: code/datums/components/anomalock_module.dm:72
    public virtual async DmTask<object> insert_core(obj_item_mod_module source = null, mob_living user = null, obj_item new_core = null, DmList modifiers = null)
    {
        if (user != null && !Dm.truthy(await user.transferItemToLoc(new_core, source)))
            return ITEM_INTERACT_FAILURE;
        core = new_core as obj_item_assembly_signaler_anomaly;
        if (user != null)
        {
            source.balloon_alert(user, I18n.T("core inserted"));
            playsound(source, Dm.resource("sound/machines/click.ogg"), 30, TRUE);
        }
        source.update_appearance(UPDATE_ICON_STATE);
        if (Dm.truthy(core_insert_callback))
        {
            if (istype(core_insert_callback, typeof(datum_callback)))
            {
                datum_callback core_insert_callback_datum = core_insert_callback as datum_callback;
                await core_insert_callback_datum.Invoke(core, user, modifiers);
            }
            else
                await Dm.Call(source, core_insert_callback as string, core, user, modifiers);
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/datums/components/anomalock_module.dm:88
    [SignalHandler]
    public virtual object on_screwdriver_act(obj_item_mod_module source = null, mob_living user = null, obj_item tool = null)
    {
        if (core == null)
        {
            source.balloon_alert(user, I18n.T("no core!"));
            return ITEM_INTERACT_FAILURE;
        }
        // tg quirk kept: an unremovable core only gets the alert, and the removal below still runs (tg has no return here)
        if (!Dm.truthy(core_removable))
            source.balloon_alert(user, I18n.T("cannot remove core!"));
        Waiting.Detach(try_remove_core(source, user, tool));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/datums/components/anomalock_module.dm:98
    public virtual async DmTask try_remove_core(obj_item_mod_module source = null, mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(await do_after(user, 3 * SECONDS, source)))
        {
            source.balloon_alert(user, I18n.T("interrupted!"));
            return;
        }
        source.balloon_alert(user, I18n.T("core removed"));
        await core.forceMove(source.drop_location() as atom);
        if (Dm.truthy(await source.Adjacent(user)) && !issilicon(user))
            await user.put_in_hands(core);
        core = null;
        source.update_appearance(UPDATE_ICON_STATE);
        if (Dm.truthy(core_remove_callback))
        {
            if (istype(core_remove_callback, typeof(datum_callback)))
            {
                datum_callback core_remove_callback_datum = core_remove_callback as datum_callback;
                await core_remove_callback_datum.Invoke(core, user);
            }
            else
                await Dm.Call(source, core_remove_callback as string, core, user);
        }
    }

    // tg: code/datums/components/anomalock_module.dm:115
    [SignalHandler]
    public virtual void on_examine(obj_item_mod_module source = null, mob viewer = null, DmList examine_list = null)
    {
        if (Dm.length(accepted_anomalies) == 0)
            return;
        if (core != null)
        {
            Dm.append(ref examine_list, span_notice(I18n.T("There is a [] installed in it. []", core.name, Dm.truthy(core_removable) ? I18n.T("You could remove it with a <b>screwdriver</b>...") : I18n.T("Unfortunately, due to a design quirk, it's unremovable."))));
            return;
        }
        DmList core_list = Dm.list();
        foreach (Type core_path in Dm.each<Type>(accepted_anomalies))
            Dm.append(ref core_list, Dm.initial(core_path, (atom p) => p.name));
        Dm.append(ref examine_list, span_notice(I18n.T("You need to insert \\a [] for this module to function.", english_list(core_list, and_text: I18n.T(" or ")))));
        if (!Dm.truthy(core_removable))
            Dm.append(ref examine_list, span_notice(I18n.T("Due to some design quirk, once a core is inserted, it won't be removable.")));
    }

    // tg: code/datums/components/anomalock_module.dm:129
    [SignalHandler]
    public virtual void on_update_icon_state(obj_item_mod_module source = null)
    {
        source.icon_state = Dm.text("[][]", Dm.initial(source, (obj_item_mod_module p) => p.icon_state), core != null ? "-core" : "");
    }
}
