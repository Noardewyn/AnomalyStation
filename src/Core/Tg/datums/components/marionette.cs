// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/marionette.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI2 (D326): the marionette component, whole (a statue pulled along turns with its puller and speaks for it). The var block is
// generated (D111).

/**
 * Marionette component
 *
 * Upon being grabbed, we will align the direction of the parent with the direction of the grabber when they rotate.
 * While grabbed, will also speak out whatever the original person says
 */
// tg: code/datums/components/marionette.dm:7
public partial class datum_component_marionette : datum_component
{
    // tg: code/datums/components/marionette.dm:11
    public override object Destroy(float? force = null)
    {
        if (grabber != null)
            UnregisterSignal(grabber, Dm.list(COMSIG_MOVABLE_KEYBIND_FACE_DIR, COMSIG_MOB_SAY, COMSIG_QDELETING));
        grabber = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/marionette.dm:17
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_LIVING_TRYING_TO_PULL, nameof(on_pull));
        RegisterSignal(parent, COMSIG_ATOM_NO_LONGER_PULLED, nameof(on_stop_pull));
    }

    // tg: code/datums/components/marionette.dm:21
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(
            COMSIG_LIVING_TRYING_TO_PULL,
            COMSIG_ATOM_NO_LONGER_PULLED));
        await base.UnregisterFromParent(__extra);
    }

    ///Called when something starts pulling us, we now listen in to that thing for rotation.
    // tg: code/datums/components/marionette.dm:29
    [SignalHandler]
    public virtual void on_pull(atom_movable source = null, atom_movable puller = null, object force = null)
    {
        if (puller == null || grabber == puller)
            return;
        if (grabber != null)
            UnregisterSignal(grabber, Dm.list(COMSIG_MOVABLE_KEYBIND_FACE_DIR, COMSIG_MOB_SAY, COMSIG_QDELETING));
        grabber = puller as mob;
        RegisterSignal(grabber, COMSIG_MOVABLE_KEYBIND_FACE_DIR, nameof(on_puller_turn));
        RegisterSignal(grabber, COMSIG_MOB_SAY, nameof(on_puller_speech));
        RegisterSignal(grabber, COMSIG_QDELETING, nameof(on_puller_qdel));
    }

    ///Stopped pulling, we clear out signals and references.
    // tg: code/datums/components/marionette.dm:42
    [SignalHandler]
    public virtual void on_stop_pull(datum source = null, atom_movable was_pulling = null)
    {
        if (grabber != null)
            UnregisterSignal(grabber, Dm.list(COMSIG_MOVABLE_KEYBIND_FACE_DIR, COMSIG_MOB_SAY, COMSIG_QDELETING));
        grabber = null;
    }

    ///Callled when the person grabbin us turns, we rotate to match their direction.
    // tg: code/datums/components/marionette.dm:49
    [SignalHandler]
    public virtual void on_puller_turn(mob_living source = null, object direction = null)
    {
        atom_movable parent_movable = (atom_movable)parent;
        Waiting.NoSleep(parent_movable.setDir(direction));
    }

    ///Called when the person grabbing us speaks, we lower their volume to 1 tile and speak what they said through us.
    // tg: code/datums/components/marionette.dm:55
    [SignalHandler]
    public virtual void on_puller_speech(datum source = null, DmList speech_args = null)
    {
        if (HAS_TRAIT(grabber, TRAIT_SIGN_LANG))
            return;

        object message = speech_args[SPEECH_MESSAGE];
        DmList spans = speech_args[SPEECH_SPANS] as DmList;
        object language = speech_args[SPEECH_LANGUAGE];
        datum_saymode saymode = speech_args[SPEECH_SAYMODE] as datum_saymode;
        atom_movable movable_parent = (atom_movable)parent;
        Waiting.Detach(movable_parent.say(
            message: message,
            spans: spans?.Copy(),
            language: language,
            forced: Dm.text("[]'s marionette", source),
            saymode: saymode,
            message_mods: Dm.list(Dm.kv(MODE_RELAY, TRUE))));
        speech_args[SPEECH_RANGE] = WHISPER_RANGE;
    }

    ///Called when our puller is somehow deleted, we simply clear the reference to them.
    // tg: code/datums/components/marionette.dm:77
    [SignalHandler]
    public virtual void on_puller_qdel()
    {
        grabber = null;
    }
}
