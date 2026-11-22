// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/persistence/counter_tram_hits.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The tram port (2026-11-12): the whole file.
public partial class datum_controller_subsystem_persistence
{
    /// Location where we save the information about how many times the tram hit on previous round
    // tg: code/controllers/subsystem/persistence/counter_tram_hits.dm:2  #define TRAM_COUNT_FILEPATH
    // DEVIATION(DV220): in data/persistence/ (tg: data/), the folder the server keeps on disk (PERSISTENCE_SAVES).
    private const string TRAM_COUNT_FILEPATH = "data/persistence/tram_hits_last_round.txt";

    // tg: code/controllers/subsystem/persistence/counter_tram_hits.dm:3
    private const int MAX_TRAM_SAVES = 4;

    // DV220: the tram histories' folder (tg: data/tram_data/).
    private static string tram_data_path(object specific_transport_id) => Dm.text("data/persistence/tram_data/[].json", specific_transport_id);

    // Loads historical tram data
    // tg: code/controllers/subsystem/persistence/counter_tram_hits.dm:6
    public virtual object load_tram_history(object specific_transport_id = null)
    {
        DmList raw_saved_trams = Dm.list();
        object json_file = file(tram_data_path(specific_transport_id));
        if (!Dm.truthy(fexists(json_file)))
            return null;
        DmList json = json_decode(file2text(json_file)) as DmList;
        if (json == null)
            return null;
        raw_saved_trams = json["data"] as DmList;

        DmList previous_tram_data = Dm.list();
        foreach (object raw_json in Dm.each(raw_saved_trams))
        {
            datum_tram_mfg_info parsed_tram_data = Dm.@new<datum_tram_mfg_info>();
            parsed_tram_data.load_from_json(raw_json as DmList);
            Dm.append(ref previous_tram_data, parsed_tram_data);
        }
        return previous_tram_data;
    }

    // Saves historical tram data
    // tg: code/controllers/subsystem/persistence/counter_tram_hits.dm:24
    public virtual void save_tram_history(object specific_transport_id = null)
    {
        DmList packaged_tram_data = Dm.list();
        foreach (datum_transport_controller_linear_tram transport in Dm.each<datum_transport_controller_linear_tram>(SStransport.transports_by_type[TRANSPORT_TYPE_TRAM]))
        {
            if (Dm.eq(transport.specific_transport_id, specific_transport_id))
            {
                packaged_tram_data = package_tram_data(transport) as DmList;
                break;
            }
        }

        object json_file = file(tram_data_path(specific_transport_id));
        DmList file_data = Dm.list();
        DmList converted_data = Dm.list();

        foreach (datum_tram_mfg_info data in Dm.each<datum_tram_mfg_info>(packaged_tram_data))
            Dm.append(ref converted_data, Dm.list(data.export_to_json()));

        file_data["data"] = converted_data;
        fdel(json_file);
        text2file(json_encode(file_data), json_file); // WRITE_FILE(json_file, ...) onto the deleted file
    }

    // tg: code/controllers/subsystem/persistence/counter_tram_hits.dm:43
    public virtual object package_tram_data(datum_transport_controller_linear_tram tram_controller = null)
    {
        DmList packaged_data = Dm.list();
        DmList tram_list = tram_controller.tram_history;
        if (tram_list != null)
        {
            while (tram_list.Count > MAX_TRAM_SAVES)
                tram_list.Cut(1, 2);

            foreach (datum_tram_mfg_info data in Dm.each<datum_tram_mfg_info>(tram_list))
                Dm.append(ref packaged_data, data);
        }

        Dm.append(ref packaged_data, tram_controller.tram_registration);
        return packaged_data;
    }

    // tg: code/controllers/subsystem/persistence/counter_tram_hits.dm:55
    public virtual void load_tram_counter()
    {
        if (!Dm.truthy(fexists(TRAM_COUNT_FILEPATH)))
            return;
        tram_hits_last_round = Dm.num(text2num(file2text(TRAM_COUNT_FILEPATH)));
    }

    // tg: code/controllers/subsystem/persistence/counter_tram_hits.dm:60
    public virtual void save_tram_counter()
    {
        rustg_file_write(Dm.text("[]", tram_hits_this_round), TRAM_COUNT_FILEPATH);
    }
}
