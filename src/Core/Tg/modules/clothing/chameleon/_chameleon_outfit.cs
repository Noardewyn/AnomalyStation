// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/chameleon/_chameleon_outfit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign G1b (D316): the whole file, the outfit action every holder of a chameleon item gets (a job's outfit
// applied to all their chameleon items at once, custom outfits saved with the right click). The var block is generated (D111).

public partial class datum_action_chameleon_outfit : datum_action
{
    // tg: code/modules/clothing/chameleon/_chameleon_outfit.dm:20
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        outfit_options = get_initial_outfits();
    }

    // tg: code/modules/clothing/chameleon/_chameleon_outfit.dm:24
    public virtual DmList get_initial_outfits()
    {
        DmList standard_outfit_options = TgState.Current.chameleon_outfit_standard_outfit_options;
        if (standard_outfit_options == null)
        {
            standard_outfit_options = Dm.list();
            foreach (Type found_outfit in Dm.each<Type>(subtypesof(typeof(datum_outfit_job))))
                standard_outfit_options[Dm.initial(found_outfit, (datum_outfit p) => p.name)] = Dm.@new(found_outfit);
            sortTim(standard_outfit_options, nameof(cmp_text_asc));
            TgState.Current.chameleon_outfit_standard_outfit_options = standard_outfit_options;
        }

        return standard_outfit_options;
    }

    // tg: code/modules/clothing/chameleon/_chameleon_outfit.dm:34
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot) || Dm.truthy(applying))
            return dot;
        if (usr != owner) // i guess
            return dot;
        applying = TRUE;

        if (Dm.truthy(Dm.bitand(trigger_flags, TRIGGER_SECONDARY_ACTION)))
            dot = await save_current_outfit(usr);
        else
            dot = await select_outfit(usr);

        applying = FALSE;
        return dot;
    }

    // tg: code/modules/clothing/chameleon/_chameleon_outfit.dm:50
    public virtual async DmTask<object> save_current_outfit(mob user = null)
    {
        DmList saved_paths = Dm.list();
        foreach (datum_action_item_action_chameleon_change change_action in Dm.each<datum_action_item_action_chameleon_change>(owner.actions))
        {
            if (change_action.active_type != null)
                Dm.combine(ref saved_paths, change_action.active_type);
        }

        save_outfit(user, saved_paths);
        return null;
    }

    // tg: code/modules/clothing/chameleon/_chameleon_outfit.dm:58
    public virtual void save_outfit(mob user = null, DmList saved_paths = null)
    {
        if (Dm.length(saved_paths) == 0)
        {
            owner.balloon_alert(user, "no outfit saved!");
            return;
        }

        foreach (object existing_outfit in Dm.each(custom_outfits))
        {
            if (Dm.equiv(custom_outfits[existing_outfit], saved_paths))
            {
                owner.balloon_alert(user, "outfit already saved!");
                return;
            }
        }

        if (next_custom_outfit > max_custom_oufits)
            next_custom_outfit = 1;

        custom_outfits ??= Dm.list(); // LAZYSET
        custom_outfits[Dm.text("Custom Outfit #[]", next_custom_outfit)] = saved_paths;
        owner.balloon_alert(user, Dm.text("outfit saved in slot []", next_custom_outfit));
        next_custom_outfit += 1;
    }

    // tg: code/modules/clothing/chameleon/_chameleon_outfit.dm:75
    public virtual async DmTask<object> select_outfit(mob user = null)
    {
        DmList all_options = Dm.list();
        if (Dm.length(custom_outfits) > 0)
        {
            Dm.append(all_options, "--- Custom outfits ---");
            Dm.append(all_options, custom_outfits);
        }
        Dm.append(all_options, "--- Job outfits ---");
        Dm.append(all_options, outfit_options);

        object selected = await tgui_input_list(user, "Select outfit to change into", "Chameleon Outfit", all_options);
        if (selected == null || QDELETED(this) || QDELETED(user) || QDELETED(owner) || !Dm.truthy(await IsAvailable(feedback: TRUE)))
            return FALSE;

        object selected_outfit = all_options[selected];
        if (selected_outfit is DmList selected_custom_outfit)
        {
            var empty_outfit = Dm.@new<datum_outfit>();
            await apply_outfit(empty_outfit, selected_custom_outfit.Copy());
            qdel(empty_outfit);
            return TRUE;
        }

        if (selected_outfit is datum_outfit outfit)
        {
            await apply_outfit(outfit);
            return TRUE;
        }

        return FALSE;
    }

    /**
     * Applies the given outfit to all chameleon actions the owner has
     *
     * * outfit - what outfit to apply
     * * outfit_types - optinal, list of typepaths to apply. If null, defaults to all items in the passed outfit. This list is mutated!
     */
    // tg: code/modules/clothing/chameleon/_chameleon_outfit.dm:107
    public virtual async DmTask apply_outfit(datum_outfit outfit = null, DmList outfit_types = null)
    {
        outfit_types ??= outfit.get_chameleon_disguise_info() as DmList;

        foreach (datum_action_item_action_chameleon_change change_action in Dm.each<datum_action_item_action_chameleon_change>(owner.actions))
        {
            await change_action.apply_outfit(outfit, outfit_types);
            if (change_action.target is not obj_item cham_item)
                continue;
            if (!Dm.truthy(cham_item.slot_flags))
                continue;
            // Clothing items get a small delay to create a visual effect and also introduce some counterplay and also distinguish from lings
            await stoplag(0.1f * SECONDS);
        }
    }
}

public sealed partial class TgState
{
    // tg: code/modules/clothing/chameleon/_chameleon_outfit.dm:25  get_initial_outfits' var/static/list/standard_outfit_options
    internal DmList chameleon_outfit_standard_outfit_options;
}
