// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/persistence/counter_delamination.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file.
public partial class datum_controller_subsystem_persistence
{
    /// Location where we save the information about how many rounds it has been since the engine blew up
    // tg: code/controllers/subsystem/persistence/counter_delamination.dm:2-3  #define DELAMINATION_COUNT_FILEPATH, DELAMINATION_HIGHSCORE_FILEPATH
    // DEVIATION(DV220): in data/persistence/ (tg: data/), the folder the server keeps on disk (PERSISTENCE_SAVES).
    private const string DELAMINATION_COUNT_FILEPATH = "data/persistence/rounds_since_delamination.txt";
    private const string DELAMINATION_HIGHSCORE_FILEPATH = "data/persistence/delamination_highscore.txt";

    // tg: code/controllers/subsystem/persistence/counter_delamination.dm:5
    public virtual void load_delamination_counter()
    {
        if (!Dm.truthy(fexists(DELAMINATION_COUNT_FILEPATH)))
            return;
        rounds_since_engine_exploded = Dm.num(text2num(file2text(DELAMINATION_COUNT_FILEPATH)));
        if (Dm.truthy(fexists(DELAMINATION_HIGHSCORE_FILEPATH)))
            delam_highscore = Dm.num(text2num(file2text(DELAMINATION_HIGHSCORE_FILEPATH)));
        foreach (obj_machinery_incident_display sign in Dm.each_cast<obj_machinery_incident_display>(GLOB.map_incident_displays))
            sign.update_delam_count(rounds_since_engine_exploded, delam_highscore);
    }

    // tg: code/controllers/subsystem/persistence/counter_delamination.dm:14
    public virtual void save_delamination_counter()
    {
        rustg_file_write(Dm.text("[]", rounds_since_engine_exploded + 1), DELAMINATION_COUNT_FILEPATH);
        if ((rounds_since_engine_exploded + 1) > delam_highscore)
            rustg_file_write(Dm.text("[]", rounds_since_engine_exploded + 1), DELAMINATION_HIGHSCORE_FILEPATH);
    }
}
