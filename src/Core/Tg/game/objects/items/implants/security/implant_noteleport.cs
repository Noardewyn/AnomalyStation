// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/implants/security/implant_noteleport.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DV2 (D333): the file whole. The bluespace grounding implant: a teleport or a jaunt of its holder is flung back with five
// seconds of paralysis and a burst of quantum sparks. The var block is generated (D111).

///Blocks the implantee from being teleported
// tg: code/game/objects/items/implants/security/implant_noteleport.dm:2
public partial class obj_item_implant_teleport_blocker : obj_item_implant
{
    // tg: code/game/objects/items/implants/security/implant_noteleport.dm:21
    public override async DmTask<object> implant(mob_living target = null, mob user = null, float? silent = null, float? force = null, params object[] __extra)
    {
        silent ??= FALSE;
        force ??= FALSE;
        object dot = await base.implant(target, user, silent, force, __extra);
        if (!Dm.truthy(dot) || !isliving(target))
            return FALSE;
        RegisterSignal(target, COMSIG_MOVABLE_TELEPORTING, nameof(on_teleport));
        RegisterSignal(target, COMSIG_MOB_PRE_JAUNT, nameof(on_jaunt));
        return TRUE;
    }

    // tg: code/game/objects/items/implants/security/implant_noteleport.dm:29
    public override async DmTask<object> removed(mob_living source = null, float? silent = null, float? special = null)
    {
        mob_living target = source;
        silent ??= FALSE;
        special ??= FALSE;
        object dot = await base.removed(target, silent, special);
        if (!Dm.truthy(dot) || !isliving(target))
            return FALSE;
        UnregisterSignal(target, COMSIG_MOVABLE_TELEPORTING);
        UnregisterSignal(target, COMSIG_MOB_PRE_JAUNT);
        return TRUE;
    }

    /// Signal for COMSIG_MOVABLE_TELEPORTING that blocks teleports and stuns the would-be-teleportee.
    // tg: code/game/objects/items/implants/security/implant_noteleport.dm:38
    [SignalHandler]
    public virtual object on_teleport(mob_living teleportee = null, atom destination = null, object channel = null)
    {
        to_chat(teleportee, span_holoparasite(I18n.T("You feel yourself teleporting, but are suddenly flung back to where you just were!")));

        Waiting.NoSleep(teleportee.apply_status_effect(typeof(datum_status_effect_incapacitating_paralyzed), 5 * SECONDS));
        do_sparks(5, TRUE, teleportee, spark_type: typeof(datum_effect_system_basic_spark_spread_quantum));
        return TRUE;
    }

    /// Signal for COMSIG_MOB_PRE_JAUNT that prevents a user from entering a jaunt.
    // tg: code/game/objects/items/implants/security/implant_noteleport.dm:48
    [SignalHandler]
    public virtual object on_jaunt(mob_living jaunter = null)
    {
        to_chat(jaunter, span_holoparasite(I18n.T("As you attempt to jaunt, you slam directly into the barrier between realities and are sent crashing back into corporeality!")));

        Waiting.NoSleep(jaunter.apply_status_effect(typeof(datum_status_effect_incapacitating_paralyzed), 5 * SECONDS));
        do_sparks(5, TRUE, jaunter, spark_type: typeof(datum_effect_system_basic_spark_spread_quantum));
        return COMPONENT_BLOCK_JAUNT;
    }
}
