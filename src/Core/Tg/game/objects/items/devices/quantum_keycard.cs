// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/quantum_keycard.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DV2 (D333): the file whole. The quantum keycard: linked by a quantum pad (quantum_pad.dm, not ported yet: not-translated.md
// 216), it takes the colour of the department the pad stands in (its greyscale) and tells where the pad is; Alt-click holds its
// unlink button for four seconds. The var block is generated (D111).

// tg: code/game/objects/items/devices/quantum_keycard.dm:1
public partial class obj_item_quantum_keycard : obj_item
{
    /// where the pad is located and what color the card will become
    // tg: code/game/objects/items/devices/quantum_keycard.dm:20
    public static DmList gags_coloring => TgState.Current.obj_item_quantum_keycard__gags_coloring ??= Dm.list(
        Dm.kv(typeof(area_station_maintenance), COLOR_ASSISTANT_GRAY),
        Dm.kv(typeof(area_station_security), COLOR_SECURITY_RED),
        Dm.kv(typeof(area_station_service), COLOR_SERVICE_LIME),
        Dm.kv(typeof(area_centcom), COLOR_CENTCOM_BLUE),  // how?
        Dm.kv(typeof(area_station_command), COLOR_COMMAND_BLUE),
        Dm.kv(typeof(area_station_ai), COLOR_COMMAND_BLUE),
        Dm.kv(typeof(area_station_medical), COLOR_MEDICAL_BLUE),
        Dm.kv(typeof(area_station_science), COLOR_SCIENCE_PINK),
        Dm.kv(typeof(area_station_engineering), COLOR_ENGINEERING_ORANGE),
        Dm.kv(typeof(area_station_cargo), COLOR_CARGO_BROWN),
        Dm.kv(typeof(area_mine), COLOR_CARGO_BROWN));

    // tg: code/game/objects/items/devices/quantum_keycard.dm:34
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(qpad))
        {
            Dm.append(ref dot, I18n.T("It's currently linked to a quantum pad."));

            object area_name = get_area_name(qpad);
            if (Dm.truthy(area_name))
                Dm.append(ref dot, span_notice(I18n.T("The pad is located in \\the []", area_name)));

            Dm.append(ref dot, span_notice(I18n.T("Alt-click to unlink the keycard.")));
        }
        else
            Dm.append(ref dot, span_notice(I18n.T("Insert [] into an active quantum pad to link it.", this)));
        return dot;
    }

    // tg: code/game/objects/items/devices/quantum_keycard.dm:47
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        to_chat(user, span_notice(I18n.T("You start pressing []'s unlink button...", this)));
        if (!Dm.truthy(await do_after(user, 4 * SECONDS, target: this)))
            return CLICK_ACTION_BLOCKING;
        to_chat(user, span_notice(I18n.T("The keycard beeps twice and disconnects the quantum link.")));
        await set_pad();
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/devices/quantum_keycard.dm:55
    public virtual async DmTask set_pad(obj_machinery_quantumpad new_pad = null)
    {
        qpad = new_pad;

        if (new_pad is not obj_machinery_quantumpad)
        {
            await set_greyscale(Dm.initial(this, (obj_item_quantum_keycard p) => p.greyscale_colors));
            name = Dm.initial(this, (obj_item_quantum_keycard p) => p.name);
            return;
        }

        object or__1 = is_type_in_list(get_area(new_pad) as datum, gags_coloring, zebra: TRUE);
        object new_color = Dm.truthy(or__1) ? or__1 : COLOR_WEBSAFE_DARK_GRAY;
        await set_greyscale(new_color);
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/devices/quantum_keycard.dm:20
    internal DmList obj_item_quantum_keycard__gags_coloring;
}
