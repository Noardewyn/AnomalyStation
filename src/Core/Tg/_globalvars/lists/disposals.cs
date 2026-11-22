// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/disposals.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E16: the file (the conveyor sort-router's "input is tagged" filter reads the destinations).

// tg: code/_globalvars/lists/disposals.dm:45
public partial class datum_controller_global_vars : datum_controller
{
    //disposals must always be 1, since anything that's untagged will automatically go to disposals, or sorttype = 1 --Superxpdude

    //If you don't want to fuck up disposals, add to this list, and don't change the order.
    //If you insist on changing the order, you'll have to change every sort junction to reflect the new order. --Pete

    // tg: code/_globalvars/lists/disposals.dm:45
    public DmList TAGGERLOCATIONS;

    // tg: code/_globalvars/lists/disposals.dm:45  (GLOBAL_MANAGED(TAGGERLOCATIONS, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalTAGGERLOCATIONS()
    {
        TAGGERLOCATIONS = Dm.list("Disposals",
            "Cargo Bay", "QM Office", "Engineering", "CE Office",
            "Atmospherics", "Security", "HoS Office", "Medbay",
            "CMO Office", "Chemistry", "Research", "RD Office",
            "Robotics", "HoP Office", "Library", "Chapel", "Theatre",
            "Bar", "Kitchen", "Hydroponics", "Janitor Closet", "Genetics",
            "Experimentor Lab", "Ordnance", "Dormitories", "Virology",
            "Xenobiology", "Law Office", "Detective's Office");
        Dm.append(ref gvars_datum_init_order, "TAGGERLOCATIONS");
    }
}
