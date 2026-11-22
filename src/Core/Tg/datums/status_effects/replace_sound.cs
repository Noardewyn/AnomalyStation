// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/replace_sound.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DE1 (D333): the whole file (an emote's sound replaced while it lasts, through COMSIG_MOB_EMOTE_SOUND, datums/emotes.dm:277;
// the item kill sound component's deathgasp). The var block is generated (D111).

///A status effect that temporarily replaces the sound of an emote with another
// tg: code/datums/status_effects/replace_sound.dm:2
public partial class datum_status_effect_replace_sound : datum_status_effect
{
    // tg: code/datums/status_effects/replace_sound.dm:11
    [DmParams("new_owner", "duration", "emote_key_to_sound", "priority")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object duration = Dm.arg(__extra, 0);
        DmList emote_key_to_sound = Dm.arg(__extra, 1) as DmList;
        object priority = Dm.arg(__extra, 2) ?? EMOTE_SOUND_STATUS_EFFECT;
        this.duration = Dm.num_or_null(duration);
        this.emote_key_to_sound = emote_key_to_sound.Copy();
        this.priority = priority;
        return await base.on_creation(new_owner, __extra);
    }

    // tg: code/datums/status_effects/replace_sound.dm:17
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        foreach (object key in Dm.each(emote_key_to_sound))
            RegisterSignal(owner, COMSIG_MOB_EMOTE_SOUND(key), nameof(on_emote));
        return TRUE;
    }

    // tg: code/datums/status_effects/replace_sound.dm:22
    [SignalHandler]
    public virtual void on_emote(datum source = null, object key = null, DmList sounds = null)
    {
        object sound_override = get_emote_sound_from_list(emote_key_to_sound[key], source as mob_living);
        sounds[sound_override] = priority;
    }

    // tg: code/datums/status_effects/replace_sound.dm:27
    public override async DmTask<object> on_remove()
    {
        foreach (object key in Dm.each(emote_key_to_sound))
            UnregisterSignal(owner, COMSIG_MOB_EMOTE_SOUND(key));
        return null;
    }
}
