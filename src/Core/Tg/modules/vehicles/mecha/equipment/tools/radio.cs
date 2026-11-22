// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/equipment/tools/radio.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

///Mech radio module
// tg: code/modules/vehicles/mecha/equipment/tools/radio.dm:2
public partial class obj_item_mecha_parts_mecha_equipment_radio : obj_item_mecha_parts_mecha_equipment
{
    ///Internal radio item
    // tg: code/modules/vehicles/mecha/equipment/tools/radio.dm:9
    public obj_item_radio_mech radio;

    // tg: code/modules/vehicles/mecha/equipment/tools/radio.dm:3
    public obj_item_mecha_parts_mecha_equipment_radio()
    {
        name = "mounted radio";
        desc = "A basic component of every vehicle.";
        icon_state = "mecha_radio";
        equipment_slot = "mecha_utility";
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/radio.dm:11
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        radio = Dm.@new<obj_item_radio_mech>(this);
        RegisterSignal(radio, COMSIG_QDELETING, nameof(radio_deleted));
        return dot;
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/radio.dm:16
    public override object Destroy(float? force = null)
    {
        qdel(radio);
        return base.Destroy(force);
    }

    // tg: code/modules/vehicles/mecha/equipment/tools/radio.dm:20
    public override DmList get_snowflake_data() => Dm.list(
        Dm.kv("snowflake_id", MECHA_SNOWFLAKE_ID_RADIO),
        Dm.kv("microphone", radio.get_broadcasting()),
        Dm.kv("speaker", radio.get_listening()),
        Dm.kv("frequency", radio.get_frequency()),
        Dm.kv("minFrequency", Dm.truthy(radio.freerange) ? MIN_FREE_FREQ : MIN_FREQ),
        Dm.kv("maxFrequency", Dm.truthy(radio.freerange) ? MAX_FREE_FREQ : MAX_FREQ));

    // tg: code/modules/vehicles/mecha/equipment/tools/radio.dm:30
    public override DmTask<object> handle_ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        if (Dm.eq(action, "toggle_microphone"))
        {
            radio.set_broadcasting(Dm.truthy(radio.get_broadcasting()) ? 0 : 1);
            return DmTask<object>.FromResult(TRUE);
        }
        if (Dm.eq(action, "toggle_speaker"))
        {
            radio.set_listening(Dm.truthy(radio.get_listening()) ? 0 : 1);
            return DmTask<object>.FromResult(TRUE);
        }
        if (Dm.eq(action, "set_frequency"))
        {
            object new_frequency = text2num(@params["new_frequency"]);
            radio.set_frequency(sanitize_frequency(new_frequency, radio.freerange, Dm.bitand(radio.special_channels, RADIO_SPECIAL_SYNDIE)));
            return DmTask<object>.FromResult(TRUE);
        }
        return DmTask<object>.FromResult(FALSE);
    }

    ///Internal radio got deleted, somehow
    // tg: code/modules/vehicles/mecha/equipment/tools/radio.dm:45
    public virtual void radio_deleted()
    {
        if (!QDELETED(this))
            qdel(this);
    }
}
