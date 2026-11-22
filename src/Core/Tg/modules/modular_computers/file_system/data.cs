// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/data.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M9 slice E (the notepad's files): the data files and the text files (data.dm:1-35). The ordnance data (the doppler array and the
// tank compressor, research) and the paint projects (the NanoPaint app, the canvases) are their apps' (generated-only). The var
// blocks are generated (D111).

// tg: code/modules/modular_computers/file_system/data.dm:2  #define BLOCK_SIZE 250 (file-local)
/**
 * Holds data in string format.
 */
// tg: code/modules/modular_computers/file_system/data.dm:15
public partial class datum_computer_file_data_text : datum_computer_file_data
{
    private const float BLOCK_SIZE = 250;

    // tg: code/modules/modular_computers/file_system/data.dm:20
    public override datum_computer_file clone(object rename = null)
    {
        datum_computer_file_data_text temp = base.clone(rename) as datum_computer_file_data_text;
        temp.stored_text = stored_text;
        return temp;
    }

    // Calculates file size from amount of characters in saved string
    // tg: code/modules/modular_computers/file_system/data.dm:26
    public virtual void calculate_size()
    {
        size = Dm.max(1, Dm.round(Dm.length(stored_text) / BLOCK_SIZE));
    }
}
