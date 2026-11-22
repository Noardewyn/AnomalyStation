// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/boulder_processing/refinery.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Lavaland L2c (D304): the whole file. The var block is generated (D111).

/**
 * Your new favorite industrial waste magnet!
 * Accepts boulders and produces sheets of non-metallic materials.
 * When upgraded, it can hold more boulders and process more at once.
 */
// tg: code/modules/mining/boulder_processing/refinery.dm:6
public partial class obj_machinery_bouldertech_refinery : obj_machinery_bouldertech
{
    // tg: code/modules/mining/boulder_processing/refinery.dm:17
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_boulder_reactions)));
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation)));
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/modules/mining/boulder_processing/refinery.dm:23
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        await set_light_on(Dm.truthy(anchored) && Dm.truthy(is_operational) && !Dm.truthy(panel_open) ? TRUE : FALSE);
        return dot;
    }

    // tg: code/modules/mining/boulder_processing/refinery.dm:27
    public override void create_reagents(object max_vol = null, object flags = null)
    {
        qdel(reagents);
        reagents = null;
        reagents = Dm.@new<datum_reagents_plumbing>(max_vol, flags);
        ((datum_reagents)reagents).my_atom = this;
    }

    // tg: code/modules/mining/boulder_processing/refinery.dm:32
    public override object get_booster_reagents()
    {
        if (!Dm.truthy(length(get_booster_reagents__booster_reagents)))
        {
            get_booster_reagents__booster_reagents = Dm.list(
                Dm.kv(typeof(datum_reagent_toxin_acid), 1),
                Dm.kv(typeof(datum_reagent_toxin_acid_fluacid), 2),
                Dm.kv(typeof(datum_reagent_toxin_acid_nitracid), 3),
                Dm.kv(typeof(datum_reagent_teslium), 5));
        }
        return get_booster_reagents__booster_reagents;
    }

    // tg: code/modules/mining/boulder_processing/refinery.dm:33  var/static/list/booster_reagents (a proc-level static, rules 10.6)
    private static ref DmList get_booster_reagents__booster_reagents => ref TgState.Current.obj_machinery_bouldertech_refinery__get_booster_reagents__booster_reagents;

    // tg: code/modules/mining/boulder_processing/refinery.dm:43
    public override object can_process_material(datum_material possible_mat = null)
    {
        if (!Dm.truthy(length(can_process_material__processable_materials)))
        {
            can_process_material__processable_materials = Dm.list(
                typeof(datum_material_glass),
                typeof(datum_material_plasma),
                typeof(datum_material_diamond),
                typeof(datum_material_bluespace),
                typeof(datum_material_bananium),
                typeof(datum_material_plastic));
        }
        return is_type_in_list(possible_mat, can_process_material__processable_materials);
    }

    // tg: code/modules/mining/boulder_processing/refinery.dm:44  var/static/list/processable_materials (a proc-level static, rules 10.6)
    private static ref DmList can_process_material__processable_materials => ref TgState.Current.obj_machinery_bouldertech_refinery__can_process_material__processable_materials;

    // tg: code/modules/mining/boulder_processing/refinery.dm:56
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();

        boulders_held_max = 0;
        foreach (datum_stock_part_matter_bin bin in Dm.each<datum_stock_part_matter_bin>(component_parts))
            boulders_held_max += Dm.num(bin.tier);

        boulders_processing_count = 0;
        foreach (datum_stock_part_servo servo in Dm.each<datum_stock_part_servo>(component_parts))
            boulders_processing_count += Dm.num(servo.tier);
        boulders_processing_count = ROUND_UP((boulders_processing_count / 8) * boulders_held_max);

        float new_volume = 0;
        foreach (obj_item_reagent_containers beaker in Dm.each<obj_item_reagent_containers>(component_parts))
            new_volume += Dm.num(beaker.volume);
        if (reagents == null)
            create_reagents(new_volume, OPENCONTAINER);
        ((datum_reagents)reagents).maximum_volume = new_volume;
        return dot;
    }

    // tg: code/modules/mining/boulder_processing/refinery.dm:75
    public override async DmTask check_for_boosts()
    {
        await base.check_for_boosts(); //resets to 1.00 efficiency in the parent

        datum_reagents holder = (datum_reagents)reagents;
        float highest_boost = 0;
        object biggest_booster = null;
        DmList booster_list = get_booster_reagents() as DmList;
        foreach (object booster in Dm.each(booster_list))
        {
            float booster_volume = Dm.num(booster_list[booster]);
            if (!Dm.truthy(holder.has_reagent(booster, booster_volume))) //check that we have the associated quantity of the chem in order to perform the boost.
                continue;
            if (Dm.num(booster_list[booster]) > highest_boost)
            {
                highest_boost = booster_volume;
                biggest_booster = booster;
            }
        }

        if (biggest_booster == null)
            return;

        await holder.remove_reagent(biggest_booster, highest_boost); //remove the associated amount from the reagents
        refining_efficiency = 1 + (highest_boost / 10); //Results in a boost from 10-30%
        await holder.add_reagent(waste_chemical, highest_boost);
    }

    // tg: code/modules/mining/boulder_processing/refinery.dm:96
    public override async DmTask<object> plunger_act(obj_item_plunger attacking_plunger = null, mob_living user = null, object reinforced = null)
    {
        object dot = await base.plunger_act(attacking_plunger, user, reinforced);
        balloon_alert(user, I18n.T("emptying..."));
        if (Dm.truthy(await do_after(user, 2 * SECONDS, this)))
        {
            datum_reagents holder = (datum_reagents)reagents;
            await holder.expose(drop_location() as atom);
            await holder.clear_reagents();
        }
        return dot;
    }
}

