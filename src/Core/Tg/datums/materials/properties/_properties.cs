// Translated from tgstation (snapshot 1d99bd7f): code/datums/materials/properties/_properties.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 2 (materials and stacks): the whole file. The var blocks are generated (D111).

/// Singleton datums used to hold material property info
// tg: code/datums/materials/properties/_properties.dm:2
public partial class datum_material_property : datum
{
    /// Returns a string to display to sci glasses wearers when the material is examined
    // tg: code/datums/materials/properties/_properties.dm:11
    public virtual object get_descriptor(object value = null)
    {
        return null;
    }

    /// Returns the contents of the tooltip under our descriptor
    // tg: code/datums/materials/properties/_properties.dm:15
    public virtual object get_tooltip(object value = null)
    {
        return I18n.T("[]\\Roman[]", Dm.num(value) < 0 ? "-" : "", round(abs(Dm.num(value)), 1));
    }

    /// Called whenever a material with this property initializes. Mostly used for behavior tracking on optional properties
    // tg: code/datums/materials/properties/_properties.dm:19
    public virtual void attach_to(datum_material material = null)
    {
        return;
    }
}

/// How dense a material is
// tg: code/datums/materials/properties/_properties.dm:23
public partial class datum_material_property_density : datum_material_property
{
    // tg: code/datums/materials/properties/_properties.dm:27
    public override object get_descriptor(object value = null)
    {
        object sw__1 = value;
        if (Dm.in_to(sw__1, 0, 1))
            return I18n.T("extremely light");
        else if (Dm.in_to(sw__1, 1, 2))
            return I18n.T("very light");
        else if (Dm.in_to(sw__1, 2, 3))
            return I18n.T("light");
        else if (Dm.in_to(sw__1, 3, 4))
            return I18n.T("slightly dense");
        else if (Dm.in_to(sw__1, 4, 6))
            return I18n.T("dense");
        else if (Dm.in_to(sw__1, 6, 8))
            return I18n.T("very dense");
        else if (Dm.in_to(sw__1, 8, INFINITY))
            return I18n.T("extremely dense");
        return null;
    }
}

/// How hard to deformation a material is, pierce/slashing impacts
// tg: code/datums/materials/properties/_properties.dm:47
public partial class datum_material_property_hardness : datum_material_property
{
    // tg: code/datums/materials/properties/_properties.dm:51
    public override object get_descriptor(object value = null)
    {
        object sw__1 = value;
        if (Dm.in_to(sw__1, 0, 1))
            return I18n.T("extremely soft");
        else if (Dm.in_to(sw__1, 1, 2))
            return I18n.T("very soft");
        else if (Dm.in_to(sw__1, 2, 3))
            return I18n.T("soft");
        else if (Dm.in_to(sw__1, 3, 4))
            return I18n.T("slightly hard");
        else if (Dm.in_to(sw__1, 4, 6))
            return I18n.T("hard");
        else if (Dm.in_to(sw__1, 6, 8))
            return I18n.T("very hard");
        else if (Dm.in_to(sw__1, 8, INFINITY))
            return I18n.T("extremely hard");
        return null;
    }
}

/// How well a material bends and sustains deformation
// tg: code/datums/materials/properties/_properties.dm:69
public partial class datum_material_property_flexibility : datum_material_property
{
    // tg: code/datums/materials/properties/_properties.dm:73
    public override object get_descriptor(object value = null)
    {
        object sw__1 = value;
        if (Dm.in_to(sw__1, 0, 1))
            return I18n.T("extremely rigid");
        else if (Dm.in_to(sw__1, 1, 2))
            return I18n.T("very rigid");
        else if (Dm.in_to(sw__1, 2, 3))
            return I18n.T("rigid");
        else if (Dm.in_to(sw__1, 3, 4))
            return I18n.T("slightly flexible");
        else if (Dm.in_to(sw__1, 4, 6))
            return I18n.T("flexible");
        else if (Dm.in_to(sw__1, 6, 8))
            return I18n.T("very flexible");
        else if (Dm.in_to(sw__1, 8, INFINITY))
            return I18n.T("extremely flexible");
        return null;
    }
}

