// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/signs/signs_misc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (the tram port, 2026-11-12): the tram information plate (signs_misc.dm:31-111) but add_context (the screentips stay
// deferred, live-partial.txt). The other signs of the file are var-only.

// Tram-mounted statistics plate
// tg: code/game/objects/structures/signs/signs_misc.dm:31
public partial class obj_structure_sign_tram_plate : obj_structure_sign
{
    // tg: code/game/objects/structures/signs/signs_misc.dm:45
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/game/objects/structures/signs/signs_misc.dm:49
    public override void LateInitialize(params object[] __extra)
    {
        link_tram();
        set_tram_serial();
    }

    // tg: code/game/objects/structures/signs/signs_misc.dm:60
    public virtual void link_tram()
    {
        foreach (datum_transport_controller_linear_tram tram in Dm.each<datum_transport_controller_linear_tram>(SStransport.transports_by_type[TRANSPORT_TYPE_TRAM]))
        {
            if (Dm.eq(tram.specific_transport_id, specific_transport_id))
            {
                transport_ref = WEAKREF(tram) as datum_weakref;
                break;
            }
        }
    }

    // tg: code/game/objects/structures/signs/signs_misc.dm:66
    public virtual void set_tram_serial()
    {
        datum_transport_controller_linear_tram tram = transport_ref?.resolve() as datum_transport_controller_linear_tram;
        if (tram == null || tram.tram_registration == null)
            return;

        tram_serial = tram.tram_registration.serial_number;
        desc = I18n.T("A plate showing details from the manufacturer about this Nakamura Engineering SkyyTram Mk VI, serial number [].<br><br>We are not responsible for any injuries or fatalities caused by usage of the tram. Using the tram carries inherent risks, and we cannot guarantee the safety of all passengers. By using the tram, you assume, acknowledge, and accept all the risks and responsibilities. <br><br>Please be aware that riding the tram can cause a variety of injuries, including but not limited to: slips, trips, and falls; collisions with other passengers or objects; strains, sprains, and other musculoskeletal injuries; cuts, bruises, and lacerations; and more severe injuries such as head trauma, spinal cord injuries, and even death. These injuries can be caused by a variety of factors, including the movements of the tram, the behaviour of other passengers, and unforeseen circumstances such as foul play or mechanical issues.<br><br>By entering the tram, guideway, or crossings you agree Nanotrasen is not liable for any injuries, damages, or losses that may occur. If you do not agree to these terms, please do not use the tram.<br>", tram_serial);
    }

    // tg: code/game/objects/structures/signs/signs_misc.dm:79
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "TramPlaque");
            ui.autoupdate = FALSE;
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/structures/signs/signs_misc.dm:86
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        datum_transport_controller_linear_tram tram = transport_ref?.resolve() as datum_transport_controller_linear_tram;
        DmList data = Dm.list();
        DmList current_tram = Dm.list();
        DmList previous_trams = Dm.list();

        Dm.append(ref current_tram, Dm.list(Dm.list(
            Dm.kv("serialNumber", tram.tram_registration.serial_number),
            Dm.kv("mfgDate", tram.tram_registration.mfg_date),
            Dm.kv("distanceTravelled", tram.tram_registration.distance_travelled),
            Dm.kv("tramCollisions", tram.tram_registration.collisions))));

        foreach (datum_tram_mfg_info previous_tram in Dm.each<datum_tram_mfg_info>(tram.tram_history))
        {
            Dm.append(ref previous_trams, Dm.list(Dm.list(
                Dm.kv("serialNumber", previous_tram.serial_number),
                Dm.kv("mfgDate", previous_tram.mfg_date),
                Dm.kv("distanceTravelled", previous_tram.distance_travelled),
                Dm.kv("tramCollisions", previous_tram.collisions))));
        }

        data["currentTram"] = current_tram;
        data["previousTrams"] = previous_trams;
        return data;
    }
}