/**
 * Your other new favorite industrial waste magnet!
 * Accepts boulders and produces sheets of metallic materials.
 * When upgraded, it can hold more boulders and process more at once.
 */
// tg: code/modules/mining/boulder_processing/refinery.dm:110
public partial class obj_machinery_bouldertech_refinery_smelter : obj_machinery_bouldertech_refinery
{
    // tg: code/modules/mining/boulder_processing/refinery.dm:125
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_light_value();
        return dot;
    }

    // tg: code/modules/mining/boulder_processing/refinery.dm:130
    public override object get_booster_reagents()
    {
        if (!Dm.truthy(length(get_booster_reagents__booster_reagents)))
        {
            get_booster_reagents__booster_reagents = Dm.list(
                Dm.kv(typeof(datum_reagent_fuel), 1),
                Dm.kv(typeof(datum_reagent_thermite), 2),
                Dm.kv(typeof(datum_reagent_gunpowder), 3),
                Dm.kv(typeof(datum_reagent_liquid_dark_matter), 5));
        }
        return get_booster_reagents__booster_reagents;
    }

    // tg: code/modules/mining/boulder_processing/refinery.dm:131  var/static/list/booster_reagents (a proc-level static, rules 10.6)
    private static ref DmList get_booster_reagents__booster_reagents => ref TgState.Current.obj_machinery_bouldertech_refinery_smelter__get_booster_reagents__booster_reagents;

    // tg: code/modules/mining/boulder_processing/refinery.dm:141
    public override object can_process_material(datum_material possible_mat = null)
    {
        if (!Dm.truthy(length(can_process_material__processable_materials)))
        {
            can_process_material__processable_materials = Dm.list(
                typeof(datum_material_iron),
                typeof(datum_material_titanium),
                typeof(datum_material_silver),
                typeof(datum_material_gold),
                typeof(datum_material_uranium));
        }
        return is_type_in_list(possible_mat, can_process_material__processable_materials);
    }

    // tg: code/modules/mining/boulder_processing/refinery.dm:142  var/static/list/processable_materials (a proc-level static, rules 10.6)
    private static ref DmList can_process_material__processable_materials => ref TgState.Current.obj_machinery_bouldertech_refinery_smelter__can_process_material__processable_materials;

    // tg: code/modules/mining/boulder_processing/refinery.dm:153
    public virtual void update_light_value()
    {
        Waiting.NoSleep(set_light_on(!Dm.truthy(panel_open) && Dm.truthy(anchored) && Dm.truthy(is_operational) ? TRUE : FALSE));
    }

    // tg: code/modules/mining/boulder_processing/refinery.dm:156
    public override void on_set_anchored(atom_movable source = null, object anchorvalue = null)
    {
        update_light_value();
    }

    // tg: code/modules/mining/boulder_processing/refinery.dm:159
    public override async DmTask on_set_is_operational(object old_value = null)
    {
        update_light_value();
        await DmTask.CompletedTask;
    }

    // tg: code/modules/mining/boulder_processing/refinery.dm:162
    public override void on_set_panel_open(object old_value = null)
    {
        update_light_value();
    }

    // tg: code/modules/mining/boulder_processing/refinery.dm:165
    public override async DmTask gib_mob(mob_living rockman = null)
    {
        rockman.visible_message(span_warning(I18n.T("[] is processed by []!", rockman, this)), span_userdanger(I18n.T("You get melted into rock by []!", this)));
        rockman.investigate_log(Dm.text("was melted by [] for having the MOB_MINERAL mob biotype", this), INVESTIGATE_DEATHS);
        await rockman.dust();
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mining/boulder_processing/refinery.dm:33  var/static/list/booster_reagents
    internal DmList obj_machinery_bouldertech_refinery__get_booster_reagents__booster_reagents;
    // tg: code/modules/mining/boulder_processing/refinery.dm:44  var/static/list/processable_materials
    internal DmList obj_machinery_bouldertech_refinery__can_process_material__processable_materials;
    // tg: code/modules/mining/boulder_processing/refinery.dm:131  var/static/list/booster_reagents
    internal DmList obj_machinery_bouldertech_refinery_smelter__get_booster_reagents__booster_reagents;
    // tg: code/modules/mining/boulder_processing/refinery.dm:142  var/static/list/processable_materials
    internal DmList obj_machinery_bouldertech_refinery_smelter__can_process_material__processable_materials;
}
