// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/status_effects.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only INCAPACITATED_IGNORING, become_blind, cure_blind, is_blind, is_blind_from, is_nearsighted_currently (gaps ST5: the eye chart), the staggered family
// (adjust_staggered, adjust_staggered_up_to, set_staggered, set_staggered_if_lower) and the temporary blindness family
// (adjust_temp_blindness, adjust_temp_blindness_up_to, set_temp_blindness, set_temp_blindness_if_lower) translated (M2 P1:
// the status effects the M2 path applies), adjust_dizzy, adjust_jitter, adjust_confusion and adjust_drowsiness (M4b: water
// wakes a mob up), adjust_slurring, adjust_confusion_up_to, set_confusion_if_lower, adjust_eye_blur, adjust_eye_blur_up_to
// and set_eye_blur_if_lower, and adjust_stutter (the M7 projectiles slice: apply_effects), set_jitter_if_lower and set_stutter_if_lower (M7x slice E: the batons), adjust_derpspeech_up_to and adjust_silence (gaps B1: the brain traumas and the voice of god), the rest of the dizziness and drowsiness family and set_silence_if_lower (M8 slice B) and the hallucination family (M8 slice G), the rest of the slurring family and the drugginess family (gaps RG1: the drinks), set_confusion (gaps RG2), the static vision family (gaps BR1: the netpods) (the blunt head trauma and the concussion, item_attack.dm:442, brain_damage/mild.dm:101; the
// stagger combo and the low blood, _species.dm:951, blood.dm:216). The object-like #defines are emitted by the defines converter. The macros
// that expand to a call on src are non-virtual public instance methods of the receiver's class (rules 11.1):
// mob_living for apply_status_effect, remove_status_effect and the timed status effect procs, mob for has_status_effect
// and has_status_effect_from_source.

public static partial class GlobalProcs
{
    /// Checks to see if a mob would be incapacitated even while ignoring some types
    /// Does this by inverting the passed in flags and seeing if we're still incapacitated
    // tg: code/__DEFINES/status_effects.dm:50
    public static float INCAPACITATED_IGNORING(object mob, float flags) => Dm.bitand(((mob)mob).incapacitated, Dm.bitnot(flags));
}

public partial class mob_living : mob
{
    /// Causes the mob to become blind via the passed source
    // tg: code/__DEFINES/status_effects.dm:75
    public DmTask<object> become_blind(object source) => apply_status_effect(typeof(datum_status_effect_grouped_blindness), source);

    /// Cures the mob's blindness from the passed source, removing blindness wholesale if no sources are left
    // tg: code/__DEFINES/status_effects.dm:77
    public object cure_blind(object source) => remove_status_effect(typeof(datum_status_effect_grouped_blindness), source);

    /// Causes the mob to become nearsighted via the passed source
    // tg: code/__DEFINES/status_effects.dm:92  (gaps P3, D327: the near sightness mutation)
    public DmTask<object> become_nearsighted(object source) => apply_status_effect(typeof(datum_status_effect_grouped_nearsighted), source);

    /// Causes the mob to become nearsighted from the passed source by a severity, which may be corrected with glasses.
    // tg: code/__DEFINES/status_effects.dm:94
    public DmTask<object> assign_nearsightedness(object source, object amount, object correctable) => apply_status_effect(typeof(datum_status_effect_grouped_nearsighted), source, amount, correctable);

    /// Cures the mob's nearsightedness from the passed source, removing nearsighted wholesale if no sources are left.
    // tg: code/__DEFINES/status_effects.dm:96
    public object cure_nearsighted(object source) => remove_status_effect(typeof(datum_status_effect_grouped_nearsighted), source);
}

public partial class mob : atom_movable
{
    /// Is the mob blind?
    // tg: code/__DEFINES/status_effects.dm:80  (variadic: the arguments are not used)
    public object is_blind(params object[] __extra) => has_status_effect(typeof(datum_status_effect_grouped_blindness));

    /// Is the mob blind from the passed source or sources?
    // tg: code/__DEFINES/status_effects.dm:82
    public object is_blind_from(object sources) => has_status_effect_from_source(typeof(datum_status_effect_grouped_blindness), sources);

    /// Is the mob nearsighted? (M8 slice D: the eyes' status in a health scan)
    // tg: code/__DEFINES/status_effects.dm:99  (variadic: the arguments are not used)
    public object is_nearsighted(params object[] __extra) => has_status_effect(typeof(datum_status_effect_grouped_nearsighted));

