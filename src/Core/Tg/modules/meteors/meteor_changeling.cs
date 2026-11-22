// Translated from tgstation (snapshot 1d99bd7f): code/modules/meteors/meteor_changeling.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: the space changeling's meaty meteor, which carries the changeling and ejects it onto the
// station when it splatters, when a meteor shield shoots it down or when it misses the station. The var block is generated (D111).
// Its overrides take the port's signatures of the meteor procs (meteor_types.cs, D299).

// tg: code/modules/meteors/meteor_changeling.dm:2
public partial class obj_effect_meteor_meaty_changeling : obj_effect_meteor_meaty
{
    // tg: code/modules/meteors/meteor_changeling.dm:14
    [DmParams("mapload", "target")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        turf target = Dm.arg<turf>(__extra, 0);
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        landing_target = target;
        return dot;
    }

    // tg: code/modules/meteors/meteor_changeling.dm:18
    public override object Destroy(float? force = null)
    {
        landing_target = null;
        return base.Destroy(force);
    }

    // tg: code/modules/meteors/meteor_changeling.dm:22
    public override async DmTask meteor_effect()
    {
        await base.meteor_effect();
        foreach (atom_movable child in Dm.each<atom_movable>(contents))
            await child.forceMove(get_turf(this));
    }

    // tg: code/modules/meteors/meteor_changeling.dm:27
    public override void ram_turf(turf T = null)
    {
        return; //So we don't instantly smash into our occupant upon unloading them.
    }

    // tg: code/modules/meteors/meteor_changeling.dm:30
    public override object shield_defense(atom defender = null)
    {
        landing_target = defender;
        Waiting.NoSleep(yeet_changeling());
        return TRUE;
    }

    /// Eject changeling onto whatever turf we're aiming at
    // tg: code/modules/meteors/meteor_changeling.dm:36
    public virtual async DmTask yeet_changeling()
    {
        if (landing_target == null)
        {
            //If our destination turf is gone for some reason, we chuck them at the observer_start landmark (usually at the center of the station) as a last resort.
            landing_target = Dm.locate<obj_effect_landmark_observer_start>(GLOB.landmarks_list);
        }

        foreach (atom_movable changeling in Dm.each<atom_movable>(contents))
        {
            await changeling.forceMove(get_turf(this));
            await changeling.throw_at(landing_target, 2, 2);
            changeling.visible_message(span_warning(I18n.T("[] is launched out from inside of \\the []!", changeling, this)), span_changeling(I18n.T("Sensing that something is terribly wrong, we forcibly eject ourselves from \\the []!", this)));
            playsound(changeling, Dm.resource("sound/effects/splat.ogg"), 50, pressure_affected: FALSE);
        }
    }

    //If the meteor misses the station and deletes itself, we make absolutely sure the changeling reaches the station.
    // tg: code/modules/meteors/meteor_changeling.dm:48
    public override void moved_off_z()
    {
        Waiting.NoSleep(yeet_changeling());
        base.moved_off_z();
    }

    // tg: code/modules/meteors/meteor_changeling.dm:52
    public override void check_examine_award(mob user = null) //We don't want this to be a free achievement that comes with the role.
    {
        return;
    }
}
