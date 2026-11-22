// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/block_turf_fingerprints.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps J4, D298).

// tg: code/datums/elements/block_turf_fingerprints.dm:7
public partial class datum_element_block_turf_fingerprints : datum_element
{
    // tg: code/datums/elements/block_turf_fingerprints.dm:9
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        await base.Attach(target, __extra);
        if (!ismovable(target))
            return ELEMENT_INCOMPATIBLE;

        atom_movable target_movable = target as atom_movable;
        if (isturf(target_movable.loc))
            apply_to_turf(target_movable.loc as turf);

        RegisterSignal(target, COMSIG_MOVABLE_MOVED, nameof(move_turf));
        return null;
    }

    // tg: code/datums/elements/block_turf_fingerprints.dm:20
    public override object Detach(datum target = null, params object[] __extra)
    {
        object dot = base.Detach(target, __extra);
        atom_movable movable = target as atom_movable;
        if (isturf(movable.loc))
            remove_from_turf(movable.loc as turf);

        UnregisterSignal(target, COMSIG_MOVABLE_MOVED);
        return dot;
    }

    // tg: code/datums/elements/block_turf_fingerprints.dm:27
    public virtual void apply_to_turf(turf the_turf = null)
    {
        // It's possible two things with this element could be on the same turf, so let's avoid double-applying
        if (Dm.truthy(Dm.bitand(the_turf.interaction_flags_atom, INTERACT_ATOM_NO_FINGERPRINT_ATTACK_HAND)))
        {
            // But what if the turf has this flag by default? We still need to override register a signal.
            // Otherwise we may run into a very niche bug:
            // - A turf as this flag by default
            // - A movable with this element is placed on the turf
            // - It does not gain the flag nor register a signal
            // - The turf changes, and the new turf does not gain the flag
            if (Dm.truthy(Dm.bitand(Dm.initial(the_turf, (turf p) => p.interaction_flags_atom), INTERACT_ATOM_NO_FINGERPRINT_ATTACK_HAND)))
                RegisterSignal(the_turf, COMSIG_TURF_CHANGE, nameof(replace_our_turf), @override: TRUE);
            return;
        }

        the_turf.interaction_flags_atom = Dm.bitor(the_turf.interaction_flags_atom, INTERACT_ATOM_NO_FINGERPRINT_ATTACK_HAND);
        RegisterSignal(the_turf, COMSIG_TURF_CHANGE, nameof(replace_our_turf));
    }

    // tg: code/datums/elements/block_turf_fingerprints.dm:43
    public virtual void remove_from_turf(turf the_turf = null)
    {
        the_turf.interaction_flags_atom = Dm.bitand(the_turf.interaction_flags_atom, ~INTERACT_ATOM_NO_FINGERPRINT_ATTACK_HAND);
        UnregisterSignal(the_turf, COMSIG_TURF_CHANGE);
    }

    // tg: code/datums/elements/block_turf_fingerprints.dm:47
    [SignalHandler]
    public virtual void move_turf(atom_movable source = null, atom old_loc = null, params object[] __extra)
    {
        if (isturf(old_loc))
            remove_from_turf(old_loc as turf);
        if (isturf(source.loc))
            apply_to_turf(source.loc as turf);
    }

    // tg: code/datums/elements/block_turf_fingerprints.dm:54
    [SignalHandler]
    public virtual void replace_our_turf(datum source = null, object path = null, object new_baseturfs = null, object flags = null, DmList post_change_callbacks = null)
    {
        post_change_callbacks.Add(Dm.@new<datum_callback>(this, nameof(apply_to_turf)));
    }
}
