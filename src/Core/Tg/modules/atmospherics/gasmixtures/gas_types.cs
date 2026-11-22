// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/gasmixtures/gas_types.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Translated in M2 by P2 (scope.md 2e "atmosphere": gas_types.dm): the gas metadata list, gas_id2path and the gas
// datums. M4b slice 3 (the gas visuals): generate_gas_overlays and /obj/effect/overlay/gas, the objects GAS_OVERLAYS puts
// into a turf's vis_contents (the replicator sends them as the cell's gas levels, D18).

// tg: code/modules/atmospherics/gasmixtures/gas_types.dm:1 (global procs)
public static partial class GlobalProcs
{
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:1
    public static object meta_gas_list()
    {
        DmList gas_info = Dm.list_n(META_GAS_LENGTH);
        for (int array_idx = 1; array_idx <= gas_info.len; array_idx++)
            gas_info[array_idx] = Dm.list();

        DmList gas_types = subtypesof(typeof(datum_gas));
        if (!(GAS_TYPE_COUNT == length(gas_types)))
            throw Dm.CRASH(assertion_message("code/modules/atmospherics/gasmixtures/gas_types.dm", 7, "GAS_TYPE_COUNT == length(gas_types)", "GAS_TYPE_COUNT != length(subtypesof(gas_types)), if you added new gas please increment GAS_TYPE_COUNT"), "code/modules/atmospherics/gasmixtures/gas_types.dm", 7);
        foreach (object gas_path__it in Dm.each(gas_types))
        {
            Type gas_path = gas_path__it as Type;
            ((DmList)gas_info[META_GAS_SPECIFIC_HEAT])[gas_path] = Dm.initial(gas_path, (datum_gas p) => p.specific_heat);
            ((DmList)gas_info[META_GAS_NAME])[gas_path] = Dm.initial(gas_path, (datum_gas p) => p.name);
            ((DmList)gas_info[META_GAS_MOLES_VISIBLE])[gas_path] = Dm.initial(gas_path, (datum_gas p) => p.moles_visible);
            if (Dm.truthy(((DmList)gas_info[META_GAS_MOLES_VISIBLE])[gas_path]))
            {
                // rules 10.6 (settled at the M2 integration): tg's Master creates every subsystem before GLOB (master.dm:111-128), so SSmapping is never null
                // here; the test harnesses of the port build GLOB first (TgToyRoom, BootPieces, the unit-test harness), so the
                // read is null-conditional (no offset) instead of a runtime error there. A booted world reads the real value.
                Dm.append_at((DmList)gas_info[META_GAS_OVERLAY], gas_path, generate_gas_overlays(0, SSmapping?.max_plane_offset ?? 0, gas_path));
            }
            ((DmList)gas_info[META_GAS_FUSION_POWER])[gas_path] = Dm.initial(gas_path, (datum_gas p) => p.fusion_power);
            ((DmList)gas_info[META_GAS_DANGER])[gas_path] = Dm.bitand(Dm.initial(gas_path, (datum_gas p) => p.cargo_flags), GAS_DANGEROUS);
            ((DmList)gas_info[META_GAS_ID])[gas_path] = Dm.initial(gas_path, (datum_gas p) => p.id);
            ((DmList)gas_info[META_GAS_DESC])[gas_path] = Dm.initial(gas_path, (datum_gas p) => p.desc);
        }

