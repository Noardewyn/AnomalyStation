// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/stationloving.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1a, D316: the nuclear disk). The var block is generated (D111).

/// Teleports the movable atom back to a safe turf on the station if it leaves the z-level or becomes inaccessible.
// tg: code/datums/components/stationloving.dm:2
public partial class datum_component_stationloving : datum_component
{
    // tg: code/datums/components/stationloving.dm:11
    [DmParams("inform_admins", "allow_item_destruction")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!ismovable(parent))
            return COMPONENT_INCOMPATIBLE;
        inform_admins = Dm.num(Dm.arg(__extra, 0));
        allow_item_destruction = Dm.num(Dm.arg(__extra, 1));

        // Just in case something is being created outside of station/centcom
        if (!atom_in_bounds(parent as atom))
            await relocate();
        return await base.Initialize(__extra);
    }

    // tg: code/datums/components/stationloving.dm:21
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_PREQDELETED, nameof(on_parent_pre_qdeleted));
        RegisterSignal(parent, COMSIG_ITEM_IMBUE_SOUL, nameof(check_soul_imbue));
        RegisterSignal(parent, COMSIG_ITEM_MARK_RETRIEVAL, nameof(check_mark_retrieval));
        // Relocate when we become unreachable
        RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(on_parent_moved));
        // Relocate when our loc, or any of our loc's locs, becomes unreachable
        TgState.Current.datum_component_stationloving__RegisterWithParent__loc_connections ??= Dm.list(
            Dm.kv(COMSIG_MOVABLE_MOVED, nameof(on_parent_moved)),
            Dm.kv(SIGNAL_ADDTRAIT(TRAIT_SECLUDED_LOCATION), nameof(on_loc_secluded)));
        connect_ref = WEAKREF(await AddComponent(typeof(datum_component_connect_containers), parent, TgState.Current.datum_component_stationloving__RegisterWithParent__loc_connections) as datum) as datum_weakref;
        await base.RegisterWithParent();
    }

    // tg: code/datums/components/stationloving.dm:34
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(
            COMSIG_MOVABLE_Z_CHANGED,
            COMSIG_PREQDELETED,
            COMSIG_ITEM_IMBUE_SOUL,
            COMSIG_ITEM_MARK_RETRIEVAL,
            COMSIG_MOVABLE_MOVED));

        qdel(connect_ref);
        await base.UnregisterFromParent(__extra);
    }

    // tg: code/datums/components/stationloving.dm:45
    public override DmTask InheritComponent(datum_component newc = null, object original = null, params object[] __extra)
    {
        // tg assigns its own arguments here (inform_admins = newc.inform_admins, allow_death = ...), which changes nothing on the component
        return DmTask.CompletedTask;
    }

    /// Teleports parent to a safe turf on the station z-level.
    // tg: code/datums/components/stationloving.dm:54
    public virtual async DmTask<turf> relocate()
    {
        turf target_turf = null;
        if (Dm.truthy(length(GLOB.the_station_areas)))
            target_turf = get_safe_random_station_turf(GLOB.the_station_areas);
        else if (Dm.truthy(length(SSmapping.levels_by_trait(ZTRAIT_STATION))))
            target_turf = find_safe_turf() as turf; // Fallback for debug maps with station levels but no station areas.

        if (target_turf == null && Dm.truthy(length(GLOB.blobstart)))
            target_turf = get_turf(pick(GLOB.blobstart) as atom) as turf;

        if (target_turf == null)
        {
            var current_turf = get_turf(parent as atom) as turf;
            log_game(Dm.text("Unable to relocate [] from []: no safe station turf or blobstart landmark is available.", parent, loc_name(current_turf)));
            if (Dm.truthy(inform_admins))
                message_admins(Dm.text("Unable to relocate [] from []: no safe station turf or blobstart landmark is available.", parent, ADMIN_VERBOSEJMP(current_turf)));
            return null;
        }

        var movable_parent = (atom_movable)parent;
        playsound(movable_parent, "sound/machines/synth/synth_no.ogg", 5, TRUE);

        var holder = get(movable_parent, typeof(mob)) as mob;
        if (holder != null)
        {
            to_chat(holder, span_danger(I18n.T("You can't help but feel that you just lost something back there...")));
            await holder.temporarilyRemoveItemFromInventory(parent as obj_item, TRUE); // prevents ghost diskie
        }

        await movable_parent.forceMove(target_turf);

        return target_turf;
    }

    /// Signal proc for [COMSIG_MOVABLE_MOVED], called when our parent moves, or our parent's loc, or our parent's loc loc...
    /// To check if our disk is moving somewhere it shouldn't be, such as off Z level, or into an invalid area
    // tg: code/datums/components/stationloving.dm:86
    [SignalHandler]
    public virtual void on_parent_moved(atom_movable source = null, turf old_turf = null)
    {
        if (atom_in_bounds(source))
            return;

        var current_turf = get_turf(source) as turf;
        turf new_destination = Waiting.NoSleep(relocate());
        if (new_destination == null)
            return;
        // Our turf actually didn't change, so it's more likely we became secluded
        if (current_turf == old_turf)
        {
            log_game(Dm.text("[] moved out of bounds at [], becoming inaccessible / secluded. Moving it to [].", parent, loc_name(current_turf), loc_name(new_destination)));

            if (Dm.truthy(inform_admins))
                message_admins(Dm.text("[] moved out of bounds at [], becoming inaccessible / secluded. Moving it to [].", parent, ADMIN_VERBOSEJMP(current_turf), ADMIN_VERBOSEJMP(new_destination)));
        }
        // Our locs changes, we did in fact move somewhere
        else
        {
            log_game(Dm.text("[] attempted to be moved out of bounds from [] to []. Moving it to [].", parent, loc_name(old_turf), loc_name(current_turf), loc_name(new_destination)));

            if (Dm.truthy(inform_admins))
                message_admins(Dm.text("[] attempted to be moved out of bounds from [] to []. Moving it to [].", parent, ADMIN_VERBOSEJMP(old_turf), ADMIN_VERBOSEJMP(current_turf), ADMIN_VERBOSEJMP(new_destination)));
        }
    }

    /// Signal proc for [SIGNAL_ADDTRAIT], via [TRAIT_SECLUDED_LOCATION] on our locs, to ensure nothing funky happens
    // tg: code/datums/components/stationloving.dm:115
    [SignalHandler]
    public virtual void on_loc_secluded(atom_movable source = null)
    {
        turf new_destination = Waiting.NoSleep(relocate());
        if (new_destination == null)
            return;
        log_game(Dm.text("[] moved out of bounds at [], becoming inaccessible / secluded. Moving it to [].", parent, loc_name(source), loc_name(new_destination)));

        if (Dm.truthy(inform_admins))
            message_admins(Dm.text("[] moved out of bounds at [], becoming inaccessible / secluded. Moving it to [].", parent, ADMIN_VERBOSEJMP(source), ADMIN_VERBOSEJMP(new_destination)));
    }

    // tg: code/datums/components/stationloving.dm:128
    [SignalHandler]
    public virtual object check_soul_imbue(datum source = null)
    {
        if (Dm.truthy(disallow_soul_imbue))
            return COMPONENT_BLOCK_IMBUE;
        return null;
    }

    // tg: code/datums/components/stationloving.dm:134
    [SignalHandler]
    public virtual object check_mark_retrieval(datum source = null)
    {
        return COMPONENT_BLOCK_MARK_RETRIEVAL;
    }

    /// Checks whether a given atom's turf is within bounds. Returns TRUE if it is, FALSE if it isn't.
    // tg: code/datums/components/stationloving.dm:140
    public virtual bool atom_in_bounds(atom atom_to_check = null)
    {
        // Typecache of shuttles that we allow the disk to stay on
        TgState.Current.datum_component_stationloving__atom_in_bounds__allowed_shuttles ??= typecacheof(Dm.list(
            typeof(area_shuttle_syndicate),
            typeof(area_shuttle_escape),
            typeof(area_shuttle_pod_1),
            typeof(area_shuttle_pod_2),
            typeof(area_shuttle_pod_3),
            typeof(area_shuttle_pod_4))) as DmList;
        // Typecache of areas on the centcom Z-level that we allow the disk to stay on
        TgState.Current.datum_component_stationloving__atom_in_bounds__disallowed_centcom_areas ??= typecacheof(Dm.list(
            typeof(area_centcom_abductor_ship),
            typeof(area_awaymission_errorroom))) as DmList;

        // Our loc is a secluded location = not in bounds
        if (atom_to_check.loc != null && HAS_TRAIT(atom_to_check.loc, TRAIT_SECLUDED_LOCATION))
            return false;
        // No turf below us = nullspace = not in bounds
        var destination_turf = get_turf(atom_to_check) as turf;
        if (destination_turf == null)
            return false;
        if (Dm.truthy(is_station_level(destination_turf.z)))
            return true;
        if (Dm.truthy(atom_to_check.onSyndieBase()))
            return true;

        var destination_area = destination_turf.loc as area;
        if (Dm.truthy(is_centcom_level(destination_turf.z)))
        {
            if (Dm.truthy(is_type_in_typecache(destination_area, TgState.Current.datum_component_stationloving__atom_in_bounds__disallowed_centcom_areas)))
                return false;
            return true;
        }
        if (Dm.truthy(is_reserved_level(destination_turf.z)))
        {
            if (Dm.truthy(is_type_in_typecache(destination_area, TgState.Current.datum_component_stationloving__atom_in_bounds__allowed_shuttles)))
                return true;
        }

        return false;
    }

    /// Signal handler for before the parent is qdel'd. Can prevent the parent from being deleted where allow_item_destruction is FALSE and force is FALSE.
    // tg: code/datums/components/stationloving.dm:180
    [SignalHandler]
    public virtual object on_parent_pre_qdeleted(datum source = null, object force = null)
    {
        var current_turf = get_turf(parent as atom) as turf;

        if (Dm.truthy(force) && Dm.truthy(inform_admins))
        {
            message_admins(Dm.text("[] has been !!force deleted!! in [].", parent, ADMIN_VERBOSEJMP(current_turf)));
            log_game(Dm.text("[] has been !!force deleted!! in [].", parent, loc_name(current_turf)));
        }

        if (Dm.truthy(force) || Dm.truthy(allow_item_destruction))
            return FALSE;

        turf new_turf = Waiting.NoSleep(relocate());
        if (new_turf == null)
            return TRUE;
        log_game(Dm.text("[] has been destroyed in []. Preventing destruction and moving it to [].", parent, loc_name(current_turf), loc_name(new_turf)));
        if (Dm.truthy(inform_admins))
            message_admins(Dm.text("[] has been destroyed in []. Preventing destruction and moving it to [].", parent, ADMIN_VERBOSEJMP(current_turf), ADMIN_VERBOSEJMP(new_turf)));
        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/stationloving.dm:28  var/static/list/loc_connections
    internal DmList datum_component_stationloving__RegisterWithParent__loc_connections;
    // tg: code/datums/components/stationloving.dm:142  var/static/list/allowed_shuttles
    internal DmList datum_component_stationloving__atom_in_bounds__allowed_shuttles;
    // tg: code/datums/components/stationloving.dm:151  var/static/list/disallowed_centcom_areas
    internal DmList datum_component_stationloving__atom_in_bounds__disallowed_centcom_areas;
}
