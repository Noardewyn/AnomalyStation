// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/mannequin.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps ST2 (D333): the whole file (the mannequin: its wood, plastic or skeleton body, male or female, on its pedestal, dressed and
// undressed through tg's strip menu slot by slot with each worn item drawn on it as on a human, its underwear, undershirt and socks
// picked by a right click, turned, wrenched, its clothes dropped when it is destroyed; the skeleton model with its eyepatch and
// Hawaiian shirt). Its random body and material at Initialize draw from the world's random stream, as tg's. The var blocks are
// generated (D111).

/// A mannequin! A structure that can display clothing on itself.
// tg: code/game/objects/structures/mannequin.dm:6
public partial class obj_structure_mannequin : obj_structure
{
    // tg: code/game/objects/structures/mannequin.dm:1-3 (the file's #defines, #undef'd at its end)
    internal const string MANNEQUIN_WOOD = "wood";
    internal const string MANNEQUIN_PLASTIC = "plastic";
    internal const string MANNEQUIN_SKELETON = "skeleton";

    /// Static list of slot flags we have clothing slots for.
    // tg: code/game/objects/structures/mannequin.dm:30  var/static/list/slot_flags
    public static ref DmList slot_flags => ref TgState.Current.obj_structure_mannequin__slot_flags;

    // tg: code/game/objects/structures/mannequin.dm:49
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        foreach (object slot_flag in Dm.each(slot_flags))
        {
            worn_items[Dm.text("[]", slot_flag)] = null;
            foreach (Type items in Dm.each_cast<Type>(starting_items))
            {
                if (Dm.truthy(Dm.bitand(Dm.initial(items, (obj_item_clothing p) => p.slot_flags), slot_flag)))
                {
                    worn_items[Dm.text("[]", slot_flag)] = Dm.@new(items, this);
                    Dm.remove(ref starting_items, items);
                    break;
                }
            }
        }
        if (Dm.truthy(starting_items.len))
            throw Dm.CRASH(Dm.text("[] had [] starting items fail to equip.", this, starting_items.len), "code/game/objects/structures/mannequin.dm", 59);
        if (!Dm.truthy(body_type))
            body_type = pick(MALE, FEMALE);
        if (!Dm.truthy(material))
            material = pick(MANNEQUIN_WOOD, MANNEQUIN_PLASTIC);
        icon_state = Dm.text("mannequin_[]_[]", material, Dm.eq(body_type, FEMALE) ? "female" : "male");
        Waiting.NoSleep(AddElement(typeof(datum_element_strippable), GLOB.strippable_mannequin_items));
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation), ROTATION_IGNORE_ANCHORED));
        Waiting.NoSleep(AddComponent(typeof(datum_component_marionette)));
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/structures/mannequin.dm:70
    public override object Destroy(float? force = null)
    {
        QDEL_LIST_ASSOC_VAL(worn_items);
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/mannequin.dm:74
    public override async DmTask atom_destruction(object damage_flag = null)
    {
        foreach (object slot_flag in Dm.each(worn_items))
        {
            obj_item worn_item = worn_items[slot_flag] as obj_item;
            if (worn_item != null)
                await worn_item.forceMove(drop_location() as atom);
        }
        await base.atom_destruction(damage_flag);
    }

    // tg: code/game/objects/structures/mannequin.dm:81
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        foreach (object slot_flag in Dm.each(worn_items))
        {
            if (worn_items[slot_flag] == gone)
                worn_items[slot_flag] = null;
        }
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/structures/mannequin.dm:88
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/mannequin.dm:93
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        mutable_appearance pedestal = mutable_appearance(icon, Dm.text("pedestal_[]", material)) as mutable_appearance;
        pedestal.pixel_z = -3;
        Dm.append(ref dot, pedestal);
        datum_sprite_accessory_clothing_underwear underwear = SSaccessories.underwear_list[underwear_name] as datum_sprite_accessory_clothing_underwear;
        object underwear_overlay = underwear?.make_appearance(COLOR_WHITE, Dm.str(body_type), BODYSHAPE_HUMANOID);
        if (Dm.truthy(underwear_overlay))
            Dm.append(ref dot, underwear_overlay);
        datum_sprite_accessory_clothing_undershirt undershirt = SSaccessories.undershirt_list[undershirt_name] as datum_sprite_accessory_clothing_undershirt;
        object undershirt_overlay = undershirt?.make_appearance(COLOR_WHITE, Dm.str(body_type), BODYSHAPE_HUMANOID);
        if (Dm.truthy(undershirt_overlay))
            Dm.append(ref dot, undershirt_overlay);
        datum_sprite_accessory_clothing_socks socks = SSaccessories.socks_list[socks_name] as datum_sprite_accessory_clothing_socks;
        object socks_overlay = socks?.make_appearance(COLOR_WHITE, Dm.str(body_type), BODYSHAPE_HUMANOID);
        if (Dm.truthy(socks_overlay))
            Dm.append(ref dot, socks_overlay);
        foreach (object slot_flag in Dm.each(worn_items))
        {
            obj_item worn_item = worn_items[slot_flag] as obj_item;
            if (worn_item == null)
                continue;
            object default_icon = get_default_icon_by_slot(text2num(slot_flag));
            object default_layer = get_default_layer_by_slot(text2num(slot_flag));
            object female_icon = NO_FEMALE_UNIFORM;
            if (Dm.eq(body_type, FEMALE) && istype(worn_item, typeof(obj_item_clothing_under)))
            {
                obj_item_clothing_under worn_jumpsuit = (obj_item_clothing_under)worn_item;
                female_icon = worn_jumpsuit.female_sprite_flags;
            }
            Dm.append(ref dot, await worn_item.build_worn_icon(Dm.num_or_null(default_layer), default_icon, female_uniform: Dm.num_or_null(female_icon)));
        }
        return dot;
    }

    // tg: code/game/objects/structures/mannequin.dm:122
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;
        object choice = await tgui_input_list(user, I18n.T("Underwear, Undershirt, or Socks?"), I18n.T("Changing"), Dm.list("Underwear", "Undershirt", "Socks"));
        if (!Dm.truthy(await Adjacent(user)))
            return dot;
        switch (Dm.str(choice))
        {
            case "Underwear":
            {
                object new_undies = await tgui_input_list(user, I18n.T("Select the mannequin's underwear"), I18n.T("Changing"), SSaccessories.underwear_list);
                if (Dm.truthy(new_undies))
                    underwear_name = new_undies;
                break;
            }
            case "Undershirt":
            {
                object new_undershirt = await tgui_input_list(user, I18n.T("Select the mannequin's undershirt"), I18n.T("Changing"), SSaccessories.undershirt_list);
                if (Dm.truthy(new_undershirt))
                    undershirt_name = new_undershirt;
                break;
            }
            case "Socks":
            {
                object new_socks = await tgui_input_list(user, I18n.T("Select the mannequin's socks"), I18n.T("Changing"), SSaccessories.socks_list);
                if (Dm.truthy(new_socks))
                    socks_name = new_socks;
                break;
            }
        }
        update_appearance();
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/structures/mannequin.dm:30
    internal DmList obj_structure_mannequin__slot_flags = Dm.list(
        ITEM_SLOT_HEAD,
        ITEM_SLOT_EYES,
        ITEM_SLOT_EARS,
        ITEM_SLOT_MASK,
        ITEM_SLOT_NECK,
        ITEM_SLOT_BACK,
        ITEM_SLOT_BELT,
        ITEM_SLOT_ID,
        ITEM_SLOT_ICLOTHING,
        ITEM_SLOT_OCLOTHING,
        ITEM_SLOT_GLOVES,
        ITEM_SLOT_FEET);
}

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/game/objects/structures/mannequin.dm:162  GLOBAL_LIST_INIT(strippable_mannequin_items, create_strippable_list(list(...)))
    public DmList strippable_mannequin_items;

    // tg: code/game/objects/structures/mannequin.dm:162  (GLOBAL_MANAGED(strippable_mannequin_items, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalstrippable_mannequin_items()
    {
        strippable_mannequin_items = create_strippable_list(Dm.list(
            typeof(datum_strippable_item_mannequin_slot_head),
            typeof(datum_strippable_item_mannequin_slot_eyes),
            typeof(datum_strippable_item_mannequin_slot_ears),
            typeof(datum_strippable_item_mannequin_slot_mask),
            typeof(datum_strippable_item_mannequin_slot_neck),
            typeof(datum_strippable_item_mannequin_slot_back),
            typeof(datum_strippable_item_mannequin_slot_belt),
            typeof(datum_strippable_item_mannequin_slot_id),
            typeof(datum_strippable_item_mannequin_slot_uniform),
            typeof(datum_strippable_item_mannequin_slot_suit),
            typeof(datum_strippable_item_mannequin_slot_gloves),
            typeof(datum_strippable_item_mannequin_slot_feet))) as DmList;
        Dm.append(ref gvars_datum_init_order, "strippable_mannequin_items");
    }
}

