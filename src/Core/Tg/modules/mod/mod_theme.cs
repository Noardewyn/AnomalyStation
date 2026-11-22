// Translated from tgstation (snapshot 1d99bd7f): code/modules/mod/mod_theme.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The file whole. The themes' singletons (GLOB.mod_themes) came with silicons SI3 (D326), the MOD plating designs read their
// descriptions; gaps MO1 (D333) adds the themes' suit procs: set_up_parts makes a control unit's parts from the theme's default
// variant (the helmet, chestplate, gauntlets and boots, each with its part datum) and gives them the theme's name, armour and
// protections; set_skin dresses them in a skin's icons, seal flags, layers and messages. The mining theme's suit storage list
// (mining/New). tg's UNIT_TESTS-only /datum/mod_theme/New (the variants' parts check, :94-104) is inactive in production. The var
// blocks are generated (D111).

public static partial class GlobalProcs
{
    /// Global proc that sets up all MOD themes as singletons in a list and returns it.
    // tg: code/modules/mod/mod_theme.dm:2
    public static DmList setup_mod_themes()
    {
        DmList dot = Dm.list();
        foreach (Type path in Dm.each_cast<Type>(valid_subtypesof(typeof(datum_mod_theme))))
        {
            datum_mod_theme new_theme = Dm.@new(path) as datum_mod_theme;
            dot[path] = new_theme;
        }
        return dot;
    }

    /// Simple proc to search through mod_themes global to return a theme
    // tg: code/modules/mod/mod_theme.dm:9
    public static datum_mod_theme find_mod_theme(object haystack = null)
    {
        foreach (object entry in Dm.each(GLOB.mod_themes))
        {
            datum_mod_theme theme_singleton = GLOB.mod_themes[entry] as datum_mod_theme;
            if (Dm.truthy(findtext(haystack, theme_singleton.default_skin)))
                return theme_singleton;
        }
        return null;
    }
}

public partial class datum_controller_global_vars
{
    /// Global list of all /datum/mod_theme
    // tg: code/__DEFINES/mod.dm:68  GLOBAL_LIST_INIT(mod_themes, setup_mod_themes())
    public DmList mod_themes;

    public virtual void InitGlobalmod_themes()
    {
        mod_themes = setup_mod_themes();
        Dm.append(ref gvars_datum_init_order, "mod_themes");
    }
}

/// MODsuit theme, instanced once and then used by MODsuits to grab various statistics.
// tg: code/modules/mod/mod_theme.dm:15
public partial class datum_mod_theme : datum
{
    // tg: #ifdef UNIT_TESTS /datum/mod_theme/New() ... #endif not translated (inactive in production)

    /// Create parts of the suit and modify them using the theme's variables.
    // tg: code/modules/mod/mod_theme.dm:107
    public virtual async DmTask set_up_parts(obj_item_mod_control mod = null, object skin = null)
    {
        DmList parts = Dm.list(mod);
        mod.slot_flags = slot_flags;
        mod.extended_desc = extended_desc;
        mod.slowdown_deployed = slowdown_deployed;
        mod.activation_step_time = activation_step_time;
        mod.complexity_max = complexity_max;
        mod.ui_theme = ui_theme;
        mod.charge_drain = charge_drain;
        datum_mod_part control_part_datum = Dm.@new<datum_mod_part>();
        control_part_datum.set_item(mod);
        mod.mod_parts[Dm.text("[]", mod.slot_flags)] = control_part_datum;
        foreach (object path in Dm.each(variants[default_skin]))
        {
            if (!ispath(path))
                continue;
            obj_item mod_part = await Dm.new_async(path as Type, mod) as obj_item;
            if (isclothing(mod_part) && Dm.eq(mod_part.slot_flags, ITEM_SLOT_OCLOTHING))
            {
                obj_item_clothing chestplate = mod_part as obj_item_clothing;
                Dm.combine(ref chestplate.allowed, allowed_suit_storage);
            }
            datum_mod_part part_datum = Dm.@new<datum_mod_part>();
            part_datum.set_item(mod_part);
            mod.mod_parts[Dm.text("[]", mod_part.slot_flags)] = part_datum;
            Dm.append(ref parts, mod_part);
        }

        foreach (obj_item part in Dm.each_cast<obj_item>(parts))
        {
            part.name = I18n.T("[] []", name, part.name);
            part.desc = I18n.T("[] []", part.desc, desc);
            part.set_armor(armor_type);
            part.resistance_flags = resistance_flags;
            Dm.combine(ref part.flags_1, atom_flags); //flags like initialization or admin spawning are here, so we cant set, have to add
            part.heat_protection = NONE;
            part.cold_protection = NONE;
            part.max_heat_protection_temperature = max_heat_protection_temperature;
            part.min_cold_protection_temperature = min_cold_protection_temperature;
            part.siemens_coefficient = siemens_coefficient;
        }

        await set_skin(mod, Dm.truthy(skin) ? skin : default_skin);
    }