    /// Is the mob nearsighted from the passed source or sources?
    // tg: code/__DEFINES/status_effects.dm:101
    public object is_nearsighted_from(object sources) => has_status_effect_from_source(typeof(datum_status_effect_grouped_nearsighted), sources);

    /// Is the mob nearsighted CURRENTLY?
    /// This check fails if the mob is nearsighted but is wearing glasses,
    /// While is_nearsighted will always succeed even if they are wearing glasses.
    // tg: code/__DEFINES/status_effects.dm:105  (gaps ST5: the eye chart)
    public virtual object is_nearsighted_currently()
    {
        datum_status_effect_grouped_nearsighted nearsight = has_status_effect(typeof(datum_status_effect_grouped_nearsighted)) as datum_status_effect_grouped_nearsighted;
        if (nearsight == null)
            return FALSE;
        return Dm.num(nearsight.should_be_nearsighted() > NEARSIGHTED_CORRECTED);
    }
}

public partial class mob_living : mob
{
    // tg: code/__DEFINES/status_effects.dm:136  (the M7 projectiles slice: a projectile's stutter, apply_effects)
    public DmTask adjust_stutter(object duration) => adjust_timed_status_effect(duration, typeof(datum_status_effect_speech_stutter));

    // tg: code/__DEFINES/status_effects.dm:137  (M7x slice D: the tased status effect)
    public DmTask adjust_stutter_up_to(object duration, object up_to) => adjust_timed_status_effect(duration, typeof(datum_status_effect_speech_stutter), up_to);

    // tg: code/__DEFINES/status_effects.dm:146
    public DmTask adjust_slurring(object duration) => adjust_timed_status_effect(duration, typeof(datum_status_effect_speech_slurring_generic));

    // tg: code/__DEFINES/status_effects.dm:147  (gaps RG1: the drinks)
    public DmTask adjust_slurring_up_to(object duration, object up_to) => adjust_timed_status_effect(duration, typeof(datum_status_effect_speech_slurring_generic), up_to);

    // tg: code/__DEFINES/status_effects.dm:148  (gaps RG1: the drinks)
    public DmTask set_slurring(object duration) => set_timed_status_effect(duration, typeof(datum_status_effect_speech_slurring_generic));

    // tg: code/__DEFINES/status_effects.dm:149  (gaps RG1: the drinks)
    public DmTask set_slurring_if_lower(object duration) => set_timed_status_effect(duration, typeof(datum_status_effect_speech_slurring_generic), TRUE);

    // tg: code/__DEFINES/status_effects.dm:171  (gaps RG1: the drinks' and the drugs' high, debuffs/drugginess.dm)
    public DmTask adjust_drugginess(object duration) => adjust_timed_status_effect(duration, typeof(datum_status_effect_drugginess));

    // tg: code/__DEFINES/status_effects.dm:172  (gaps RG1)
    public DmTask adjust_drugginess_up_to(object duration, object up_to) => adjust_timed_status_effect(duration, typeof(datum_status_effect_drugginess), up_to);

    // tg: code/__DEFINES/status_effects.dm:173  (gaps RG1)
    public DmTask set_drugginess(object duration) => set_timed_status_effect(duration, typeof(datum_status_effect_drugginess));

    // tg: code/__DEFINES/status_effects.dm:174  (gaps RG1)
    public DmTask set_drugginess_if_lower(object duration) => set_timed_status_effect(duration, typeof(datum_status_effect_drugginess), TRUE);

    // tg: code/__DEFINES/status_effects.dm:168  (gaps RG2, D339: bungotoxin)
    public DmTask set_confusion(object duration) => set_timed_status_effect(duration, typeof(datum_status_effect_confusion));

    // tg: code/__DEFINES/status_effects.dm:151  (M4b: water wakes a mob up, other_reagents.dm:216)
    public DmTask adjust_dizzy(object duration) => adjust_timed_status_effect(duration, typeof(datum_status_effect_dizziness));

    // tg: code/__DEFINES/status_effects.dm:152  (M8 slice B: the heart attack, the stagger combo)
    public DmTask adjust_dizzy_up_to(object duration, object up_to) => adjust_timed_status_effect(duration, typeof(datum_status_effect_dizziness), up_to);

    // tg: code/__DEFINES/status_effects.dm:153
    public DmTask set_dizzy(object duration) => set_timed_status_effect(duration, typeof(datum_status_effect_dizziness));

