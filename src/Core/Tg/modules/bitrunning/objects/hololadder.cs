// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/objects/hololadder.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the hololadder that takes an avatar out of its domain (a click or a step onto it, three
// seconds) and an observer back to the server. The var block is generated (D111).

// tg: code/modules/bitrunning/objects/hololadder.dm:1
public partial class obj_structure_hololadder : obj_structure
{
    // tg: code/modules/bitrunning/objects/hololadder.dm:15
    [DmParams("mapload", "origin")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_machinery_quantum_server origin = Dm.arg(__extra, 0) as obj_machinery_quantum_server;
        object dot = base.Initialize(mapload, __extra);

        RegisterSignal(loc as datum, COMSIG_ATOM_ENTERED, nameof(on_enter));
        server_ref = WEAKREF(origin) as datum_weakref;
        register_context();
        return dot;
    }

    // tg: code/modules/bitrunning/objects/hololadder.dm:23
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);

        context[SCREENTIP_CONTEXT_LMB] = I18n.T("Disconnect");
        return dot;
    }

    // tg: code/modules/bitrunning/objects/hololadder.dm:29
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;

        if (server_ref.resolve() == null)
        {
            Dm.append(ref dot, span_infoplain(I18n.T("It's not connected to anything.")));
            return dot;
        }

        if (isobserver(user))
        {
            Dm.append(ref dot, span_notice(I18n.T("Left click to view the server that this ladder is connected to.")));
            return dot;
        }

        Dm.append(ref dot, span_infoplain(I18n.T("This ladder is connected to a server. You can click on it or walk over it to disconnect.")));
        return dot;
    }

    // tg: code/modules/bitrunning/objects/hololadder.dm:43
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;

        if (!in_range(this, user) || Dm.truthy(DOING_INTERACTION(user, DOAFTER_SOURCE_CLIMBING_LADDER)))
            return dot;

        await disconnect(user);
        return dot;
    }

    // tg: code/modules/bitrunning/objects/hololadder.dm:54
    public override async DmTask<object> attack_ghost(mob_dead_observer user = null)
    {
        mob_dead_observer ghostie = user;
        obj_machinery_quantum_server our_server = server_ref?.resolve() as obj_machinery_quantum_server;
        if (our_server == null)
            return await base.attack_ghost(user);

        await ghostie.abstract_move(get_turf(our_server));
        return null;
    }

    /// If there's a pilot ref- send the disconnect signal
    // tg: code/modules/bitrunning/objects/hololadder.dm:63
    public virtual async DmTask disconnect(mob user = null)
    {
        if (user.mind == null)
            return;

        obj_machinery_quantum_server our_server = server_ref.resolve() as obj_machinery_quantum_server;
        if (!Dm.truthy(our_server.domain_complete))
        {
            foreach (datum_weakref ghostrole_weakref in Dm.each_cast<datum_weakref>(our_server.spawned_threat_refs))
            {
                mob_living ghostrole = ghostrole_weakref.resolve() as mob_living;
                if (ghostrole != null && ghostrole.stat == STABLE && ghostrole.client != null && Dm.truthy(ghostrole.mind.has_antag_datum(typeof(datum_antagonist_bitrunning_glitch))))
                {
                    to_chat(user, span_danger(I18n.T("A being in the simulation is preventing your retreat. You must either complete your mission or remove the obstacle before safe exit will be possible.")));
                    return;
                }
            }
        }

        if (!HAS_TRAIT(user, TRAIT_TEMPORARY_BODY))
        {
            balloon_alert(user, I18n.T("no connection detected"));
            return;
        }

        balloon_alert(user, I18n.T("disconnecting..."));
        if (Dm.truthy(await do_after(user, travel_time, this)))
            user.SEND_SIGNAL(COMSIG_BITRUNNER_LADDER_SEVER, Dm.list(user));
    }

    /// Helper for times when you dont have hands (gondola??)
    // tg: code/modules/bitrunning/objects/hololadder.dm:85
    [SignalHandler]
    public virtual void on_enter(datum source = null, atom_movable arrived = null, turf old_loc = null)
    {
        if (!isliving(arrived))
            return;

        mob_living user = arrived as mob_living;
        if (user.mind == null)
            return;

        Waiting.Detach(disconnect(user));
    }
}
