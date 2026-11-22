// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/login.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/mob/living/silicon/login.dm:1
public partial class mob_living_silicon : mob_living
{
    // tg: code/modules/mob/living/silicon/login.dm:1
    public override async DmTask<object> Login()
    {
        if (mind != null)
            await (mind?.remove_antags_for_borging() ?? DmTask.CompletedTask);
        if (Dm.truthy(SStts.tts_enabled))
        {
            object voice_to_use = client?.prefs.read_preference(typeof(datum_preference_choiced_voice));
            object pitch_to_use = client?.prefs.read_preference(typeof(datum_preference_numeric_tts_voice_pitch));
            if (Dm.truthy(voice_to_use))
                voice = voice_to_use as string;
            if (Dm.truthy(pitch_to_use))
                pitch = Dm.num(pitch_to_use);
        }
        return await base.Login();
    }


    // tg: code/modules/mob/living/silicon/login.dm:14
    public override object auto_deadmin_on_login()
    {
        if (!Dm.truthy(client?.holder))
            return TRUE;
        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_auto_deadmin_silicons))) || Dm.truthy(Dm.bitand(client.prefs?.toggles, DEADMIN_POSITION_SILICON)))
            return client.holder.auto_deadmin();
        return base.auto_deadmin_on_login();
    }
}
