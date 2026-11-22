// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/atmos_sensitive.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M3 P3: the /atom roots of the atmos exposure (the light fixture's and the windoor's overrides need them):
// should_atmos_process, atmos_expose and atmos_end; M4b slice 1 (SSair's turfs call them): check_atmos_process and
// process_exposure; M4b slice 2 (the air scrubbers register for their turf's exposure): the element itself (Attach, Detach,
// react_to_move). The whole file.

//This element facilitates reaction to atmos changes when a tile is inactive.
//It adds the object to a list on SSair to be processed for so long as the object wants to be processed
//And removes it as soon as the object is no longer interested
//Don't put it on things that tend to clump into one spot, you will cause lag spikes.
// tg: code/datums/elements/atmos_sensitive.dm:5
public partial class datum_element_atmos_sensitive : datum_element
{
    // tg: code/datums/elements/atmos_sensitive.dm:8
    [DmParams("target", "mapload")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object mapload = Dm.arg(__extra, 0);
        if (!isatom(target)) //How
            return ELEMENT_INCOMPATIBLE;
        atom to_track = target as atom;
        if (to_track.loc != null)
            to_track.RegisterSignal(to_track.loc, COMSIG_TURF_EXPOSE, nameof(atom.check_atmos_process));
        RegisterSignal(to_track, COMSIG_MOVABLE_MOVED, nameof(react_to_move));

        if (!Dm.truthy(mapload) && isopenturf(to_track.loc))
            to_track.atmos_conditions_changed(); //Make sure you're properly registered

        return await base.Attach(target, Dm.forward(__extra, mapload));
    }

    // tg: code/datums/elements/atmos_sensitive.dm:21
    public override object Detach(datum source = null, params object[] __extra)
    {
        atom source_atom = source as atom;
        if (source_atom.loc != null)
            source_atom.UnregisterSignal(source_atom.loc, COMSIG_TURF_EXPOSE); // Attach registers this on the atom, not the element
        UnregisterSignal(source_atom, COMSIG_MOVABLE_MOVED);
        if (Dm.truthy(Dm.bitand(source_atom.flags_1, ATMOS_IS_PROCESSING_1)))
        {
            source_atom.atmos_end();
            Dm.remove(ref SSair.atom_process, source_atom);
            Dm.mask(ref source_atom.flags_1, Dm.bitnot(ATMOS_IS_PROCESSING_1));
        }
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/atmos_sensitive.dm:31  (oldloc widened to /atom, Appendix A: COMSIG_MOVABLE_MOVED passes the old loc, a
    // turf; typed /atom/movable it read null and the old turf kept the registration, M5x slice 6)
    [SignalHandler]
    public virtual void react_to_move(atom source = null, atom oldloc = null, object direction = null, object forced = null)
    {
        if (oldloc != null)
            source.UnregisterSignal(oldloc, COMSIG_TURF_EXPOSE);
        if (source.loc != null)
            source.RegisterSignal(source.loc, COMSIG_TURF_EXPOSE, nameof(atom.check_atmos_process));
        source.atmos_conditions_changed(); //Make sure you're properly registered
    }
}

// tg: code/datums/elements/atmos_sensitive.dm:40
public partial class atom : datum
{
    // tg: code/datums/elements/atmos_sensitive.dm:40  (SIGNAL_HANDLER is dropped, rules 7.9)
    public virtual void check_atmos_process(datum source = null, datum_gas_mixture air = null, object exposed_temperature = null)
    {
        if (Dm.truthy(should_atmos_process(air, exposed_temperature)))
        {
            if (Dm.truthy(Dm.bitand(flags_1, ATMOS_IS_PROCESSING_1)))
                return;
            Dm.append(ref SSair.atom_process, this);
            Dm.combine(ref flags_1, ATMOS_IS_PROCESSING_1);
        }
        else if (Dm.truthy(Dm.bitand(flags_1, ATMOS_IS_PROCESSING_1)))
        {
            atmos_end();
            Dm.remove(ref SSair.atom_process, this);
            Dm.mask(ref flags_1, Dm.bitnot(ATMOS_IS_PROCESSING_1));
        }
    }

    // tg: code/datums/elements/atmos_sensitive.dm:52
    public virtual async DmTask process_exposure()
    {
        turf_open spot = loc as turf_open;
        if (!isopenturf(loc))
        {
            //If you end up in a locker or a wall reconsider your life decisions
            atmos_end();
            Dm.remove(ref SSair.atom_process, this);
            Dm.mask(ref flags_1, Dm.bitnot(ATMOS_IS_PROCESSING_1));
            return;
        }
        if (!Dm.truthy(should_atmos_process(spot.air, spot.air.temperature))) //Things can change without a tile becoming active
        {
            atmos_end();
            Dm.remove(ref SSair.atom_process, this);
            Dm.mask(ref flags_1, Dm.bitnot(ATMOS_IS_PROCESSING_1));
            return;
        }
        await atmos_expose(spot.air, spot.air.temperature);
    }

    ///This is your check proc, return TRUE if you want to be processed, FALSE otherwise
    // tg: code/datums/elements/atmos_sensitive.dm:76
    public virtual object should_atmos_process(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        return FALSE;
    }

    ///This is your process() proc
    // tg: code/datums/elements/atmos_sensitive.dm:80
    public virtual async DmTask<object> atmos_expose(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        return null;
    }

    ///What to do when our requirements are no longer met
    // tg: code/datums/elements/atmos_sensitive.dm:84
    public virtual void atmos_end()
    {
        return;
    }
}

// tg: code/datums/elements/atmos_sensitive.dm:66  (interleaved types, rules 1.9)
public partial class turf_open : turf
{
    // tg: code/datums/elements/atmos_sensitive.dm:66
    public override async DmTask process_exposure()
    {
        if (!Dm.truthy(should_atmos_process(air, air.temperature)))
        {
            atmos_end();
            Dm.remove(ref SSair.atom_process, this);
            Dm.mask(ref flags_1, Dm.bitnot(ATMOS_IS_PROCESSING_1));
            return;
        }
        await atmos_expose(air, air.temperature);
    }
}
