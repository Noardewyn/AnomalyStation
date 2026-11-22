// Translated from tgstation (snapshot 1d99bd7f): code/datums/materials/requirements/_requirement.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 2 (materials and stacks): the whole file; get_description, the lathes' text for a design's material requirement, since gaps
// RD1 (D333: the autolathe). The var blocks are generated (D111).

/// A datum holding a set of flags or minimum/maximum properties to decide which materials can be used in a design
// tg: code/datums/materials/requirements/_requirement.dm:2
public partial class datum_material_requirement : datum
{
    /// Returns a string description of materials that'd fit this requirement
    // tg: code/datums/materials/requirements/_requirement.dm:14
    public virtual object get_description()
    {
        DmList flag_strings = Dm.list();
        foreach (object flag in Dm.each(bitfield_to_list(required_flags)))
            Dm.append(ref flag_strings, GLOB.material_flags_to_string[flag]);
        foreach (object flag in Dm.each(bitfield_to_list(blacklisted_flags)))
            Dm.append(ref flag_strings, Dm.text("not []", GLOB.material_flags_to_string[flag]));
        if (!Dm.truthy(length(property_minimums)) && !Dm.truthy(length(property_maximums)))
            return Dm.text("[] material", capitalize(english_list(flag_strings, and_text: " or ")));

        DmList prop_reqs = Dm.list();
        foreach (object prop_id in Dm.each(Dm.bitor(property_minimums ?? Dm.list(), property_maximums ?? Dm.list())))
        {
            datum_material_property property = SSmaterials.properties[prop_id] as datum_material_property;
            if (property_minimums != null && property_maximums != null && property_minimums[prop_id] != null && property_maximums[prop_id] != null)
                Dm.append(ref prop_reqs, Dm.text("[] between [] and []", LOWER_TEXT(property.name), property_minimums[prop_id], property_maximums[prop_id]));
            else if (property_minimums != null && property_minimums[prop_id] != null)
                Dm.append(ref prop_reqs, Dm.text("[] equal to or above []", LOWER_TEXT(property.name), property_minimums[prop_id]));
            else
                Dm.append(ref prop_reqs, Dm.text("[] equal to or below []", LOWER_TEXT(property.name), property_maximums[prop_id]));
        }

        return Dm.text("[] material with []", capitalize(english_list(flag_strings, and_text: " or ")), english_list(prop_reqs));
    }

    // tg: code/datums/materials/requirements/_requirement.dm:35
    public virtual object valid_material(datum_material material = null)
    {
        if (Dm.truthy(required_flags) && !Dm.truthy(Dm.bitand(material.mat_flags, required_flags)))
            return FALSE;

        if (Dm.truthy(blacklisted_flags) && Dm.truthy(Dm.bitand(material.mat_flags, blacklisted_flags)))
            return FALSE;

        foreach (object prop_id in Dm.each(property_minimums))
        {
            object min_val = property_minimums[prop_id];
            if (Dm.num(material.get_property(prop_id)) < Dm.num(min_val))
                return FALSE;
        }

        foreach (object prop_id in Dm.each(property_maximums))
        {
            object max_val = property_maximums[prop_id];
            if (Dm.num(material.get_property(prop_id)) > Dm.num(max_val))
                return FALSE;
        }

        return TRUE;
    }
}

// Actual requirement datums
// tg: code/datums/materials/requirements/_requirement.dm:53
public partial class datum_material_requirement_armor_material : datum_material_requirement
{
}

// tg: code/datums/materials/requirements/_requirement.dm:56
public partial class datum_material_requirement_solid_material : datum_material_requirement
{
}

// tg: code/datums/materials/requirements/_requirement.dm:59
public partial class datum_material_requirement_rigid_material : datum_material_requirement
{
}
