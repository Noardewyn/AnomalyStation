// Translated from tgstation (snapshot 1d99bd7f): code/datums/materials/properties/derived.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 2 (materials and stacks): the whole file. The var blocks are generated (D111).

/// Derived properties aren't present on materials, and are instead just centralized holders for formulas
// tg: code/datums/materials/properties/derived.dm:2
public partial class datum_material_property_derived : datum_material_property
{
    /// Does this property apply to our material?
    // tg: code/datums/materials/properties/derived.dm:7
    public virtual object is_present(datum_material material = null)
    {
        return TRUE;
    }

    /// Calculate and fetch the value of the property on this material
    // tg: code/datums/materials/properties/derived.dm:11
    public virtual object get_value(datum_material material = null)
    {
        return 0;
    }
}

/// Base atom integrity multiplier of items made from this material
// tg: code/datums/materials/properties/derived.dm:19
public partial class datum_material_property_derived_integrity : datum_material_property_derived
{
    // tg: code/datums/materials/properties/derived.dm:14  #define INTEGRITY_MIN 0.1 (a file-local define, rules 11.4)
    private const float INTEGRITY_MIN = 0.1f;
    // This results in iron being almost exactly 1
    // tg: code/datums/materials/properties/derived.dm:16  #define INTEGRITY_COEFF 1.57
    private const float INTEGRITY_COEFF = 1.57f;

    // tg: code/datums/materials/properties/derived.dm:22
    public override object get_value(datum_material material = null)
    {
        // Integrity is a combination of density, hardness and flexibility
        object density = material.get_property(MATERIAL_DENSITY);
        object hardness = material.get_property(MATERIAL_HARDNESS);
        object flexibility = material.get_property(MATERIAL_FLEXIBILITY);
        // Its primarily hardness - the harder a material is, the more it is resistant to direct impacts
        // But unless it has enough bend to it, it'll also fracture - which is why flexibility needs to be in a sweetspot, based on density
        float hardness_coeff = (2 + max(0, Dm.num(hardness) - 4) * 2 - max(0, 2 - Dm.num(hardness))) / MATERIAL_PROPERTY_MAX;
        float bend_coeff = 1 - abs(Dm.num(flexibility) - sqrt(Dm.num(density))) * 0.1f;
        // Check the math for yourself in https://www.desmos.com/calculator/ez2n34w772
        return round(INTEGRITY_MIN + hardness_coeff * bend_coeff * INTEGRITY_COEFF, 0.01f);
    }
}

/// How pretty a material is
// tg: code/datums/materials/properties/derived.dm:38
public partial class datum_material_property_derived_beauty : datum_material_property_derived
{
    // tg: code/datums/materials/properties/derived.dm:41
    public override object is_present(datum_material material = null)
    {
        return Dm.num(abs(Dm.num(material.get_property(MATERIAL_REFLECTIVITY))) > MATERIAL_PROPERTY_MAX / 2f);
    }

    // tg: code/datums/materials/properties/derived.dm:44
    public override object get_value(datum_material material = null)
    {
        // Requires the material to be especially shiny or dull
        object reflectivity = material.get_property(MATERIAL_REFLECTIVITY);
        return MATERIAL_PROPERTY_DIVERGENCE(reflectivity, 3, 6) * 0.05f;
    }
}

/// Siemens coeff multiplier for our material
// tg: code/datums/materials/properties/derived.dm:50
public partial class datum_material_property_derived_insulation : datum_material_property_derived
{
    // tg: code/datums/materials/properties/derived.dm:53
    public override object get_value(datum_material material = null)
    {
        // [0 ~ 1] is fully insulating, (1 ~ 6] maps to (0 ~ 1] and [6 ~ 10] maps to [1 ~ 2]
        // 1.18 and 0.15 here are to allow 6 to map to 1 and 10 to map to 2 and are pulled out of my ass (system in the desmos below)
        // See https://www.desmos.com/calculator/rdbv1x8oty
        object conductivity = material.get_property(MATERIAL_ELECTRICAL);
        return round(Dm.pow(max(0, Dm.num(conductivity) - 1), 1.18f) * 0.15f, 0.01f);
    }
}
