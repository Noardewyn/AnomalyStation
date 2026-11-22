// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/records.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4a, D327): the Medical and Security Records apps, a read-only list of the crew's records in the NtosRecords
// window. The var blocks are generated (D111).

// tg: code/modules/modular_computers/file_system/programs/records.dm:1
public partial class datum_computer_file_program_records : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/records.dm:34
    public virtual DmList GetRecordsReadable()
    {
        DmList all_records = Dm.list();

        switch (mode)
        {
            case "security":
                foreach (datum_record_crew person in Dm.each<datum_record_crew>(GLOB.manifest.general))
                {
                    DmList current_record = Dm.list();

                    current_record["age"] = person.age;
                    current_record["fingerprint"] = person.fingerprint;
                    current_record["gender"] = person.gender;
                    current_record["name"] = person.name;
                    current_record["rank"] = person.rank;
                    current_record["species"] = person.species;
                    current_record["wanted"] = person.wanted_status;
                    current_record["voice"] = person.voice;

                    all_records.Add(Dm.list(current_record));
                }
                break;
            case "medical":
                foreach (datum_record_crew person in Dm.each<datum_record_crew>(GLOB.manifest.general))
                {
                    DmList current_record = Dm.list();

                    current_record["bloodtype"] = person.blood_type;
                    current_record["ma_dis"] = person.major_disabilities_desc;
                    current_record["mi_dis"] = person.minor_disabilities_desc;
                    current_record["physical_status"] = person.physical_status;
                    current_record["mental_status"] = person.mental_status;
                    current_record["name"] = person.name;
                    DmList notes = Dm.list();
                    foreach (datum_medical_note note in Dm.each<datum_medical_note>(person.medical_notes))
                        notes.Add(note.content);
                    current_record["notes"] = notes.Join(" ");

                    all_records.Add(Dm.list(current_record));
                }
                break;
        }

        return all_records;
    }

    // tg: code/modules/modular_computers/file_system/programs/records.dm:71
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["records"] = GetRecordsReadable();
        data["mode"] = mode;
        return data;
    }
}
