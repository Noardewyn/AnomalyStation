// Translated from tgstation (snapshot 1d99bd7f): code/modules/meteors/meteor_waves.dm, meteor_mode_controller.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2b (D327): both files whole: the wave delay and the meteor lists of each wave kind, and the meteor mode's controller (the
// meteor game mode's endless waves, which an admin or a dynamic ruleset starts). The var blocks are generated (D111).

public partial class datum_controller_global_vars
{
    // tg: code/modules/meteors/meteor_waves.dm:1  GLOBAL_VAR_INIT(meteor_wave_delay, 625)
    public object meteor_wave_delay;
    public virtual void InitGlobalmeteor_wave_delay()
    {
        meteor_wave_delay = 625; //minimum wait between waves in tenths of seconds
        Dm.append(ref gvars_datum_init_order, "meteor_wave_delay");
    }

    // tg: code/modules/meteors/meteor_waves.dm:6
    public DmList meteors_normal;
    public virtual void InitGlobalmeteors_normal()
    {
        meteors_normal = Dm.list(Dm.kv(typeof(obj_effect_meteor_dust), 3), Dm.kv(typeof(obj_effect_meteor_medium), 8), Dm.kv(typeof(obj_effect_meteor_big), 3),
            Dm.kv(typeof(obj_effect_meteor_flaming), 1), Dm.kv(typeof(obj_effect_meteor_irradiated), 2), Dm.kv(typeof(obj_effect_meteor_carp), 1), Dm.kv(typeof(obj_effect_meteor_bluespace), 1),
            Dm.kv(typeof(obj_effect_meteor_banana), 1), Dm.kv(typeof(obj_effect_meteor_emp), 1)); //for normal meteor event
        Dm.append(ref gvars_datum_init_order, "meteors_normal");
    }

    // tg: code/modules/meteors/meteor_waves.dm:10
    public DmList meteors_threatening;
    public virtual void InitGlobalmeteors_threatening()
    {
        meteors_threatening = Dm.list(Dm.kv(typeof(obj_effect_meteor_medium), 4), Dm.kv(typeof(obj_effect_meteor_big), 8), Dm.kv(typeof(obj_effect_meteor_flaming), 3),
            Dm.kv(typeof(obj_effect_meteor_irradiated), 3), Dm.kv(typeof(obj_effect_meteor_cluster), 1), Dm.kv(typeof(obj_effect_meteor_carp), 1), Dm.kv(typeof(obj_effect_meteor_bluespace), 2),
            Dm.kv(typeof(obj_effect_meteor_emp), 2)); //for threatening meteor event
        Dm.append(ref gvars_datum_init_order, "meteors_threatening");
    }

    // tg: code/modules/meteors/meteor_waves.dm:13
    public DmList meteors_catastrophic;
    public virtual void InitGlobalmeteors_catastrophic()
    {
        meteors_catastrophic = Dm.list(Dm.kv(typeof(obj_effect_meteor_medium), 5), Dm.kv(typeof(obj_effect_meteor_big), 75),
            Dm.kv(typeof(obj_effect_meteor_flaming), 10), Dm.kv(typeof(obj_effect_meteor_irradiated), 8), Dm.kv(typeof(obj_effect_meteor_cluster), 8), Dm.kv(typeof(obj_effect_meteor_tunguska), 1),
            Dm.kv(typeof(obj_effect_meteor_carp), 2), Dm.kv(typeof(obj_effect_meteor_bluespace), 10), Dm.kv(typeof(obj_effect_meteor_emp), 8)); //for catastrophic meteor event
        Dm.append(ref gvars_datum_init_order, "meteors_catastrophic");
    }

    // tg: code/modules/meteors/meteor_waves.dm:17
    public DmList meateors;
    public virtual void InitGlobalmeateors()
    {
        meateors = Dm.list(Dm.kv(typeof(obj_effect_meteor_meaty), 5), Dm.kv(typeof(obj_effect_meteor_meaty_xeno), 1)); //for meaty ore event
        Dm.append(ref gvars_datum_init_order, "meateors");
    }

