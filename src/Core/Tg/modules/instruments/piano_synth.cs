// Translated from tgstation (snapshot 1d99bd7f): code/modules/instruments/piano_synth.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The synthesizer and the headphones (silicons SI2: the service cyborg's synthesizer; SI6c: their songs). The circuit shell and its
// synth components are wiremod's (not ported, not-translated.md section 159).

// tg: code/modules/instruments/piano_synth.dm:2
public partial class obj_item_instrument_piano_synth : obj_item_instrument
{
    // tg: code/modules/instruments/piano_synth.dm:13
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        song.allowed_instrument_ids = SSinstruments.synthesizer_instrument_ids;
        Dm.Deferred("code/modules/instruments/piano_synth.dm:16", "Initialize: AddComponent(/datum/component/shell, list(new circuit_type), shell_capacity), the circuit shell (wiremod, not ported)");
        return dot;
    }

    // tg: code/modules/instruments/piano_synth.dm:18
    public override async DmTask<object> attack_animal(mob user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(mouse_playable) || !ismouse(user))
            return await base.attack_animal(user, modifiers);
        await song.ui_interact(user);
        return null;
    }
}

// tg: code/modules/instruments/piano_synth.dm:23
public partial class obj_item_instrument_piano_synth_headphones : obj_item_instrument_piano_synth
{
    // tg: code/modules/instruments/piano_synth.dm:44
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        RegisterSignal(this, COMSIG_INSTRUMENT_START, nameof(update_icon_for_playing_music));
        RegisterSignal(this, COMSIG_INSTRUMENT_END, nameof(update_icon_for_playing_music));
        return dot;
    }

    // Called by a component signal to update musical note VFX for songs playing while worn.
    // tg: code/modules/instruments/piano_synth.dm:51
    public virtual void update_icon_for_playing_music(datum source = null, datum starting_song = null, atom player = null)
    {
        update_appearance();
    }

    // tg: code/modules/instruments/piano_synth.dm:55
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[][]", Dm.initial(this, (obj_item_instrument_piano_synth_headphones h) => h.icon_state), Dm.truthy(song?.playing) ? "_on" : null);
        return dot;
    }
}