        datum_gas_mixture.gas_meta = gas_info; // save the reference to the list
        return gas_info;
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:23
    public static object generate_gas_overlays(object old_offset = null, object new_offset = null, Type gas_type = null)
    {
        DmList to_return = Dm.list();
        for (float i = Dm.num(old_offset); i <= Dm.num(new_offset); i++)
        {
            DmList fill = Dm.list();
            Dm.append(to_return, Dm.list(fill));
            for (int j = 1; j <= TOTAL_VISIBLE_STATES; j++)
            {
                obj_effect_overlay_gas gas = Dm.@new<obj_effect_overlay_gas>(Dm.initial(gas_type, (datum_gas p) => p.gas_overlay), log(4, (j + 0.4f * TOTAL_VISIBLE_STATES) / (0.35f * TOTAL_VISIBLE_STATES)) * 255, i);
                Dm.append(fill, gas);
            }
        }
        return to_return;
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:33
    public static object gas_id2path(object id = null)
    {
        DmList meta_gas_id = (DmList)GLOB.meta_gas_info[META_GAS_ID];
        if (Dm.@in(id, meta_gas_id))
            return id;
        foreach ((object path, object meta_id) in Dm.pairs(meta_gas_id))
        {
            if (Dm.eq(meta_id, id))
                return path;
        }
        return "";
    }
}

/**
 * # Gas datums
 *
 * These are never and should never be instantiated,
 * they just exist to hold data which is passed into the gas metadata list.
 *
 * They are templates for how gases should generally act.
 */
// tg: code/modules/atmospherics/gasmixtures/gas_types.dm:50
[DmPath("/datum/gas")]
public partial class datum_gas : datum
{
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:51  (object: the V3 grep matches other vars named id, such as
    // the typepaths of /datum/pipe_info, _globalvars/pipe_info.dm:98; a gas id is text, GAS_O2)
    public object id = "";
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:52
    public float specific_heat = 0;
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:53  (/datum has no builtin name: tg declares it here)
    public string name = "";
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:54
    public object desc;
    ///icon_state in icons/effects/atmospherics.dmi
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:56
    public string gas_overlay = "";
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:57
    public object moles_visible = null;
    ///How much the gas accelerates a fusion reaction
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:59
    public float fusion_power = 0;
    /// relative rarity compared to other gases, used when setting up the reactions list.
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:61
    public float rarity = 0;
    /// Flags that relate to how the gas is handled in cargo
    /// GAS_PURCHASABLE - Can be purchased in cargo
    /// GAS_EXPORTABLE - Can be sold in cargo
    /// GAS_DANGEROUS - Is considered dangerous, requires elevated access to purchase
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:66
    public float cargo_flags = NONE;
    /// How does a single mole of this gas buy/sell for?
    /// Formula to calculate maximum value is in [code\modules\cargo\exports\large_objects.dm].
    /// Only necessary for exportable or purchasable gases, otherwise meaningless.
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:70
    public float base_value = 0;
    /// RGB code for use when a generic color representing the gas is needed. Colors taken from contants.ts
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:72
    public object primary_color;
}

// tg: code/modules/atmospherics/gasmixtures/gas_types.dm:74
[DmPath("/datum/gas/oxygen")]
public partial class datum_gas_oxygen : datum_gas
{
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:75-82
    public datum_gas_oxygen()
    {
        id = GAS_O2;
        specific_heat = 20;
        name = "Oxygen";
        rarity = 900;
        cargo_flags = GAS_PURCHASABLE;
        base_value = 0.2f;
        desc = "The gas most life forms need to be able to survive. Also an oxidizer.";
        primary_color = "#0000ff";
    }
}

// tg: code/modules/atmospherics/gasmixtures/gas_types.dm:84
[DmPath("/datum/gas/nitrogen")]
public partial class datum_gas_nitrogen : datum_gas
{
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:85-92
    public datum_gas_nitrogen()
    {
        id = GAS_N2;
        specific_heat = 20;
        name = "Nitrogen";
        rarity = 1000;
        cargo_flags = GAS_PURCHASABLE;
        base_value = 0.1f;
        desc = "A very common gas that used to pad artificial atmospheres to habitable pressure.";
        primary_color = "#ffff00";
    }
}

// tg: code/modules/atmospherics/gasmixtures/gas_types.dm:94  //what the fuck is this?
[DmPath("/datum/gas/carbon_dioxide")]
public partial class datum_gas_carbon_dioxide : datum_gas
{
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:95-103
    public datum_gas_carbon_dioxide()
    {
        id = GAS_CO2;
        specific_heat = 30;
        name = "Carbon Dioxide";
        rarity = 700;
        cargo_flags = Dm.bitor(GAS_PURCHASABLE, GAS_DANGEROUS);
        base_value = 0.2f;
        desc = "What the fuck is Carbon Dioxide?";
        // desc = "A colorless, odorless gas commonly produced by respiration and combustion. Potentially dangerous when inhaled in high concentrations."
        primary_color = COLOR_GRAY;
    }
}

// tg: code/modules/atmospherics/gasmixtures/gas_types.dm:105
[DmPath("/datum/gas/plasma")]
public partial class datum_gas_plasma : datum_gas
{
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:106-115
    public datum_gas_plasma()
    {
        id = GAS_PLASMA;
        specific_heat = 200;
        name = "Plasma";
        gas_overlay = "plasma";
        moles_visible = MOLES_GAS_VISIBLE;
        cargo_flags = GAS_DANGEROUS;
        rarity = 800;
        base_value = 1.5f;
        desc = "A flammable gas with many curious properties. Its research is one of Nanotrasen's primary objectives.";
        primary_color = "#ffc0cb";
    }
}

// tg: code/modules/atmospherics/gasmixtures/gas_types.dm:117
[DmPath("/datum/gas/water_vapor")]
public partial class datum_gas_water_vapor : datum_gas
{
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:118-128
    public datum_gas_water_vapor()
    {
        id = GAS_WATER_VAPOR;
        specific_heat = 40;
        name = "Water Vapor";
        gas_overlay = "water_vapor";
        moles_visible = MOLES_GAS_VISIBLE;
        fusion_power = 8;
        rarity = 500;
        cargo_flags = GAS_PURCHASABLE;
        base_value = 0.5f;
        desc = "Water, in gas form. Makes floors slippery and washes items on them.";
        primary_color = "#b0c4de";
    }
}

// tg: code/modules/atmospherics/gasmixtures/gas_types.dm:130
[DmPath("/datum/gas/hypernoblium")]
public partial class datum_gas_hypernoblium : datum_gas
{
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:131-141
    public datum_gas_hypernoblium()
    {
        id = GAS_HYPER_NOBLIUM;
        specific_heat = 2000;
        name = "Hyper-Noblium";
        gas_overlay = "freon";
        moles_visible = MOLES_GAS_VISIBLE;
        fusion_power = 10;
        rarity = 50;
        cargo_flags = GAS_EXPORTABLE;
        base_value = 2.5f;
        desc = "The most noble gas of them all. High quantities actively prevents reactions from occurring.";
        primary_color = COLOR_TEAL;
    }
}

// tg: code/modules/atmospherics/gasmixtures/gas_types.dm:143
[DmPath("/datum/gas/nitrous_oxide")]
public partial class datum_gas_nitrous_oxide : datum_gas
{
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:144-155
    public datum_gas_nitrous_oxide()
    {
        id = GAS_N2O;
        specific_heat = 40;
        name = "Nitrous Oxide";
        gas_overlay = "nitrous_oxide";
        moles_visible = MOLES_GAS_VISIBLE * 2;
        fusion_power = 10;
        rarity = 600;
        cargo_flags = Dm.bitor(GAS_PURCHASABLE, GAS_DANGEROUS);
        base_value = 1.5f;
        desc = "A gas known to causes drowsiness, euphoria, and eventually unconsciousness. Commonly used as an anesthetic for surgical procedures, and occasionally, as a recreational drug.";
        primary_color = "#ffe4c4";
    }
}

// tg: code/modules/atmospherics/gasmixtures/gas_types.dm:157
[DmPath("/datum/gas/nitrium")]
public partial class datum_gas_nitrium : datum_gas
{
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:158-168
    public datum_gas_nitrium()
    {
        id = GAS_NITRIUM;
        specific_heat = 10;
        name = "Nitrium";
        fusion_power = 7;
        gas_overlay = "nitrium";
        moles_visible = MOLES_GAS_VISIBLE;
        cargo_flags = Dm.bitor(GAS_DANGEROUS, GAS_EXPORTABLE);
        rarity = 1;
        base_value = 6;
        desc = "An experimental (and slightly toxic) performance enhancing gas that increases speed and alertness when inhaled.";
        primary_color = "#a52a2a";
    }
}

// tg: code/modules/atmospherics/gasmixtures/gas_types.dm:170
[DmPath("/datum/gas/tritium")]
public partial class datum_gas_tritium : datum_gas
{
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:171-181
    public datum_gas_tritium()
    {
        id = GAS_TRITIUM;
        specific_heat = 10;
        name = "Tritium";
        gas_overlay = "tritium";
        moles_visible = MOLES_GAS_VISIBLE;
        cargo_flags = Dm.bitor(GAS_DANGEROUS, GAS_EXPORTABLE);
        fusion_power = 5;
        rarity = 300;
        base_value = 2.5f;
        desc = "A highly flammable and radioactive gas.";
        primary_color = "#32cd32";
    }
}

// tg: code/modules/atmospherics/gasmixtures/gas_types.dm:183
[DmPath("/datum/gas/bz")]
public partial class datum_gas_bz : datum_gas
{
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:184-192
    public datum_gas_bz()
    {
        id = GAS_BZ;
        specific_heat = 20;
        name = "BZ";
        fusion_power = 8;
        rarity = 400;
        cargo_flags = Dm.bitor(Dm.bitor(GAS_PURCHASABLE, GAS_EXPORTABLE), GAS_DANGEROUS);
        base_value = 1.5f;
        desc = "A powerful hallucinogenic nerve agent able to induce cognitive damage.";
        primary_color = "#9370db";
    }
}

// tg: code/modules/atmospherics/gasmixtures/gas_types.dm:194
[DmPath("/datum/gas/pluoxium")]
public partial class datum_gas_pluoxium : datum_gas
{
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:195-204
    public datum_gas_pluoxium()
    {
        id = GAS_PLUOXIUM;
        specific_heat = 80;
        name = "Pluoxium";
        fusion_power = -10;
        rarity = 200;
        cargo_flags = GAS_EXPORTABLE;
        base_value = 2.5f;
        desc = "An alternative to oxygen that is eight times more efficient at lung diffusion and even has minor healing properties - while itself not being an oxidizer.";
        primary_color = "#7b68ee";
    }
}

// tg: code/modules/atmospherics/gasmixtures/gas_types.dm:206
[DmPath("/datum/gas/miasma")]
public partial class datum_gas_miasma : datum_gas
{
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:207-217
    public datum_gas_miasma()
    {
        id = GAS_MIASMA;
        specific_heat = 20;
        name = "Miasma";
        cargo_flags = Dm.bitor(GAS_DANGEROUS, GAS_EXPORTABLE);
        gas_overlay = "miasma";
        moles_visible = MOLES_GAS_VISIBLE * 60;
        rarity = 250;
        base_value = 1;
        desc = "Miasma is not necessarily a gas, but more broadly refers to biological pollutants found in the atmosphere known to cause disease and nausea.";
        primary_color = COLOR_OLIVE;
    }
}

// tg: code/modules/atmospherics/gasmixtures/gas_types.dm:219
[DmPath("/datum/gas/freon")]
public partial class datum_gas_freon : datum_gas
{
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:220-230
    public datum_gas_freon()
    {
        id = GAS_FREON;
        specific_heat = 600;
        name = "Freon";
        cargo_flags = Dm.bitor(GAS_DANGEROUS, GAS_EXPORTABLE);
        gas_overlay = "freon";
        moles_visible = MOLES_GAS_VISIBLE * 30;
        fusion_power = -5;
        rarity = 10;
        base_value = 5;
        desc = "A coolant gas. Primarily used for its endothermic reaction with oxygen.";
        primary_color = "#afeeee";
    }
}

// tg: code/modules/atmospherics/gasmixtures/gas_types.dm:232
[DmPath("/datum/gas/hydrogen")]
public partial class datum_gas_hydrogen : datum_gas
{
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:233-241
    public datum_gas_hydrogen()
    {
        id = GAS_HYDROGEN;
        specific_heat = 15;
        name = "Hydrogen";
        cargo_flags = Dm.bitor(GAS_DANGEROUS, GAS_EXPORTABLE);
        fusion_power = 2;
        rarity = 600;
        base_value = 1;
        desc = "A highly flammable gas.";
        primary_color = "#ffffff";
    }
}

// tg: code/modules/atmospherics/gasmixtures/gas_types.dm:243
[DmPath("/datum/gas/healium")]
public partial class datum_gas_healium : datum_gas
{
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:244-254
    public datum_gas_healium()
    {
        id = GAS_HEALIUM;
        specific_heat = 10;
        name = "Healium";
        cargo_flags = Dm.bitor(GAS_DANGEROUS, GAS_EXPORTABLE);
        gas_overlay = "healium";
        moles_visible = MOLES_GAS_VISIBLE;
        rarity = 300;
        base_value = 5.5f;
        desc = "An experimental alternative to anesthetic that induces a state of unconsciousness that accelerates healing and regeneration when inhaled.";
        primary_color = "#fa8072";
    }
}

// tg: code/modules/atmospherics/gasmixtures/gas_types.dm:256
[DmPath("/datum/gas/proto_nitrate")]
public partial class datum_gas_proto_nitrate : datum_gas
{
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:257-266
    public datum_gas_proto_nitrate()
    {
        id = GAS_PROTO_NITRATE;
        specific_heat = 30;
        name = "Proto-Nitrate";
        cargo_flags = Dm.bitor(GAS_DANGEROUS, GAS_EXPORTABLE);
        gas_overlay = "proto_nitrate";
        moles_visible = MOLES_GAS_VISIBLE;
        rarity = 200;
        base_value = 2.5f;
        desc = "A volatile gas that has wildly different reactions with other gases.";
        primary_color = "#adff2f";
    }
}

// tg: code/modules/atmospherics/gasmixtures/gas_types.dm:268
[DmPath("/datum/gas/zauker")]
public partial class datum_gas_zauker : datum_gas
{
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:269-279
    public datum_gas_zauker()
    {
        id = GAS_ZAUKER;
        specific_heat = 350;
        name = "Zauker";
        cargo_flags = Dm.bitor(GAS_DANGEROUS, GAS_EXPORTABLE);
        gas_overlay = "zauker";
        moles_visible = MOLES_GAS_VISIBLE;
        rarity = 1;
        base_value = 7;
        desc = "A highly toxic gas and difficult to produce gas. It breaks down when in contact with Nitrogen, making it relatively safe in Earth-like atmospheres.";
        primary_color = "#006400";
    }
}

// tg: code/modules/atmospherics/gasmixtures/gas_types.dm:281
[DmPath("/datum/gas/halon")]
public partial class datum_gas_halon : datum_gas
{
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:282-291
    public datum_gas_halon()
    {
        id = GAS_HALON;
        specific_heat = 175;
        name = "Halon";
        cargo_flags = Dm.bitor(GAS_DANGEROUS, GAS_EXPORTABLE);
        gas_overlay = "halon";
        moles_visible = MOLES_GAS_VISIBLE;
        rarity = 300;
        base_value = 4;
        desc = "A potent fire suppressant. It removes Oxygen from high temperature fires and cools down the area.";
        primary_color = COLOR_PURPLE;
    }
}

// tg: code/modules/atmospherics/gasmixtures/gas_types.dm:293
[DmPath("/datum/gas/helium")]
public partial class datum_gas_helium : datum_gas
{
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:294-303
    public datum_gas_helium()
    {
        id = GAS_HELIUM;
        specific_heat = 15;
        name = "Helium";
        fusion_power = 7;
        rarity = 50;
        cargo_flags = GAS_EXPORTABLE;
        base_value = 3.5f;
        desc = "An inert noble gas produced by the fusion of Hydrogen and its derivatives. Commonly known for being lighter than air and its ability to raise voice pitch when inhaled.";
        primary_color = "#f0f8ff";
    }
}

// tg: code/modules/atmospherics/gasmixtures/gas_types.dm:305
[DmPath("/datum/gas/antinoblium")]
public partial class datum_gas_antinoblium : datum_gas
{
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:306-317
    public datum_gas_antinoblium()
    {
        id = GAS_ANTINOBLIUM;
        specific_heat = 1;
        name = "Anti-Noblium";
        cargo_flags = Dm.bitor(GAS_DANGEROUS, GAS_EXPORTABLE);
        gas_overlay = "antinoblium";
        moles_visible = MOLES_GAS_VISIBLE;
        fusion_power = 20;
        rarity = 1;
        base_value = 10;
        desc = "A mysterious and highly reactive gas known to replicate itself. It is highly sought after due to its rarity, and will export for a high price.";
        primary_color = COLOR_MAROON;
    }
}

// tg: code/modules/atmospherics/gasmixtures/gas_types.dm:319
public partial class obj_effect_overlay_gas : obj_effect_overlay
{
    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:331
    [DmParams("state", "alph", "offset")]
    public override async DmTask New(params object[] __extra)
    {
        object state = Dm.arg(__extra, 0);
        object alph = Dm.arg(__extra, 1);
        object offset = Dm.arg(__extra, 2);
        await base.New(__extra);
        icon_state = state as string;
        alpha = Dm.num(alph);
        plane_offset = Dm.num(offset);
    }

    // tg: code/modules/atmospherics/gasmixtures/gas_types.dm:337
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        plane = GET_NEW_PLANE(Dm.initial(this, (obj_effect_overlay_gas p) => p.plane), plane_offset); // SET_PLANE_W_SCALAR(src, initial(plane), plane_offset) (__HELPERS/_planes.dm:35), inline (rules 11.1 (a))
        return dot;
    }
}
