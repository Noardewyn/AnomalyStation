// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/gas_recipe_machines/crystallizer_items.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E12a (D287): the whole file, the crystallizer's own products. The hyper-noblium crystal stops the reactions in a
// portable atmospherics device or pressure-proofs a piece of clothing; the nitrium crystal is chipped in the hand. Deferred leaf:
// the nitrium crystal's chemical smoke (do_chem_smoke: the smoke effects are not ported). The var blocks are generated.

// tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/crystallizer_items.dm:1
public partial class obj_item_hypernoblium_crystal : obj_item
{
    // tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/crystallizer_items.dm:8
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        obj_machinery_portable_atmospherics atmos_device = interacting_with as obj_machinery_portable_atmospherics;
        obj_item_clothing worn_item = interacting_with as obj_item_clothing;
        if (worn_item == null && atmos_device == null)
        {
            to_chat(user, span_warning(I18n.T("The crystal can only be used on clothing and portable atmospheric devices!")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (atmos_device != null)
        {
            if (Dm.truthy(atmos_device.nob_crystal_inserted))
            {
                to_chat(user, span_warning(I18n.T("[] already has a hypernoblium crystal inserted in it!", atmos_device)));
                return ITEM_INTERACT_BLOCKING;
            }
            atmos_device.insert_nob_crystal();
            to_chat(user, span_notice(I18n.T("You insert the [] into [].", this, atmos_device)));
        }

        if (worn_item != null)
        {
            if (istype(worn_item, typeof(obj_item_clothing_suit_space)))
            {
                to_chat(user, span_warning(I18n.T("The [] is already pressure-resistant!", worn_item)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (Dm.eq(worn_item.min_cold_protection_temperature, SPACE_SUIT_MIN_TEMP_PROTECT) && Dm.truthy(Dm.bitand(worn_item.clothing_flags, STOPSPRESSUREDAMAGE)))
            {
                to_chat(user, span_warning(I18n.T("[] is already pressure-resistant!", worn_item)));
                return ITEM_INTERACT_BLOCKING;
            }
            to_chat(user, span_notice(I18n.T("You see how the [] changes color, it's now pressure proof.", worn_item)));
            worn_item.name = Dm.text("pressure-resistant []", worn_item.name);
            await worn_item.remove_atom_colour(WASHABLE_COLOUR_PRIORITY);
            await worn_item.add_atom_colour(color_transition_filter("#00fff7", SATURATION_OVERRIDE), FIXED_COLOUR_PRIORITY);
            worn_item.min_cold_protection_temperature = SPACE_SUIT_MIN_TEMP_PROTECT;
            worn_item.cold_protection = worn_item.body_parts_covered;
            worn_item.clothing_flags = Dm.bitor(worn_item.clothing_flags, STOPSPRESSUREDAMAGE);
        }

        uses--;
        if (uses <= 0)
            qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/crystallizer_items.dm:42
public partial class obj_item_nitrium_crystal : obj_item
{
    // tg: code/modules/atmospherics/machinery/components/gas_recipe_machines/crystallizer_items.dm:49
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        await do_chem_smoke(cloud_size, this, get_turf(this), Dm.list(Dm.kv(typeof(datum_reagent_nitrium_low_metabolization), 3), Dm.kv(typeof(datum_reagent_nitrium_high_metabolization), 2)));
        qdel(this);
        return dot;
    }
}
