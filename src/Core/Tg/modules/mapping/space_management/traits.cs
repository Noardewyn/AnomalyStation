// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapping/space_management/traits.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M3 P2: the whole file. level_trait's DEFAULT_MAP_TRAITS branch (no z_list) keeps the M2 behaviour for the host-built
// rooms, see there.

// tg: code/modules/mapping/space_management/traits.dm:2
public partial class datum_controller_subsystem_mapping : datum_controller_subsystem
{
    /// Look up levels[z].traits[trait]
    // tg: code/modules/mapping/space_management/traits.dm:2
    public virtual object level_trait(object z = null, object trait = null)
    {
        if (!isnum(z) || Dm.lt(z, 1))
            return null;
        if (Dm.truthy(z_list))
        {
            if (Dm.gt(z, z_list.len))
            {
                stack_trace(Dm.text("Unmanaged z-level []! maxz = [], z_list.len = []", z, Dm.world.maxz, z_list.len), "code/modules/mapping/space_management/traits.dm", 7);
                return Dm.list();
            }
            var S = (datum_space_level)z_list[z];
            return S.traits[trait];
        }
        else
        {
            // PORT: tg reads DEFAULT_MAP_TRAITS here (the compiled-in CentCom level, _maps/_basemap.dm) until SSmapping's
            // InitializeDefaultZLevels has run. A world without a selected map (MapSeam) has no compiled-in CentCom: its
            // levels are the host-built test and toy rooms (TestRoom, ToyRoom), which SSmapping never manages, and they
            // have no trait (as in M2), so no room is a CentCom, secret or away level.
            Dm.Deferred("code/modules/mapping/space_management/traits.dm:5", "level_trait: a world without SSmapping's z-levels (the host-built rooms) reads no DEFAULT_MAP_TRAITS: its levels have no trait (the compiled-in CentCom level is not loaded, D108)");
            return null;
        }
    }
}

// tg: code/modules/mapping/space_management/traits.dm:19
public partial class datum_controller_subsystem_mapping : datum_controller_subsystem
{
    /// Check if levels[z] has any of the specified traits
    // tg: code/modules/mapping/space_management/traits.dm:19
    public virtual object level_has_any_trait(object z = null, DmList traits = null)
    {
        var level_to_check = (datum_space_level)z_list[z];
        if (Dm.truthy(length(Dm.bitand(level_to_check.traits, traits))))
            return TRUE;
        return FALSE;
    }

    /// Check if levels[z] has all of the specified traits
    // tg: code/modules/mapping/space_management/traits.dm:26
    public virtual object level_has_all_traits(object z = null, DmList traits = null)
    {
        var level_to_check = (datum_space_level)z_list[z];
        if (Dm.eq(length(Dm.bitand(level_to_check.traits, traits)), length(traits)))
            return TRUE;
        return FALSE;
    }

    /// Get a list of all z which have the specified trait
    // tg: code/modules/mapping/space_management/traits.dm:33
    public virtual object levels_by_trait(object trait = null)
    {
        return z_trait_levels[trait] ?? Dm.list();
    }

    /// Get a list of all z which have any of the specified traits
    // tg: code/modules/mapping/space_management/traits.dm:37
    public virtual object levels_by_any_trait(DmList traits = null)
    {
        DmList final_return = Dm.list();
        foreach (object trait in Dm.each(traits))
        {
            if (Dm.truthy(z_trait_levels[trait]))
                Dm.combine(ref final_return, z_trait_levels[trait]);
        }
        return final_return;
    }

    /// Get a list of all z which have all of the specified traits
    // tg: code/modules/mapping/space_management/traits.dm:45
    public virtual object levels_by_all_traits(DmList traits = null)
    {
        DmList final_return = Dm.list();
        foreach (datum_space_level level in Dm.each<datum_space_level>(z_list))
        {
            if (Dm.truthy(level_has_all_traits(level.z_value, traits)))
                Dm.append(ref final_return, level.z_value);
        }
        return final_return;
    }

    /// Prefer not to use this one too often
    // tg: code/modules/mapping/space_management/traits.dm:53
    public virtual object get_station_center()
    {
        object station_z = ((DmList)levels_by_trait(ZTRAIT_STATION))[1];
        return locate(round(Dm.world.maxx * 0.5f, 1), round(Dm.world.maxy * 0.5f, 1), Dm.num(station_z));
    }
}
