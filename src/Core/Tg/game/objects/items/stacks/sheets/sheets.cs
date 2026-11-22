// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/stacks/sheets/sheets.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 2 (materials and stacks): the whole file. The var block is generated (D111).

// tg: code/game/objects/items/stacks/sheets/sheets.dm:1
[DmPath("/obj/item/stack/sheet")]
public partial class obj_item_stack_sheet : obj_item_stack
{
    // tg: code/game/objects/items/stacks/sheets/sheets.dm:31
    [DmParams("mapload", "new_amount", "merge", "mat_override", "mat_amt")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object new_amount = Dm.arg(__extra, 0);
        float? merge = Dm.arg<float?>(__extra, 1);
        DmList mat_override = Dm.arg<DmList>(__extra, 2);
        float? mat_amt = Dm.arg<float?>(__extra, 3);
        merge ??= TRUE;
        mat_amt ??= 1;
        object dot = base.Initialize(mapload, Dm.forward(__extra, new_amount, merge, mat_override, mat_amt));
        pixel_x = rand(-4, 4);
        pixel_y = rand(-4, 4);
        if (Dm.truthy(sniffable) && Dm.num(amount) >= 10 && Dm.truthy(is_station_level(z)))
            Dm.combine(ref GLOB.sniffable_sheets, this);
        return dot;
    }

    // tg: code/game/objects/items/stacks/sheets/sheets.dm:38
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(sniffable))
            Dm.remove(ref GLOB.sniffable_sheets, this);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/stacks/sheets/sheets.dm:43
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(manufactured) && Dm.truthy(gulag_value))
            Dm.append(ref dot, span_notice(I18n.T("It has been embossed with a manufacturer's mark of guaranteed quality.")));

        datum_material material = await get_master_material() as datum_material;
        if (!HAS_TRAIT(user, TRAIT_RESEARCH_SCANNER) || !Dm.truthy(material))
            return dot;

        DmList material_string = Dm.list();
        foreach (object prop_id in Dm.each(material.mat_properties))
        {
            datum_material_property property = SSmaterials.properties[prop_id] as datum_material_property;
            object prop_value = material.get_property(prop_id);
            if (prop_value == null) // Error?
                continue;
            object descriptor = property?.get_descriptor(prop_value);
            object tooltip_hint = property?.get_tooltip(prop_value);
            if (Dm.truthy(descriptor)) // Overriden derivative property?
                Dm.append(ref material_string, span_tooltip(I18n.T("[]: []", property, tooltip_hint), Dm.str(descriptor)));
        }

        if (Dm.truthy(length(material_string)))
            Dm.append(ref dot, span_info(I18n.T("[] is [].", capitalize(material.name), english_list(material_string))));
        return dot;
    }

    // tg: code/game/objects/items/stacks/sheets/sheets.dm:66
    public override async DmTask add(object _amount = null)
    {
        await base.add(_amount);
        if (Dm.truthy(sniffable) && Dm.num(amount) >= 10 && Dm.truthy(is_station_level(z)))
            Dm.combine(ref GLOB.sniffable_sheets, this);
    }

    // tg: code/game/objects/items/stacks/sheets/sheets.dm:71
    public override async DmTask<object> merge(obj_item_stack target_stack = null, object limit = null)
    {
        object dot = await base.merge(target_stack, limit);
        manufactured = Dm.num(Dm.truthy(manufactured) && Dm.truthy(((obj_item_stack_sheet)target_stack).manufactured));
        return dot;
    }

    // tg: code/game/objects/items/stacks/sheets/sheets.dm:75
    public override async DmTask copy_evidences(obj_item_stack from = null)
    {
        await base.copy_evidences(from);
        manufactured = ((obj_item_stack_sheet)from).manufactured;
    }

    /// removing from sniffable handled by the sniffer itself when it checks for targets

    /**
     * Facilitates sheets being smacked on the floor
     *
     * This is used for crafting by hitting the floor with items.
     * The initial use case is glass sheets breaking in to shards when the floor is hit.
     * Args:
     * * target: The floor that was hit
     * * user: The user that did the action
     * * modifiers: The modifiers passed in from attackby
     */
    // tg: code/game/objects/items/stacks/sheets/sheets.dm:91
    public virtual async DmTask<object> on_attack_floor(turf_open_floor target = null, mob user = null, DmList modifiers = null)
    {
        DmList shards = Dm.list();
        foreach (datum_material mat in Dm.each<datum_material>(custom_materials))
        {
            if (mat.shard_type != null)
                Dm.append(ref shards, mat.shard_type);
        }
        if (!Dm.truthy(shards.len))
            return FALSE;
        if (!Dm.truthy(await use(1)))
        {
            to_chat(user, Dm.truthy(is_cyborg) ? span_warning(I18n.T("There is not enough material in the synthesizer to produce a shard!")) : span_warning(I18n.T("Somehow, there is not enough of [] to shatter!", this)));
            if (!Dm.truthy(is_cyborg))
                stack_trace("A stack of sheet material was attempted to be shattered into shards while having less than 1 sheets remaining.", "code/game/objects/items/stacks/sheets/sheets.dm", 101);
            return FALSE;
        }
        await user.do_attack_animation(target, ATTACK_EFFECT_BOOP);
        playsound(target, SFX_SHATTER, 70, TRUE);
        DmList shards_created = Dm.list();
        foreach (object shard_to_create in Dm.each(shards))
        {
            obj_item new_shard = Dm.@new((Type)shard_to_create, target) as obj_item;
            // tg: code/game/objects/items/stacks/sheets/sheets.dm:108
            new_shard.add_fingerprint(user);
            Dm.append(ref shards_created, Dm.text("[]", new_shard.name));
        }
        user.visible_message(span_notice(I18n.T("[] shatters the sheet of [] on [], leaving [].", user, name, target, english_list(shards_created))),
            span_notice(I18n.T("You shatter the sheet of [] on [], leaving [].", name, target, english_list(shards_created))));
        return TRUE;
    }
}
