// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/direct_explosive_trap.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X2 (D316): the whole file but the bomber log and the admin notice with its links (the admin logs). The var block
// is generated.

/**
 * Responds to certain signals and 'explodes' on the person using the item.
 * Differs from `interaction_booby_trap` in that this doesn't actually explode, it just directly calls ex_act on one person.
 */
// tg: code/datums/components/direct_explosive_trap.dm:5
public partial class datum_component_direct_explosive_trap : datum_component
{
    // tg: code/datums/components/direct_explosive_trap.dm:17
    [DmParams("saboteur", "explosive_force", "expire_time", "glow_colour", "explosive_checks", "triggering_signals")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        mob_living saboteur = Dm.arg<mob_living>(__extra, 0);
        object explosive_force = Dm.arg(__extra, 1) ?? EXPLODE_HEAVY;
        object expire_time = Dm.arg(__extra, 2) ?? 1 * MINUTES;
        object glow_colour = Dm.arg(__extra, 3) ?? COLOR_RED;
        datum_callback explosive_checks = Dm.arg<datum_callback>(__extra, 4);
        DmList triggering_signals = Dm.arg<DmList>(__extra, 5) ?? Dm.list(COMSIG_ATOM_ATTACKBY, COMSIG_ATOM_ATTACK_HAND, COMSIG_ATOM_BUMPED);
        await base.Initialize(__extra);
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;
        this.saboteur = saboteur;
        this.explosive_force = explosive_force;
        this.glow_colour = glow_colour;
        this.explosive_checks = explosive_checks;
        this.triggering_signals = triggering_signals;

        if (Dm.num(expire_time) > 0)
            _addtimer(Dm.@new<datum_callback>(this, nameof(bomb_expired)), Dm.num(expire_time), TIMER_DELETE_ME, file: "code/datums/components/direct_explosive_trap.dm", line: 35);
        return null;
    }

    // tg: code/datums/components/direct_explosive_trap.dm:37
    public override async DmTask RegisterWithParent()
    {
        if (!Dm.@in(COMSIG_ATOM_EXAMINE, triggering_signals)) // Maybe you're being extra mean with this one
            RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examined));
        RegisterSignals(parent, triggering_signals, nameof(explode));
        if (saboteur != null)
            RegisterSignal(saboteur, COMSIG_QDELETING, nameof(on_bomber_deleted));
        log_bomber(saboteur, Dm.text("has set a direct_explosive_trap with severity [] on", explosive_force), parent as atom, Dm.text("to detonate on the signals []", english_list(triggering_signals)), message_admins: saboteur != null ? TRUE : FALSE);;
        await DmTask.CompletedTask;
    }

    // tg: code/datums/components/direct_explosive_trap.dm:45
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.add(Dm.list(COMSIG_ATOM_EXAMINE), triggering_signals));
        if (saboteur != null)
            UnregisterSignal(saboteur, COMSIG_QDELETING);
        await DmTask.CompletedTask;
    }

    // tg: code/datums/components/direct_explosive_trap.dm:50
    public override object Destroy(float? force = null)
    {
        if (saboteur == null)
            return base.Destroy(force);
        UnregisterSignal(saboteur, COMSIG_QDELETING);
        saboteur = null;
        return base.Destroy(force);
    }

    /// Called if we sit too long without going off
    // tg: code/datums/components/direct_explosive_trap.dm:58
    public virtual void bomb_expired()
    {
        if (saboteur != null)
            to_chat(saboteur, span_bolddanger("Failure! Your trap didn't catch anyone this time..."));
        qdel(this);
    }

    /// Let people know something is up
    // tg: code/datums/components/direct_explosive_trap.dm:64
    [SignalHandler]
    public virtual void on_examined(datum source = null, mob user = null, DmList text = null)
    {
        Dm.append(text, span_holoparasite(Dm.text("It glows with a strange <font color=\"[]\">light</font>...", glow_colour)));
    }

    /// Blow up
    // tg: code/datums/components/direct_explosive_trap.dm:69
    [SignalHandler]
    public virtual void explode(atom source = null, object victim = null)
    {
        if (!isliving(victim))
            return;
        mob_living living_victim = (mob_living)victim;
        if (explosive_checks != null && !Dm.truthy(Waiting.NoSleep(explosive_checks.Invoke(living_victim))))
            return;
        to_chat(living_victim, span_bolddanger(Dm.text("[] was boobytrapped!", source)));
        if (saboteur != null)
            to_chat(saboteur, span_bolddanger(Dm.text("Success! Your trap on [] caught []!", source, living_victim.name)));
        atom parent_atom = (atom)parent;
        message_admins(Dm.text("Direct EX_ACT explosion with severity [] on []. Caused by direct_explosive_trap on: []. Set by: [].", explosive_force, ADMIN_LOOKUPFLW(victim), ADMIN_VERBOSEJMP(parent_atom), key_name(saboteur)));
        log_game(Dm.text("Direct EX_ACT explosion with severity [] on []. Caused by direct_explosive_trap on: [] at []. Set by: [].", explosive_force, key_name(victim), parent_atom.name, AREACOORD(parent_atom), key_name(saboteur)));
        log_game(Dm.text("Direct EX_ACT explosion with severity [] on []. Caused by direct_explosive_trap on: [] at []. Set by: [].", explosive_force, key_name(living_victim), parent_atom.name, AREACOORD(parent_atom), key_name(saboteur)));
        playsound(source, Dm.resource("sound/effects/explosion/explosion2.ogg"), 200, TRUE);
        Dm.@new<obj_effect_temp_visual_explosion>(get_turf(source));
        Waiting.NoSleep(EX_ACT(living_victim, explosive_force));
        qdel(this);
    }

    /// Don't hang a reference to the person who placed the bomb
    // tg: code/datums/components/direct_explosive_trap.dm:87
    [SignalHandler]
    public virtual void on_bomber_deleted()
    {
        saboteur = null;
    }
}