// tg: code/game/objects/structures/mannequin.dm:177
public partial class datum_strippable_item_mannequin_slot : datum_strippable_item
{
    // tg: code/game/objects/structures/mannequin.dm:181
    public override object get_item(atom source = null)
    {
        obj_structure_mannequin mannequin_source = source as obj_structure_mannequin;
        return mannequin_source != null ? mannequin_source.worn_items[Dm.text("[]", item_slot)] : null;
    }

    // tg: code/game/objects/structures/mannequin.dm:185
    public override async DmTask<object> try_equip(atom source = null, obj_item equipping = null, mob user = null)
    {
        object dot = await base.try_equip(source, equipping, user);
        if (!Dm.truthy(dot))
            return FALSE;
        if (!Dm.truthy(Dm.bitand(equipping.slot_flags, item_slot)))
        {
            to_chat(user, span_warning(I18n.T("[] won't fit!", equipping)));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/game/objects/structures/mannequin.dm:194
    public override object finish_equip(atom source = null, obj_item equipping = null, mob user = null)
    {
        obj_structure_mannequin mannequin_source = source as obj_structure_mannequin;
        if (mannequin_source == null)
            return null;
        if (!Dm.truthy(Waiting.NoSleep(user.transferItemToLoc(equipping, mannequin_source))) || QDELETED(equipping))
            return null;
        mannequin_source.worn_items[Dm.text("[]", item_slot)] = equipping;
        mannequin_source.update_appearance();
        return null;
    }

    // tg: code/game/objects/structures/mannequin.dm:203
    public override async DmTask<object> finish_unequip(atom source = null, mob user = null)
    {
        obj_structure_mannequin mannequin_source = source as obj_structure_mannequin;
        if (mannequin_source == null)
            return null;
        obj_item unequipped = mannequin_source.worn_items[Dm.text("[]", item_slot)] as obj_item;
        await user.put_in_hands(unequipped);
        return null;
    }
}
