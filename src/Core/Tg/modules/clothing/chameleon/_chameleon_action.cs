// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/chameleon/_chameleon_action.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign G1b (D316): the whole file, the chameleon item's change action (the disguise list, the look copied from
// the picked type with its greyscale icons, the EMP's random looks, the outfit's application). The var block is generated (D111).

public partial class datum_action_item_action_chameleon_change : datum_action_item_action_chameleon
{
    /// Default duration of an EMP randomisation on a chameleon item
    // tg: code/modules/clothing/chameleon/_chameleon_action.dm:2  #define EMP_RANDOMISE_TIME 30 SECONDS (file-local)
    private const float EMP_RANDOMISE_TIME = 30 * SECONDS;

    // tg: code/modules/clothing/chameleon/_chameleon_action.dm:27
    [DmParams("Target")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (target is not obj_item)
        {
            stack_trace(Dm.text("Adding chameleon action to non-item ([])", target), "code/modules/clothing/chameleon/_chameleon_action.dm", 30);
            qdel(this);
            return;
        }

        name = Dm.text("Change [] Appearance", chameleon_name);
        await build_all_button_icons();

        chameleon_blacklist ??= Dm.list(); // LAZYINITLIST
        chameleon_typecache ??= Dm.list();
        chameleon_list ??= Dm.list();

        await initialize_blacklist();
        await initialize_disguises();

        if (active_type != null)
        {
            if (Dm.truthy(is_type_in_typecache(active_type, chameleon_blacklist)))
            {
                stack_trace(Dm.text("[] has an active type defined in init which is blacklisted ([])", type, active_type), "code/modules/clothing/chameleon/_chameleon_action.dm", 45);
                active_type = null;
            }
            else
                await update_look(active_type);
        }

        RegisterSignal(target, COMSIG_ATOM_EMP_ACT, nameof(on_emp));
    }