    // tg: code/__DEFINES/status_effects.dm:154  (M8 slice B: morphine, atropine, penthrite, the low blood)
    public DmTask set_dizzy_if_lower(object duration) => set_timed_status_effect(duration, typeof(datum_status_effect_dizziness), TRUE);

    // tg: code/__DEFINES/status_effects.dm:156
    public DmTask adjust_staggered(object duration) => adjust_timed_status_effect(duration, typeof(datum_status_effect_staggered));

    // tg: code/__DEFINES/status_effects.dm:157
    public DmTask adjust_staggered_up_to(object duration, object up_to) => adjust_timed_status_effect(duration, typeof(datum_status_effect_staggered), up_to);

    // tg: code/__DEFINES/status_effects.dm:158
    public DmTask set_staggered(object duration) => set_timed_status_effect(duration, typeof(datum_status_effect_staggered));

    // tg: code/__DEFINES/status_effects.dm:159
    public DmTask set_staggered_if_lower(object duration) => set_timed_status_effect(duration, typeof(datum_status_effect_staggered), TRUE);

    // tg: code/__DEFINES/status_effects.dm:161  (M4b: water wakes a mob up, other_reagents.dm:219)
    public DmTask adjust_jitter(object duration) => adjust_timed_status_effect(duration, typeof(datum_status_effect_jitter));

    // tg: code/__DEFINES/status_effects.dm:139  (M7x slice E: the stun baton's stutter)
    public DmTask set_stutter(object duration) => set_timed_status_effect(duration, typeof(datum_status_effect_speech_stutter));
    public DmTask set_stutter_if_lower(object duration) => set_timed_status_effect(duration, typeof(datum_status_effect_speech_stutter), TRUE);

    // tg: code/__DEFINES/status_effects.dm:142  (gaps B1: the dumbness trauma's derpspeech)
    public DmTask adjust_derpspeech_up_to(object duration, object up_to) => adjust_timed_status_effect(duration, typeof(datum_status_effect_speech_stutter_derpspeech), up_to);

    // tg: code/__DEFINES/status_effects.dm:143  (silicons SI6: an EMP's corrupted pAI vocabulary)
    public DmTask set_derpspeech(object duration) => set_timed_status_effect(duration, typeof(datum_status_effect_speech_stutter_derpspeech));

    // tg: code/__DEFINES/status_effects.dm:162  (M7x slice D: the tased status effect)
    public DmTask adjust_jitter_up_to(object duration, object up_to) => adjust_timed_status_effect(duration, typeof(datum_status_effect_jitter), up_to);

    // tg: code/__DEFINES/status_effects.dm:163  (gaps P3: the epilepsy mutation)
    public DmTask set_jitter(object duration) => set_timed_status_effect(duration, typeof(datum_status_effect_jitter));

    // tg: code/__DEFINES/status_effects.dm:164  (M7x slice E: the stun baton's jitter)
    public DmTask set_jitter_if_lower(object duration) => set_timed_status_effect(duration, typeof(datum_status_effect_jitter), TRUE);

    // tg: code/__DEFINES/status_effects.dm:166  (M4b: water wakes a mob up, other_reagents.dm:217)
    public DmTask adjust_confusion(object duration) => adjust_timed_status_effect(duration, typeof(datum_status_effect_confusion));

    // tg: code/__DEFINES/status_effects.dm:167
    public DmTask adjust_confusion_up_to(object duration, object up_to) => adjust_timed_status_effect(duration, typeof(datum_status_effect_confusion), up_to);

    // tg: code/__DEFINES/status_effects.dm:169
    public DmTask set_confusion_if_lower(object duration) => set_timed_status_effect(duration, typeof(datum_status_effect_confusion), TRUE);

    // tg: code/__DEFINES/status_effects.dm:186  (M4b: water wakes a mob up, other_reagents.dm:218)
    public DmTask adjust_drowsiness(object duration) => adjust_timed_status_effect(duration, typeof(datum_status_effect_drowsiness));

    // tg: code/__DEFINES/status_effects.dm:187  (M8 slice B: morphine)
    public DmTask adjust_drowsiness_up_to(object duration, object up_to) => adjust_timed_status_effect(duration, typeof(datum_status_effect_drowsiness), up_to);

    // tg: code/__DEFINES/status_effects.dm:188
    public DmTask set_drowsiness(object duration) => set_timed_status_effect(duration, typeof(datum_status_effect_drowsiness));

