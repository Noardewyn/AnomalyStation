// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/digital_clock.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DC1 (D333, the station's wall clocks): the whole file. The digital clock on its wall shows the station time's hours and
// minutes as tg's digit overlays every second (its own processing subsystem), lit blue while powered; an emag or an EMP scrambles
// the digits until a multitool resets it, a welder repairs its display, and a right-click wrench takes it off the wall as its
// frame. The var blocks and the wall frame are generated (D111).

// tg: code/game/machinery/digital_clock.dm:1
public partial class obj_machinery_digital_clock : obj_machinery
{
    // tg: code/game/machinery/digital_clock.dm:24
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.wrench_act_secondary(user, tool);
        balloon_alert(user, I18n.T("[]securing...", Dm.truthy(anchored) ? I18n.T("un") : ""));
        tool.play_tool_sound(this);
        if (Dm.truthy(await tool.use_tool(this, user, 6 * SECONDS)))
        {
            playsound(loc, Dm.resource("sound/items/deconstruct.ogg"), 50, vary: TRUE);
            balloon_alert(user, I18n.T("[]secured", Dm.truthy(anchored) ? I18n.T("un") : ""));
            await deconstruct();
            return TRUE;
        }
        return dot;
    }

    // tg: code/game/machinery/digital_clock.dm:34
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.combat_mode))
            return null;
        if (Dm.num(atom_integrity) >= max_integrity)
        {
            balloon_alert(user, I18n.T("it doesn't need repairs!"));
            return TRUE;
        }
        balloon_alert(user, I18n.T("repairing display..."));
        if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, amount: 0, volume: 50)))
            return TRUE;
        balloon_alert(user, I18n.T("repaired"));
        atom_integrity = max_integrity;
        await set_machine_stat(Dm.bitand(machine_stat, Dm.bitnot(BROKEN)));
        update_appearance();
        return TRUE;
    }

    // tg: code/game/machinery/digital_clock.dm:48
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.combat_mode))
            return null;
        if (!Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return null;
        balloon_alert(user, I18n.T("resetting..."));
        tool.play_tool_sound(this);
        if (Dm.truthy(await tool.use_tool(this, user, 6 * SECONDS)))
        {
            playsound(loc, Dm.resource("sound/items/deconstruct.ogg"), 50, vary: TRUE);
            balloon_alert(user, I18n.T("reset"));
            obj_flags = Dm.bitand(obj_flags, Dm.bitnot(EMAGGED));
            return TRUE;
        }
        return null;
    }

    // tg: code/game/machinery/digital_clock.dm:61
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        playsound(this, SFX_SPARKS, 100, vary: TRUE, extrarange: SHORT_RANGE_SOUND_EXTRARANGE);
        do_sparks(3, cardinal_only: FALSE, source: this);
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        return TRUE;
    }

    // tg: code/game/machinery/digital_clock.dm:69
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        await emag_act();
        return dot;
    }

    // tg: code/game/machinery/digital_clock.dm:73
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        if (Dm.truthy(disassembled))
            Dm.@new<obj_item_wallframe_digital_clock>(drop_location());
        else
        {
            Dm.@new<obj_item_stack_sheet_iron>(drop_location(), 2);
            Dm.@new<obj_item_shard>(drop_location());
            Dm.@new<obj_item_shard>(drop_location());
        }
        return null;
    }

    // tg: code/game/machinery/digital_clock.dm:81
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload))
            Waiting.NoSleep(find_and_mount_on_atom());
        Waiting.NoSleep(AddElement(typeof(datum_element_beauty), 200));
        return dot;
    }

    // tg: code/game/machinery/digital_clock.dm:87
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
            return null;
        update_appearance();
        return null;
    }

    // tg: code/game/machinery/digital_clock.dm:93
    public override object update_appearance(float? updates = null)
    {
        updates ??= ALL;
        object dot = base.update_appearance(updates);
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
        {
            Waiting.NoSleep(set_light(0));
            return dot;
        }
        Waiting.NoSleep(set_light(l_range: 1.5f, l_power: 0.7f, l_color: LIGHT_COLOR_BLUE)); // blue light
        return dot;
    }

    // tg: code/game/machinery/digital_clock.dm:100
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
            return dot;
        Dm.append(ref dot, update_time());
        return dot;
    }

    // tg: code/game/machinery/digital_clock.dm:108
    public virtual DmList update_time()
    {
        float station_minutes;
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            station_minutes = rand(0, 99);
        else
            station_minutes = Dm.num(text2num(round_timestamp(format: "mm")));

        // tenth / the '3' in '31' / 31 -> 3.1 -> 3
        float station_minute_tenth = station_minutes >= 10 ? round(station_minutes * 0.1f) : 0;
        // one / the '1' in '31' / 31 -> 31 - (3 * 10) -> 31 - 30 -> 1
        float station_minute_one = station_minutes - (station_minute_tenth * 10);

        float station_hours;

        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            station_hours = rand(0, 99);
        else
            station_hours = Dm.num(text2num(round_timestamp(format: "hh")));

        // one / the '1' in '12' / 12 -> 1.2 -> 1
        // (tg tests station_minutes here, not station_hours: an hour of 10 or more reads as 0x in the first ten minutes of the
        // hour, tg's own bug kept as is)
        float station_hours_tenth = station_minutes >= 10 ? round(station_hours * 0.1f) : 0;
        // tenth / the '2' in '12' / 12 -> 12 - (1 * 10) -> 12 - 10 -> 2
        float station_hours_one = station_hours - (station_hours_tenth * 10);

        DmList return_overlays = Dm.list();

        var minute_one_overlay = mutable_appearance(Dm.resource("icons/obj/digital_clock.dmi"), Dm.text("+[]", station_minute_one)) as mutable_appearance;
        minute_one_overlay.pixel_w = 0;
        Dm.append(ref return_overlays, minute_one_overlay);

        var minute_tenth_overlay = mutable_appearance(Dm.resource("icons/obj/digital_clock.dmi"), Dm.text("+[]", station_minute_tenth)) as mutable_appearance;
        minute_tenth_overlay.pixel_w = -4;
        Dm.append(ref return_overlays, minute_tenth_overlay);

        var separator = mutable_appearance(Dm.resource("icons/obj/digital_clock.dmi"), "+separator") as mutable_appearance;
        Dm.append(ref return_overlays, separator);

        var hour_one_overlay = mutable_appearance(Dm.resource("icons/obj/digital_clock.dmi"), Dm.text("+[]", station_hours_one)) as mutable_appearance;
        hour_one_overlay.pixel_w = -10;
        Dm.append(ref return_overlays, hour_one_overlay);

        var hour_tenth_overlay = mutable_appearance(Dm.resource("icons/obj/digital_clock.dmi"), Dm.text("+[]", station_hours_tenth)) as mutable_appearance;
        hour_tenth_overlay.pixel_w = -14;
        Dm.append(ref return_overlays, hour_tenth_overlay);

        return return_overlays;
    }
}