/// How shiny a material is
// tg: code/datums/materials/properties/_properties.dm:91
public partial class datum_material_property_reflectivity : datum_material_property
{
    // tg: code/datums/materials/properties/_properties.dm:95
    public override object get_descriptor(object value = null)
    {
        object sw__1 = value;
        if (Dm.in_to(sw__1, 0, 1))
            return I18n.T("extremely dull");
        else if (Dm.in_to(sw__1, 1, 2))
            return I18n.T("very dull");
        else if (Dm.in_to(sw__1, 2, 3))
            return I18n.T("dull");
        else if (Dm.in_to(sw__1, 3, 4))
            return I18n.T("matte");
        else if (Dm.in_to(sw__1, 4, 6))
            return I18n.T("reflective");
        else if (Dm.in_to(sw__1, 6, 8))
            return I18n.T("very reflective");
        else if (Dm.in_to(sw__1, 8, INFINITY))
            return I18n.T("extremely reflective");
        return null;
    }
}

/// How electrically conductive a material is (siemens coeff)
// tg: code/datums/materials/properties/_properties.dm:113
public partial class datum_material_property_electric_conductivity : datum_material_property
{
    // tg: code/datums/materials/properties/_properties.dm:117
    public override object get_descriptor(object value = null)
    {
        object sw__1 = value;
        if (Dm.eq(sw__1, 0))
            return I18n.T("perfectly insulating");
        else if (Dm.in_to(sw__1, 0, 1))
            return I18n.T("highly insulating");
        else if (Dm.in_to(sw__1, 1, 2))
            return I18n.T("insulating");
        else if (Dm.in_to(sw__1, 2, 3))
            return I18n.T("poorly insulating");
        else if (Dm.in_to(sw__1, 3, 4))
            return I18n.T("mildly conductive");
        else if (Dm.in_to(sw__1, 4, 6))
            return I18n.T("conductive");
        else if (Dm.in_to(sw__1, 6, 8))
            return I18n.T("highly conductive");
        else if (Dm.in_to(sw__1, 8, INFINITY))
            return I18n.T("extremely conductive");
        return null;
    }
}

/// How well a material conducts heat
// tg: code/datums/materials/properties/_properties.dm:137
public partial class datum_material_property_thermal_conductivity : datum_material_property
{
    // tg: code/datums/materials/properties/_properties.dm:141
    public override object get_descriptor(object value = null)
    {
        object sw__1 = value;
        if (Dm.in_to(sw__1, 0, 1))
            return I18n.T("extremely temperature-resistant");
        else if (Dm.in_to(sw__1, 1, 2))
            return I18n.T("very temperature-resistant");
        else if (Dm.in_to(sw__1, 2, 3))
            return I18n.T("temperature-resistant");
        else if (Dm.in_to(sw__1, 3, 4))
            return I18n.T("slightly thermally conductive");
        else if (Dm.in_to(sw__1, 4, 6))
            return I18n.T("thermally conductive");
        else if (Dm.in_to(sw__1, 6, 8))
            return I18n.T("very thermally conductive");
        else if (Dm.in_to(sw__1, 8, INFINITY))
            return I18n.T("extremely thermally conductive");
        return null;
    }
}

/// How well the material resists acids and other similar chemicals
// tg: code/datums/materials/properties/_properties.dm:159
public partial class datum_material_property_chemical_resistance : datum_material_property
{
    // tg: code/datums/materials/properties/_properties.dm:163
    public override object get_descriptor(object value = null)
    {
        object sw__1 = value;
        if (Dm.in_to(sw__1, 0, 1))
            return I18n.T("extremely reactive");
        else if (Dm.in_to(sw__1, 1, 2))
            return I18n.T("reactive");
        else if (Dm.in_to(sw__1, 2, 3))
            return I18n.T("slightly reactive");
        else if (Dm.in_to(sw__1, 3, 4))
            return I18n.T("mildly chemically resistant");
        else if (Dm.in_to(sw__1, 4, 6))
            return I18n.T("chemically resistant");
        else if (Dm.in_to(sw__1, 6, 8))
            return I18n.T("highly chemically resistant");
        else if (Dm.in_to(sw__1, 8, INFINITY))
            return I18n.T("extremely chemically resistant");
        return null;
    }
}