    // tg: code/modules/mod/mod_theme.dm:145
    public virtual async DmTask set_skin(obj_item_mod_control mod = null, object skin = null)
    {
        mod.skin = skin as string;
        DmList used_skin = variants[skin] as DmList;
        DmList parts = mod.get_parts() as DmList;
        foreach (obj_item_clothing part in Dm.each_cast<obj_item_clothing>(parts))
        {
            DmList category = used_skin[part.type] as DmList;
            datum_mod_part part_datum = mod.get_part_datum(part) as datum_mod_part;
            part_datum.unsealed_layer = category[UNSEALED_LAYER];
            part_datum.sealed_layer = category[SEALED_LAYER];
            part_datum.unsealed_message = (category[UNSEALED_MESSAGE] is var or__1 && Dm.truthy(or__1) ? or__1 : "No unseal message set! Tell a coder!");
            part_datum.sealed_message = (category[SEALED_MESSAGE] is var or__2 && Dm.truthy(or__2) ? or__2 : "No seal message set! Tell a coder!");
            part.clothing_flags = Dm.num(category[UNSEALED_CLOTHING] is var or__3 && Dm.truthy(or__3) ? or__3 : NONE);
            part.visor_flags = Dm.num(category[SEALED_CLOTHING] is var or__4 && Dm.truthy(or__4) ? or__4 : NONE);
            part.flags_inv = (category[UNSEALED_INVISIBILITY] is var or__5 && Dm.truthy(or__5) ? or__5 : NONE);
            part.visor_flags_inv = Dm.num(category[SEALED_INVISIBILITY] is var or__6 && Dm.truthy(or__6) ? or__6 : NONE);
            part.flags_cover = Dm.num(category[UNSEALED_COVER] is var or__7 && Dm.truthy(or__7) ? or__7 : NONE);
            part.visor_flags_cover = Dm.num(category[SEALED_COVER] is var or__8 && Dm.truthy(or__8) ? or__8 : NONE);
            REMOVE_TRAITS_IN(part, MOD_THEME_TRAIT);
            foreach (object trait in Dm.each(category[MOD_PART_TRAITS]))
                ADD_TRAIT(part, trait, MOD_THEME_TRAIT);
            if (Dm.truthy(((datum_mod_part)mod.get_part_datum(part)).@sealed))
            {
                Dm.combine(ref part.clothing_flags, part.visor_flags);
                Dm.combine(ref part.flags_inv, part.visor_flags_inv);
                Dm.combine(ref part.flags_cover, part.visor_flags_cover);
                part.alternate_worn_layer = part_datum.sealed_layer;
            }
            else
            {
                part.alternate_worn_layer = part_datum.unsealed_layer;
            }
            if (part_datum.overslotting != null)
                await part_datum.overslotting.forceMove(mod.drop_location() as atom);
        }
        foreach (obj_item part in Dm.each_cast<obj_item>(Dm.add(parts, mod)))
        {
            part.icon = (used_skin[MOD_ICON_OVERRIDE] is var or__9 && Dm.truthy(or__9) ? or__9 : Dm.resource("icons/obj/clothing/modsuit/mod_clothing.dmi"));
            part.worn_icon = (used_skin[MOD_WORN_ICON_OVERRIDE] is var or__10 && Dm.truthy(or__10) ? or__10 : Dm.resource("icons/mob/clothing/modsuit/mod_clothing.dmi"));
            part.icon_state = Dm.text("[]-[][]", skin, part.base_icon_state, Dm.truthy(((datum_mod_part)mod.get_part_datum(part)).@sealed) ? "-sealed" : "");
            if (mod.wearer != null)
                await mod.wearer.update_clothing(part.slot_flags);
        }
        mod.wearer?.refresh_obscured();
    }
}

// tg: code/modules/mod/mod_theme.dm:524
public partial class datum_mod_theme_mining : datum_mod_theme
{
    // tg: code/modules/mod/mod_theme.dm:617
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        allowed_suit_storage = GLOB.mining_suit_allowed;
    }
}
