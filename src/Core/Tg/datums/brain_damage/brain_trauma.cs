// Translated from tgstation (snapshot 1d99bd7f): code/datums/brain_damage/brain_trauma.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

//Brain Traumas are the new actual brain damage. Brain damage itself acts as a way to acquire traumas: every time brain damage is dealt, there's a chance of receiving a trauma.
//This chance gets higher the higher the mob's brainloss is. Removing traumas is a separate thing from removing brain damage: you can get restored to full brain operativity,
// but keep the quirks, until repaired by neurine, surgery, lobotomy or magic; depending on the resilience
// of the trauma.

// tg: code/datums/brain_damage/brain_trauma.dm:6
[DmPath("/datum/brain_trauma")]
public partial class datum_brain_trauma : datum
{
    /// How hard is this to cure?
    // tg: code/datums/brain_damage/brain_trauma.dm:28
    public float? resilience = TRAUMA_RESILIENCE_BASIC;

    // tg: code/datums/brain_damage/brain_trauma.dm:32
    public override object Destroy(float? force = null)
    {
        force ??= FALSE;
        // Handles our references with our brain
        brain?.remove_trauma_from_traumas(this);
        if (owner != null)
        {
            log_game(Dm.text("[] has lost the following brain trauma: []", key_name_and_tag(owner), GetType()));
            Waiting.NoSleep(on_lose());
            owner = null;
        }
        return base.Destroy(force);
    }

    //Called on life ticks
    // tg: code/datums/brain_damage/brain_trauma.dm:42
    public virtual async DmTask on_life(object seconds_per_tick = null)
    {
        return;
    }

    //Called on death
    // tg: code/datums/brain_damage/brain_trauma.dm:46
    public virtual void on_death()
    {
        return;
    }

    //Called when given to a mob
    // tg: code/datums/brain_damage/brain_trauma.dm:50
    [ShouldCallParent]
    public virtual async DmTask<object> on_gain()
    {
        if (Dm.truthy(gain_text))
            to_chat(owner, gain_text);
        RegisterSignal(owner, COMSIG_MOB_SAY, nameof(handle_speech));
        RegisterSignal(owner, COMSIG_MOVABLE_HEAR, nameof(handle_hearing));
        return TRUE;
    }

    //Called when removed from a mob
    // tg: code/datums/brain_damage/brain_trauma.dm:59
    [ShouldCallParent]
    public virtual async DmTask on_lose(object silent = null)
    {
        if (!Dm.truthy(silent) && Dm.truthy(lose_text))
            to_chat(owner, lose_text);
        UnregisterSignal(owner, COMSIG_MOB_SAY);
        UnregisterSignal(owner, COMSIG_MOVABLE_HEAR);
    }

    //Called when hearing a spoken message
    // tg: code/datums/brain_damage/brain_trauma.dm:67
    [SignalHandler]
    public virtual void handle_hearing(datum source = null, DmList hearing_args = null)
    {
        UnregisterSignal(owner, COMSIG_MOVABLE_HEAR);
    }

    //Called when speaking
    // tg: code/datums/brain_damage/brain_trauma.dm:73
    [SignalHandler]
    public virtual void handle_speech(datum source = null, DmList speech_args = null)
    {
        UnregisterSignal(owner, COMSIG_MOB_SAY);
    }
}