    // tg: code/__DEFINES/status_effects.dm:189
    public DmTask set_drowsiness_if_lower(object duration) => set_timed_status_effect(duration, typeof(datum_status_effect_drowsiness), TRUE);

    // tg: code/__DEFINES/status_effects.dm:176  (gaps B1: the voice of god's silence)
    public DmTask adjust_silence(object duration) => adjust_timed_status_effect(duration, typeof(datum_status_effect_silenced));

    // tg: code/__DEFINES/status_effects.dm:177  (syndicate X3a: the sleeping carp's Kraken Wrack)
    public DmTask adjust_silence_up_to(object duration, object up_to) => adjust_timed_status_effect(duration, typeof(datum_status_effect_silenced), up_to);

    // tg: code/__DEFINES/status_effects.dm:179  (M8 slice B: the heart's desperation)
    public DmTask set_silence_if_lower(object duration) => set_timed_status_effect(duration, typeof(datum_status_effect_silenced), TRUE);

    // tg: code/__DEFINES/status_effects.dm:181  (M8 slice G: the hallucination status effect)
    public DmTask adjust_hallucinations(object duration) => adjust_timed_status_effect(duration, typeof(datum_status_effect_hallucination));

    // tg: code/__DEFINES/status_effects.dm:182
    public DmTask adjust_hallucinations_up_to(object duration, object up_to) => adjust_timed_status_effect(duration, typeof(datum_status_effect_hallucination), up_to);

    // tg: code/__DEFINES/status_effects.dm:183
    public DmTask set_hallucinations(object duration) => set_timed_status_effect(duration, typeof(datum_status_effect_hallucination));

    // tg: code/__DEFINES/status_effects.dm:184
    public DmTask set_hallucinations_if_lower(object duration) => set_timed_status_effect(duration, typeof(datum_status_effect_hallucination), TRUE);

    // tg: code/__DEFINES/status_effects.dm:194
    public DmTask adjust_eye_blur(object duration) => adjust_timed_status_effect(duration, typeof(datum_status_effect_eye_blur));

    // tg: code/__DEFINES/status_effects.dm:195
    public DmTask adjust_eye_blur_up_to(object duration, object up_to) => adjust_timed_status_effect(duration, typeof(datum_status_effect_eye_blur), up_to);

    // tg: code/__DEFINES/status_effects.dm:196  (gaps P5: the aiuri reaction's flash)
    public DmTask set_eye_blur(object duration) => set_timed_status_effect(duration, typeof(datum_status_effect_eye_blur));

    // tg: code/__DEFINES/status_effects.dm:197
    public DmTask set_eye_blur_if_lower(object duration) => set_timed_status_effect(duration, typeof(datum_status_effect_eye_blur), TRUE);

    // tg: code/__DEFINES/status_effects.dm:199
    public DmTask adjust_temp_blindness(object duration) => adjust_timed_status_effect(duration, typeof(datum_status_effect_temporary_blindness));

    // tg: code/__DEFINES/status_effects.dm:200
    public DmTask adjust_temp_blindness_up_to(object duration, object up_to) => adjust_timed_status_effect(duration, typeof(datum_status_effect_temporary_blindness), up_to);

    // tg: code/__DEFINES/status_effects.dm:201
    public DmTask set_temp_blindness(object duration) => set_timed_status_effect(duration, typeof(datum_status_effect_temporary_blindness));

    // tg: code/__DEFINES/status_effects.dm:202
    public DmTask set_temp_blindness_if_lower(object duration) => set_timed_status_effect(duration, typeof(datum_status_effect_temporary_blindness), TRUE);

    // tg: code/__DEFINES/status_effects.dm:204  (gaps BR1: the netpod's and the avatar's connection static)
    public DmTask adjust_static_vision(object duration) => adjust_timed_status_effect(duration, typeof(datum_status_effect_static_vision));

    // tg: code/__DEFINES/status_effects.dm:205
    public DmTask adjust_static_vision_up_to(object duration, object up_to) => adjust_timed_status_effect(duration, typeof(datum_status_effect_static_vision), up_to);

    // tg: code/__DEFINES/status_effects.dm:206
    public DmTask set_static_vision(object duration) => set_timed_status_effect(duration, typeof(datum_status_effect_static_vision));

    // tg: code/__DEFINES/status_effects.dm:207
    public DmTask set_static_vision_if_lower(object duration) => set_timed_status_effect(duration, typeof(datum_status_effect_static_vision), TRUE);
}
