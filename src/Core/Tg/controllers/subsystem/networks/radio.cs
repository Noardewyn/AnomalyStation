// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/networks/radio.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice D: the radio). The say modes it builds are the antagonists' and the silicons' hiveminds (datums/saymode.dm:
// changeling, xeno, vocal cords, binary, holopad); the generated-only one (the vocal cords') is skipped until translated
// (not-translated.md 73).
// The var block is generated (D111).

// tg: code/controllers/subsystem/networks/radio.dm:1 SUBSYSTEM_DEF(radio)
public partial class datum_controller_subsystem_radio : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(radio) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSradio) (__DEFINES/MC.dm:29)
    // expanded inline (it reads src).
    // tg: code/controllers/subsystem/networks/radio.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSradio != this)
        {
            if (SSradio is datum_controller_subsystem_radio)
            {
                Recover();
                qdel(SSradio);
            }
            SSradio = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/networks/radio.dm:8
    public override void PreInit()
    {
        // GAP(M3 P1 -> lead): generated-only types skipped until translated (DV25): the vocal cords' say mode
        foreach (Type _SM in Dm.each_cast<Type>(subtypesof_written(typeof(datum_saymode))))
        {
            datum_saymode SM = Dm.@new(_SM) as datum_saymode; // new _SM() (gaps K1: the port made a bare /datum/saymode here, under a null key)
            saymodes[SM.key] = SM;
        }
        base.PreInit();
    }

    /// Gets the say mode associated with the given key, if available to the given user.
    // tg: code/controllers/subsystem/networks/radio.dm:15
    public virtual datum_saymode get_available_say_mode(mob_living user = null, object key = null)
    {
        datum_saymode selected_saymode = SSradio.saymodes[key] as datum_saymode;
        if (selected_saymode == null)
            return null;
        if (!Dm.truthy(selected_saymode.can_be_used_by(user)))
            return null;
        return selected_saymode;
    }

    // tg: code/controllers/subsystem/networks/radio.dm:23
    public virtual datum_radio_frequency add_object(obj device = null, object new_frequency = null, object filter = null)
    {
        string f_text = num2text(Dm.num(new_frequency));
        datum_radio_frequency frequency = frequencies[f_text] as datum_radio_frequency;
        if (frequency == null)
            frequencies[f_text] = frequency = Dm.@new<datum_radio_frequency>(new_frequency);
        frequency.add_listener(device, filter);
        return frequency;
    }

    // tg: code/controllers/subsystem/networks/radio.dm:31
    public virtual object remove_object(obj device = null, object old_frequency = null)
    {
        string f_text = num2text(Dm.num(old_frequency));
        datum_radio_frequency frequency = frequencies[f_text] as datum_radio_frequency;
        if (frequency != null)
            frequency.remove_listener(device);
        // let's don't delete frequencies in case a non-listener keeps a reference
        return 1;
    }

    // tg: code/controllers/subsystem/networks/radio.dm:39
    public virtual datum_radio_frequency return_frequency(object new_frequency = null)
    {
        string f_text = num2text(Dm.num(new_frequency));
        datum_radio_frequency frequency = frequencies[f_text] as datum_radio_frequency;
        if (frequency == null)
            frequencies[f_text] = frequency = Dm.@new<datum_radio_frequency>(new_frequency);
        return frequency;
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/networks/radio.dm:1  SUBSYSTEM_DEF(radio): GLOBAL_REAL(SSradio, /datum/controller/subsystem/radio)
    internal datum_controller_subsystem_radio SSradio;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_radio SSradio => ref TgState.Current.SSradio;
}
