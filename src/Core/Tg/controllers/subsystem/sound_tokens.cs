// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/sound_tokens.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b audio slice 5 (loops and alarms): the whole file. The hand-written var block of the W4.0 stub is kept (rules 10.2).

// tg: code/controllers/subsystem/sound_tokens.dm:1 SUBSYSTEM_DEF(sound_tokens)
[DmPath("/datum/controller/subsystem/sound_tokens")]
public partial class datum_controller_subsystem_sound_tokens : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/sound_tokens.dm:2
    public datum_controller_subsystem_sound_tokens()
    {
        name = "Sound Tokens";
        wait = 1;
        ss_flags = Dm.bitor(Dm.bitor(SS_TICKER, SS_BACKGROUND), SS_NO_INIT);
    }

    // tg: code/controllers/subsystem/sound_tokens.dm:7-8 (object-creating defaults, rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        clients_needing_update = Dm.list();
        currentrun = Dm.list();
    }

    // The New() that SUBSYSTEM_DEF(sound_tokens) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSsound_tokens) (__DEFINES/MC.dm:29)
    // expanded inline (it reads src).
    // tg: code/controllers/subsystem/sound_tokens.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSsound_tokens != this)
        {
            if (SSsound_tokens is datum_controller_subsystem_sound_tokens)
            {
                Recover();
                qdel(SSsound_tokens);
            }
            SSsound_tokens = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/sound_tokens.dm:7
    public DmList clients_needing_update;
    // tg: code/controllers/subsystem/sound_tokens.dm:8
    public DmList currentrun;

    // tg: code/controllers/subsystem/sound_tokens.dm:10
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        if (!Dm.truthy(resumed))
        {
            currentrun = clients_needing_update;
            clients_needing_update = Dm.list();
        }
        while (Dm.truthy(length(currentrun)))
        {
            client client = currentrun[currentrun.len] as client;
            currentrun.len--;
            mob owned_mob = client.mob;
            foreach (datum_sound_token token in Dm.each_cast<datum_sound_token>(owned_mob?.sound_tokens))
                token.update_listener(owned_mob);
            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                break;
        }
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/sound_tokens.dm:1  SUBSYSTEM_DEF(sound_tokens): GLOBAL_REAL(SSsound_tokens, /datum/controller/subsystem/sound_tokens)
    internal datum_controller_subsystem_sound_tokens SSsound_tokens;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_sound_tokens SSsound_tokens => ref TgState.Current.SSsound_tokens;
}
