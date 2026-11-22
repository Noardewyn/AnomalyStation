// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/pet_commands/fetch.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

/**
 * # Pet Command: Fetch
 * Watch for someone throwing or pointing at something and then go get it and bring it back.
 * If it's food we might eat it instead.
 */
// tg: code/datums/components/pet_commands/fetch.dm:7
public partial class datum_pet_command_fetch : datum_pet_command
{
    // tg: code/datums/components/pet_commands/fetch.dm:19
    [DmParams("parent")]
    public override async DmTask New(params object[] __extra)
    {
        mob_living parent = Dm.arg<mob_living>(__extra, 0);
        await base.New(__extra);
        if (parent == null)
            return;
        Waiting.NoSleep(parent.AddElement(typeof(datum_element_ai_held_item))); // We don't remove this on destroy because they might still be holding something
        return;
    }

    // tg: code/datums/components/pet_commands/fetch.dm:25
    public override async DmTask add_new_friend(mob_living tamer = null)
    {
        await base.add_new_friend(tamer);
        RegisterSignal(tamer, COMSIG_MOB_THROW, nameof(listened_throw));
    }

    // tg: code/datums/components/pet_commands/fetch.dm:29
    public override async DmTask remove_friend(mob_living unfriended = null)
    {
        await base.remove_friend(unfriended);
        UnregisterSignal(unfriended, COMSIG_MOB_THROW);
    }

    // tg: code/datums/components/pet_commands/fetch.dm:33
    public override object retrieve_command_text(atom living_pet = null, atom target = null)
    {
        return target == null ? null : Dm.text("signals [] to fetch []!", living_pet, target);
    }

    /// A friend has thrown something, if we're listening or at least not busy then go get it
    // tg: code/datums/components/pet_commands/fetch.dm:37
    [SignalHandler]
    public virtual void listened_throw(mob_living_carbon thrower = null)
    {
        mob_living parent = weak_parent.resolve() as mob_living;
        if (parent == null)
            return;

        DmList blackboard = (parent.ai_controller as datum_ai_controller).blackboard;
        if (!Dm.truthy(trigger_on_throw) && !Dm.truthy(blackboard[BB_ACTIVE_PET_COMMAND]))
            return; // We don't have a command and we only listen to commands
        if (Dm.truthy(blackboard[BB_ACTIVE_PET_COMMAND]) && blackboard[BB_ACTIVE_PET_COMMAND] != this)
            return; // We have a command and it's not this one
        if (Dm.truthy(blackboard[BB_CURRENT_PET_TARGET]) || Dm.truthy(blackboard[BB_FETCH_DELIVER_TO]))
            return; // We're already very fetching
        if (!Dm.truthy(can_see(parent, thrower, length: sense_radius)))
            return; // Can't see it

        obj_item thrown_thing = thrower.get_active_held_item() as obj_item;
        if (!isitem(thrown_thing))
            return;
        if (Dm.truthy((blackboard[BB_FETCH_IGNORE_LIST] as DmList)?[thrown_thing]))
            return; // We're ignoring it already

        RegisterSignal(thrown_thing, COMSIG_MOVABLE_THROW_LANDED, nameof(listen_throw_land));
    }

    /// A throw we were listening to has finished, see if it's in range for us to try grabbing it
    // tg: code/datums/components/pet_commands/fetch.dm:63
    [SignalHandler]
    public virtual void listen_throw_land(obj_item thrown_thing = null, datum_thrownthing throwingdatum = null)
    {
        UnregisterSignal(thrown_thing, COMSIG_MOVABLE_THROW_LANDED);
        mob_living parent = weak_parent.resolve() as mob_living;
        if (parent == null)
            return;
        if (!isturf(thrown_thing.loc))
            return;
        if (!Dm.truthy(can_see(parent, thrown_thing, length: sense_radius)))
            return;

        mob thrower = throwingdatum?.get_thrower() as mob;
        if (thrower == null)
            return;
        Waiting.Detach(on_thrown_landed(parent, thrower as mob_living, thrown_thing));
    }

    // the last lines of listen_throw_land
    private async DmTask on_thrown_landed(mob_living parent, mob_living thrower, obj_item thrown_thing)
    {
        await try_activate_command(thrower);
        await set_command_target(parent, thrown_thing);
        (parent.ai_controller as datum_ai_controller).set_blackboard_key(BB_FETCH_DELIVER_TO, thrower);
    }

    // Don't try and fetch turfs or anchored objects if someone points at them
    // tg: code/datums/components/pet_commands/fetch.dm:83
    public override async DmTask<object> look_for_target(mob_living pointing_friend = null, atom pointed_atom = null)
    {
        if (pointed_atom is not obj_item)
            return FALSE;
        if (Dm.truthy(((atom_movable)pointed_atom).anchored))
            return FALSE;
        object dot = await base.look_for_target(pointing_friend, pointed_atom);
        if (!Dm.truthy(dot))
            return FALSE;

        mob_living parent = weak_parent.resolve() as mob_living;
        (parent.ai_controller as datum_ai_controller).set_blackboard_key(BB_FETCH_DELIVER_TO, pointing_friend);
        return dot;
    }

    // Install the fetch BT subtree. The subtree itself handles all phases (seek > pick up > deliver).
    // tg: code/datums/components/pet_commands/fetch.dm:96
    public override async DmTask<object> execute_action(datum_ai_controller controller = null)
    {
        await controller.set_behavior_tree_override(SUBPLAN_ID_PET_COMMAND, typeof(datum_bt_node_subtree_pet_command_fetch));
        return null;
    }
}
