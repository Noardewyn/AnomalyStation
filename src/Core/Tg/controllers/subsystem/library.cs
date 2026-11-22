// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/library.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps ST2 (D333): the whole file (the library's subsystem: it loads the mapped bookcases' shelves at its init, keeps the books of each
// area for the library consoles, the printable official posters and the map's library areas). The var block is generated (D111).

/// Manages library data, loading bookselves, etc
// tg: code/controllers/subsystem/library.dm:2  SUBSYSTEM_DEF(library)
public partial class datum_controller_subsystem_library : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(library) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSlibrary) (__DEFINES/MC.dm:29) expanded inline
    // tg: code/controllers/subsystem/library.dm:2
    public override async DmTask New(params object[] __extra)
    {
        if (SSlibrary != this)
        {
            if (SSlibrary is datum_controller_subsystem_library)
            {
                Recover();
                qdel(SSlibrary);
            }
            SSlibrary = this;
        }
        PreInit();
        await DmTask.CompletedTask;
    }

    // tg: code/controllers/subsystem/library.dm:25
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        prepare_official_posters();
        prepare_library_areas();
        await load_shelves();
        return SS_INIT_SUCCESS;
    }

    // tg: code/controllers/subsystem/library.dm:31
    public virtual async DmTask load_shelves()
    {
        DmList load_callbacks = Dm.list();

        foreach (object case_to_load in Dm.each(shelves_to_load))
        {
            if (!Dm.truthy(case_to_load))
            {
                stack_trace("A null bookcase somehow ended up in SSlibrary's shelves_to_load list. Did something harddel?", "code/controllers/subsystem/library.dm", 36);
                continue;
            }
            Dm.append(ref load_callbacks, Dm.@new<datum_callback>(case_to_load, nameof(obj_structure_bookcase.load_shelf)));
        }
        shelves_to_load = null;

        //Load all of the shelves asyncronously at the same time, blocking until the last one is finished.
        await callback_select(load_callbacks, savereturns: FALSE);
    }

    /// Returns a list of copied book datums that we consider to be "in" the passed in area at roundstart
    // tg: code/controllers/subsystem/library.dm:45
    public virtual object get_area_books(area book_parent = null)
    {
        DmList areas = Dm.list(book_parent.type);
        // If we have an area that's in the global libraries list, we want all the others too
        if (Dm.truthy(length(Dm.bitand(areas, library_areas))))
            Dm.combine(ref areas, library_areas);

        DmList books = Dm.list();
        foreach (object area_type in Dm.each(areas))
        {
            foreach (datum_book_info info in Dm.each<datum_book_info>(books_by_area[area_type]))
                Dm.append(ref books, info.return_copy());
        }

        return books;
    }

    // tg: code/controllers/subsystem/library.dm:58
    public virtual void prepare_official_posters()
    {
        printable_posters = Dm.list();
        foreach (Type poster_type in Dm.each_cast<Type>(subtypesof(typeof(obj_structure_sign_poster_official))))
        {
            if (Dm.eq(Dm.initial(poster_type, (obj_structure_sign_poster_official p) => p.pixel_x), 0) && Dm.eq(Dm.initial(poster_type, (obj_structure_sign_poster_official p) => p.pixel_y), 0)) //Anything with an initial pixel offset is a directional subtype, keep it out the list
                printable_posters[Dm.initial(poster_type, (obj_structure_sign_poster_official p) => p.name)] = poster_type;
        }
    }

    // tg: code/controllers/subsystem/library.dm:64
    public virtual void prepare_library_areas()
    {
        library_areas = Dm.sub(typesof(typeof(area_station_service_library)), typeof(area_station_service_library_abandoned)) as DmList;
        // `?.`: a world booted from a host-built room has no map config (as roundend.dm's map name); DM reads null off a null there too
        DmList additional_areas = SSmapping.current_map?.library_areas;
        if (additional_areas != null)
            Dm.append(ref library_areas, additional_areas);
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/library.dm:2  SUBSYSTEM_DEF(library): GLOBAL_REAL(SSlibrary, /datum/controller/subsystem/library)
    internal datum_controller_subsystem_library SSlibrary;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_library SSlibrary => ref TgState.Current.SSlibrary;
}