    // tg: code/modules/meteors/meteor_waves.dm:19
    public DmList meteors_dust;
    public virtual void InitGlobalmeteors_dust()
    {
        meteors_dust = Dm.list(Dm.kv(typeof(obj_effect_meteor_dust), 1)); //for space dust event
        Dm.append(ref gvars_datum_init_order, "meteors_dust");
    }

    // tg: code/modules/meteors/meteor_waves.dm:21
    public DmList meteors_stray;
    public virtual void InitGlobalmeteors_stray()
    {
        meteors_stray = Dm.list(Dm.kv(typeof(obj_effect_meteor_medium), 15), Dm.kv(typeof(obj_effect_meteor_big), 10),
            Dm.kv(typeof(obj_effect_meteor_flaming), 25), Dm.kv(typeof(obj_effect_meteor_irradiated), 30), Dm.kv(typeof(obj_effect_meteor_carp), 25), Dm.kv(typeof(obj_effect_meteor_bluespace), 30),
            Dm.kv(typeof(obj_effect_meteor_banana), 25), Dm.kv(typeof(obj_effect_meteor_meaty), 10), Dm.kv(typeof(obj_effect_meteor_meaty_xeno), 8), Dm.kv(typeof(obj_effect_meteor_emp), 30),
            Dm.kv(typeof(obj_effect_meteor_cluster), 20), Dm.kv(typeof(obj_effect_meteor_tunguska), 1)); //for stray meteor event (bigger numbers for a bit finer weighting)
        Dm.append(ref gvars_datum_init_order, "meteors_stray");
    }

    // tg: code/modules/meteors/meteor_waves.dm:26
    public DmList meteors_sandstorm;
    public virtual void InitGlobalmeteors_sandstorm()
    {
        meteors_sandstorm = Dm.list(Dm.kv(typeof(obj_effect_meteor_sand), 45), Dm.kv(typeof(obj_effect_meteor_dust), 5)); //for sandstorm event
        Dm.append(ref gvars_datum_init_order, "meteors_sandstorm");
    }

    // tg: code/modules/meteors/meteor_waves.dm:28
    public DmList meteorsSPOOKY;
    public virtual void InitGlobalmeteorsSPOOKY()
    {
        meteorsSPOOKY = Dm.list(Dm.kv(typeof(obj_effect_meteor_pumpkin), 1));
        Dm.append(ref gvars_datum_init_order, "meteorsSPOOKY");
    }

    // tg: code/modules/meteors/meteor_mode_controller.dm:2  GLOBAL_DATUM(meteor_mode, /datum/meteor_mode_controller)
    public datum_meteor_mode_controller meteor_mode;

    // tg: code/modules/meteors/meteor_mode_controller.dm:2  (GLOBAL_UNMANAGED(meteor_mode), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalmeteor_mode()
    {
        return;
    }
}

/// All this datum does is regularly spawn meteors until the round ends.
// tg: code/modules/meteors/meteor_mode_controller.dm:5
public partial class datum_meteor_mode_controller : datum
{
    // tg: code/modules/meteors/meteor_mode_controller.dm:11
    public virtual void start_meteor()
    {
        START_PROCESSING(SSprocessing, this);
    }

    // tg: code/modules/meteors/meteor_mode_controller.dm:14
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (meteordelay > world.time - Dm.num(SSticker.round_start_time))
            return null;

        DmList wavetype = GLOB.meteors_normal;
        float meteorminutes = (world.time - Dm.num(SSticker.round_start_time) - meteordelay) / 10 / 60;

        if (prob(meteorminutes))
            wavetype = GLOB.meteors_threatening;

        if (prob(meteorminutes / 2))
            wavetype = GLOB.meteors_catastrophic;

        float ramp_up_final = clamp(round(meteorminutes / rampupdelta), 1, 10);

        spawn_meteors(ramp_up_final, wavetype);
        return null;
    }
}