    // tg: code/modules/clothing/chameleon/_chameleon_action.dm:53
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSprocessing, this);
        return base.Destroy(force);
    }

    // tg: code/modules/clothing/chameleon/_chameleon_action.dm:57
    [SignalHandler]
    public virtual void on_emp(datum source = null, object severity = null, object protection = null)
    {
        if (Dm.truthy(Dm.bitand(protection, EMP_PROTECT_SELF)))
            return;
        Waiting.NoSleep(emp_randomise());
    }

    // tg: code/modules/clothing/chameleon/_chameleon_action.dm:63
    public override async DmTask<object> Grant(mob grant_to = null, params object[] __extra)
    {
        object dot = await base.Grant(grant_to, __extra);
        if (owner == null)
            return dot;

        // Whenever a mob gains their first cham change action, they need to also gain the outfit action
        if (Dm.locate<datum_action_chameleon_outfit>(grant_to.actions) != null)
            return dot;

        var outfit_action = Dm.@new<datum_action_chameleon_outfit>(owner);
        await outfit_action.Grant(owner);
        return dot;
    }

    // tg: code/modules/clothing/chameleon/_chameleon_action.dm:75
    public override async DmTask Remove(mob remove_from = null)
    {
        await base.Remove(remove_from);
        // Likewise when the mob loses the cham change action, if they have no others, they need to lose the outfit action
        if (Dm.locate<datum_action_item_action_chameleon_change>(remove_from.actions) != null)
            return;

        var outfit_action = Dm.locate<datum_action_chameleon_outfit>(remove_from.actions) as datum_action_chameleon_outfit;
        qdel(outfit_action); // QDEL_NULL
    }

    /// Basic initialization of the chameleon items we cannot pick from
    // tg: code/modules/clothing/chameleon/_chameleon_action.dm:85
    public virtual async DmTask initialize_blacklist()
    {
        Dm.combine(ref chameleon_blacklist, typecacheof(target.type));
    }

    /// Basic initialization of the chameleon items we can pick from
    // tg: code/modules/clothing/chameleon/_chameleon_action.dm:89
    public virtual async DmTask initialize_disguises()
    {
        if (!Dm.ispath(chameleon_type, typeof(obj_item)))
        {
            stack_trace(Dm.text("Non-item chameleon type defined on [] ([])", type, chameleon_type), "code/modules/clothing/chameleon/_chameleon_action.dm", 90);
            return;
        }

        add_chameleon_items(chameleon_type);
    }

    /// Used for formatting a typepath into something human readable for selection
    // tg: code/modules/clothing/chameleon/_chameleon_action.dm:97
    public virtual object format_readable_name(object format_type = null)
    {
        if (format_type is Type format_item && Dm.ispath(format_item, typeof(obj_item)))
        {
            object state = Dm.initial(format_item, (atom p) => p.post_init_icon_state);
            if (!Dm.truthy(state))
                state = Dm.initial(format_item, (atom p) => p.icon_state);
            return Dm.text("[] ([])", Dm.initial(format_item, (atom p) => p.name), replacetext(state, "_", " "));
        }

        return Dm.text("[]", format_type);
    }

    /**
     * Adds items of the given type or types to the chameleon selection list
     *
     * * type_or_types_to_add: A single typepath or a list of typepaths to add to the chameleon selection
     * * only_root: If TRUE, only add the literal type or types passed, not their children
     * * ignore_root: If TRUE, only add children of the type or types passed, not the literal types
     */
    // tg: code/modules/clothing/chameleon/_chameleon_action.dm:111
    public virtual void add_chameleon_items(object type_or_types_to_add = null, float only_root = FALSE, float ignore_root = FALSE)
    {
        DmList new_items = typecacheof(type_or_types_to_add, only_root_path: only_root, ignore_root_path: ignore_root) as DmList;
        foreach (Type item_type in Dm.each<Type>(Dm.sub(new_items, chameleon_typecache)))
        {
            if (Dm.truthy(is_type_in_typecache(item_type, chameleon_blacklist))
                || Dm.truthy(Dm.bitand(Dm.initial(item_type, (obj_item p) => p.item_flags), ABSTRACT))
                || item_type == Dm.initial(item_type, (datum p) => p.abstract_type)
                || !Dm.truthy(Dm.initial(item_type, (atom p) => p.icon_state)))
                continue;
            chameleon_list[format_readable_name(item_type)] = item_type;
        }
        Dm.combine(ref chameleon_typecache, new_items);
    }

    // tg: code/modules/clothing/chameleon/_chameleon_action.dm:119
    public virtual async DmTask select_look(mob user = null)
    {
        object picked_name = await tgui_input_list(user, Dm.text("Select [] to change into", chameleon_name), "Chameleon Settings", sort_list(chameleon_list, nameof(cmp_typepaths_asc)) as DmList);
        if (picked_name == null || chameleon_list[picked_name] == null || QDELETED(this) || QDELETED(user) || QDELETED(owner) || !Dm.truthy(await IsAvailable(feedback: TRUE)))
            return;
        Type picked_item = chameleon_list[picked_name] as Type;
        await update_look(picked_item);
    }

    // tg: code/modules/clothing/chameleon/_chameleon_action.dm:126
    public virtual async DmTask random_look()
    {
        object picked_name = pick(chameleon_list);
        await update_look(chameleon_list[picked_name] as Type);
    }

    // tg: code/modules/clothing/chameleon/_chameleon_action.dm:130
    public virtual async DmTask update_look(Type picked_item = null)
    {
        obj_item chameleon_item = target as obj_item;

        await update_item(picked_item);
        await build_all_button_icons();
        active_type = picked_item;

        if (chameleon_item.loc is mob wearer)
        {
            await wearer.update_clothing(Dm.bitor(chameleon_item.slot_flags, ITEM_SLOT_HANDS));
            wearer.refresh_obscured();
        }
    }

    // tg: code/modules/clothing/chameleon/_chameleon_action.dm:142
    protected internal virtual async DmTask update_item(Type picked_item = null)
    {
        // PROTECTED_PROC(TRUE): call update_look, not this!
        atom atom_target = target as atom;
        atom_target.name = Dm.initial(picked_item, (atom p) => p.name);
        atom_target.desc = Dm.initial(picked_item, (atom p) => p.desc);
        object post_init_state = Dm.initial(picked_item, (atom p) => p.post_init_icon_state);
        atom_target.icon_state = Dm.truthy(post_init_state) ? Dm.str(post_init_state) : Dm.initial(picked_item, (atom p) => p.icon_state);

        if (atom_target is obj_item item_target)
        {
            item_target.worn_icon = Dm.initial(picked_item, (obj_item p) => p.worn_icon);
            item_target.lefthand_file = Dm.initial(picked_item, (obj_item p) => p.lefthand_file);
            item_target.righthand_file = Dm.initial(picked_item, (obj_item p) => p.righthand_file);

            item_target.worn_icon_state = Dm.initial(picked_item, (obj_item p) => p.worn_icon_state);
            item_target.inhand_icon_state = Dm.initial(picked_item, (obj_item p) => p.inhand_icon_state);

            object greyscale_colors = Dm.initial(picked_item, (atom p) => p.greyscale_colors);
            if (Dm.truthy(greyscale_colors))
            {
                if (Dm.truthy(Dm.initial(picked_item, (obj_item p) => p.greyscale_config_worn)))
                    item_target.worn_icon = SSgreyscale.GetColoredIconByType(Dm.initial(picked_item, (obj_item p) => p.greyscale_config_worn), greyscale_colors);
                if (Dm.truthy(Dm.initial(picked_item, (obj_item p) => p.greyscale_config_inhand_left)))
                    item_target.lefthand_file = SSgreyscale.GetColoredIconByType(Dm.initial(picked_item, (obj_item p) => p.greyscale_config_inhand_left), greyscale_colors);
                if (Dm.truthy(Dm.initial(picked_item, (obj_item p) => p.greyscale_config_inhand_right)))
                    item_target.righthand_file = SSgreyscale.GetColoredIconByType(Dm.initial(picked_item, (obj_item p) => p.greyscale_config_inhand_right), greyscale_colors);
            }

            item_target.flags_inv = Dm.initial(picked_item, (obj_item p) => p.flags_inv);
            item_target.hair_mask = Dm.initial(picked_item, (obj_item p) => p.hair_mask);
            item_target.transparent_protection = Dm.initial(picked_item, (obj_item p) => p.transparent_protection);
            if (item_target is obj_item_clothing clothing_target && Dm.ispath(picked_item, typeof(obj_item_clothing)))
                clothing_target.flags_cover = Dm.initial(picked_item, (obj_item_clothing p) => p.flags_cover);
        }

        if (Dm.truthy(Dm.initial(picked_item, (atom p) => p.greyscale_config)) && Dm.truthy(Dm.initial(picked_item, (atom p) => p.greyscale_colors)))
            atom_target.icon = SSgreyscale.GetColoredIconByType(Dm.initial(picked_item, (atom p) => p.greyscale_config), Dm.initial(picked_item, (atom p) => p.greyscale_colors));
        else
            atom_target.icon = Dm.initial(picked_item, (atom p) => p.icon);
    }

    // tg: code/modules/clothing/chameleon/_chameleon_action.dm:194
    public override async DmTask<object> do_effect(object trigger_flags = null)
    {
        await select_look(owner);
        return TRUE;
    }

    // tg: code/modules/clothing/chameleon/_chameleon_action.dm:198
    public virtual async DmTask emp_randomise(float amount = EMP_RANDOMISE_TIME)
    {
        START_PROCESSING(SSprocessing, this);
        await random_look();

        emp_timer = Dm.world.time + amount; // COOLDOWN_START, inline
    }

    // tg: code/modules/clothing/chameleon/_chameleon_action.dm:204
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (emp_timer <= Dm.world.time) // COOLDOWN_FINISHED, inline
        {
            STOP_PROCESSING(SSprocessing, this);
            return null;
        }
        Waiting.NoSleep(random_look());
        return null;
    }

    // tg: code/modules/clothing/chameleon/_chameleon_action.dm:210
    [ShouldCallParent]
    public virtual async DmTask<object> apply_outfit(datum_outfit applying_from = null, DmList all_items_to_apply = null)
    {
        Type using_item_type = null;
        foreach (object item_type in Dm.each(all_items_to_apply))
        {
            if (!Dm.ispath(item_type, typeof(obj_item)))
            {
                stack_trace(Dm.text("Invalid item type passed to apply_outfit ([])", item_type), "code/modules/clothing/chameleon/_chameleon_action.dm", 211);
                continue;
            }
            if (Dm.truthy(chameleon_typecache[item_type]))
            {
                using_item_type = item_type as Type;
                break;
            }
        }

        if (using_item_type == null)
            return FALSE;

        if (applying_from is datum_outfit_job job_outfit)
        {
            datum_job job_datum = SSjob.get_job_type(job_outfit.jobtype) as datum_job;
            await apply_job_data(job_datum);
        }

        await update_look(using_item_type);
        Dm.remove(all_items_to_apply, using_item_type);
        return TRUE;
    }

    /// Used when applying this cham item via a job datum (from an outfit selection)
    // tg: code/modules/clothing/chameleon/_chameleon_action.dm:235
    public virtual async DmTask apply_job_data(datum_job job_datum = null)
    {
        return;
    }
}
