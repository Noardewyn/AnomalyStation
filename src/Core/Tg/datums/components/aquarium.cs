// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/aquarium.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: what makes an atom an aquarium. It keeps the fish and the props put in it, their beauty, the fluid and its
// temperature (set by hand, by the assisted mode from the fish inside, or held safe by the stasis mode), the feed storage that feeds
// the fish at an interval, the fish's breeding candidates and evolution conditions, the layers of their visuals, admiring it for a
// mood, plunging the feed out, and the Aquarium window. The var blocks are generated (D111).

public sealed partial class TgState
{
    // tg: code/datums/components/aquarium.dm:49  var/static/list/fluid_types
    internal DmList datum_component_aquarium__fluid_types;
    // tg: code/datums/components/aquarium.dm:56  var/static/list/aquarium_modes
    internal DmList datum_component_aquarium__aquarium_modes;
}

/**
 * The component that manages the aquariums UI, fluid, temperature, the current fish inside the parent object, as well as beauty,
 * and a few other common aquarium features.
 */
// tg: code/datums/components/aquarium.dm:9
public partial class datum_component_aquarium : datum_component
{
    ///Defines that clamp the beauty of the aquarium, to prevent it from making most areas great or horrid all by itself.
    // tg: code/datums/components/aquarium.dm:2  #define MIN_AQUARIUM_BEAUTY -3500 (file-local)
    private const float MIN_AQUARIUM_BEAUTY = -3500;
    // tg: code/datums/components/aquarium.dm:3  #define MAX_AQUARIUM_BEAUTY 6000 (file-local)
    private const float MAX_AQUARIUM_BEAUTY = 6000;

    ///static list of available fluid types.
    // tg: code/datums/components/aquarium.dm:49 (rules 10.6)
    public static DmList fluid_types => TgState.Current.datum_component_aquarium__fluid_types ??= Dm.list(
        AQUARIUM_FLUID_SALTWATER,
        AQUARIUM_FLUID_FRESHWATER,
        AQUARIUM_FLUID_SULPHWATEVER,
        AQUARIUM_FLUID_AIR);

    ///static list of available aquarium modes
    // tg: code/datums/components/aquarium.dm:56 (rules 10.6)
    public static DmList aquarium_modes => TgState.Current.datum_component_aquarium__aquarium_modes ??= Dm.list(
        Dm.kv(AQUARIUM_MODE_MANUAL, "Take full control of fluid and temperature settings. There's no stasis option here."),
        Dm.kv(AQUARIUM_MODE_AUTO, "Let an internal processor regulate aquarium settings based on its fish population. Can temporarily fall back to stasis."),
        Dm.kv(AQUARIUM_MODE_SAFE, "Prevent fish from dying from wrong fluid/temperature settings and hunger, but also stops growth and reproduction"));

    // tg: code/datums/components/aquarium.dm:68
    [DmParams("min_px", "max_px", "min_py", "max_py", "default_beauty", "reagents_size", "min_fluid_temp", "max_fluid_temp", "init_mode")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object min_px = Dm.arg(__extra, 0);
        object max_px = Dm.arg(__extra, 1);
        object min_py = Dm.arg(__extra, 2);
        object max_py = Dm.arg(__extra, 3);
        object default_beauty = Dm.arg(__extra, 4) ?? 0f;
        object reagents_size = Dm.arg(__extra, 5) ?? 6f;
        float min_fluid_temp = Dm.arg<float?>(__extra, 6) ?? MIN_AQUARIUM_TEMP;
        float max_fluid_temp = Dm.arg<float?>(__extra, 7) ?? MAX_AQUARIUM_TEMP;
        object init_mode = Dm.arg(__extra, 8) ?? AQUARIUM_MODE_AUTO;

        if (!ismovable(parent))
            return COMPONENT_INCOMPATIBLE;

        this.default_beauty = default_beauty;
        this.reagents_size = reagents_size;

        aquarium_zone_min_pw = min_px;
        aquarium_zone_max_pw = max_px;
        aquarium_zone_min_pz = min_py;
        aquarium_zone_max_pz = max_py;

        this.min_fluid_temp = min_fluid_temp;
        this.max_fluid_temp = max_fluid_temp;
        fluid_temp = clamp(fluid_temp, min_fluid_temp, max_fluid_temp);

        await set_aquarium_mode(init_mode);
        return null;
    }

    // tg: code/datums/components/aquarium.dm:97
    public override async DmTask RegisterWithParent()
    {
        if (Dm.truthy(default_beauty))
            await update_aquarium_beauty(0f);

        RegisterSignals(parent, Dm.list(COMSIG_ATOM_ENTERED, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON), nameof(on_entered));
        RegisterSignal(parent, COMSIG_ATOM_EXITED, nameof(on_exited));

        RegisterSignal(parent, COMSIG_AQUARIUM_GET_REPRODUCTION_CANDIDATES, nameof(get_candidates));
        RegisterSignal(parent, COMSIG_AQUARIUM_CHECK_EVOLUTION_CONDITIONS, nameof(check_evolution));

        RegisterSignal(parent, COMSIG_AQUARIUM_SET_VISUAL, nameof(set_visual));
        RegisterSignal(parent, COMSIG_AQUARIUM_REMOVE_VISUAL, nameof(remove_visual));

        atom_movable movable = (atom_movable)parent;

        ADD_KEEP_TOGETHER(movable, AQUARIUM_TRAIT); //render the fish on the same layer of the aquarium.

        if (Dm.num(reagents_size) > 0)
        {
            if (movable.reagents == null)
                movable.create_reagents(reagents_size, SEALED_CONTAINER);
            if (Dm.truthy(((datum_reagents)movable.reagents).total_volume))
                start_autofeed(((datum_reagents)movable.reagents));
            else
                RegisterSignal(((datum_reagents)movable.reagents), COMSIG_REAGENTS_HOLDER_UPDATED, nameof(start_autofeed));
            RegisterSignal(movable, COMSIG_PLUNGER_ACT, nameof(on_plunger_act));
        }

        RegisterSignal(movable, COMSIG_ATOM_ITEM_INTERACTION, nameof(on_item_interaction));
        RegisterSignal(movable, COMSIG_CLICK_ALT, nameof(on_click_alt));
        RegisterSignal(movable, COMSIG_ATOM_EXAMINE, nameof(on_examine));

        if (isitem(movable))
        {
            RegisterSignal(movable, COMSIG_ITEM_ATTACK_SELF, nameof(interact));
            RegisterSignal(movable, COMSIG_ITEM_ATTACK_SELF_SECONDARY, nameof(secondary_interact));
            RegisterSignals(movable, Dm.list(COMSIG_ATOM_ATTACK_ROBOT_SECONDARY, COMSIG_ATOM_ATTACK_HAND_SECONDARY), nameof(on_secondary_attack_hand));
        }
        else
        {
            RegisterSignal(movable, COMSIG_ATOM_UI_INTERACT, nameof(interact));
        }

        await movable.AddElement(typeof(datum_element_relay_attackers));
        await movable.AddComponent(typeof(datum_component_fishing_spot), typeof(datum_fish_source_aquarium));

        Dm.combine(ref movable.flags_1, HAS_CONTEXTUAL_SCREENTIPS_1);
        RegisterSignal(movable, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM, nameof(on_requesting_context_from_item));

        foreach (atom_movable content in Dm.each<atom_movable>(movable.contents))
        {
            if (Dm.truthy(Dm.bitand(content.flags_1, INITIALIZED_1)))
                on_entered(movable, content);
        }

        movable.add_traits(Dm.list(TRAIT_IS_AQUARIUM, TRAIT_STOP_FISH_FLOPPING), AQUARIUM_TRAIT);
    }

    // tg: code/datums/components/aquarium.dm:147
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        atom_movable movable = (atom_movable)parent;
        UnregisterSignal(movable, Dm.list(
            COMSIG_ATOM_ENTERED,
            COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON,
            COMSIG_ATOM_EXITED,
            COMSIG_AQUARIUM_GET_REPRODUCTION_CANDIDATES,
            COMSIG_AQUARIUM_CHECK_EVOLUTION_CONDITIONS,
            COMSIG_AQUARIUM_SET_VISUAL,
            COMSIG_AQUARIUM_REMOVE_VISUAL,
            COMSIG_PLUNGER_ACT,
            COMSIG_ATOM_ITEM_INTERACTION,
            COMSIG_CLICK_ALT,
            COMSIG_ATOM_EXAMINE,
            COMSIG_ITEM_ATTACK_SELF,
            COMSIG_ATOM_ATTACK_ROBOT_SECONDARY,
            COMSIG_ATOM_ATTACK_HAND_SECONDARY,
            COMSIG_ATOM_UI_INTERACT,
            COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM));
        if (((datum_reagents)movable.reagents) != null)
            UnregisterSignal(movable, COMSIG_REAGENTS_HOLDER_UPDATED);
        // tg stops the processing only while the reagents are there; on a deletion the atom's Destroy has dropped them
        // first and SSobj drops the deleted component on its next fire. It is stopped here either way.
        STOP_PROCESSING(SSobj, this);
        beauty_by_content = null;
        tracked_fish_by_type = null;
        movable.remove_traits(Dm.list(TRAIT_IS_AQUARIUM, TRAIT_AQUARIUM_PANEL_OPEN, TRAIT_STOP_FISH_REPRODUCTION_AND_GROWTH, TRAIT_STOP_FISH_FLOPPING), AQUARIUM_TRAIT);
        qdel(movable.GetComponent(typeof(datum_component_fishing_spot)));
        REMOVE_KEEP_TOGETHER(movable, AQUARIUM_TRAIT);
        await DmTask.CompletedTask;
    }

    // tg: code/datums/components/aquarium.dm:176
    public override async DmTask PreTransfer(datum new_parent = null)
    {
        if (new_parent is not atom_movable new_movable)
            return;
        if (HAS_TRAIT(parent, TRAIT_AQUARIUM_PANEL_OPEN))
            ADD_TRAIT(new_movable, TRAIT_AQUARIUM_PANEL_OPEN, AQUARIUM_TRAIT);
        if (HAS_TRAIT_FROM(parent, TRAIT_STOP_FISH_REPRODUCTION_AND_GROWTH, AQUARIUM_TRAIT))
            ADD_TRAIT(new_movable, TRAIT_STOP_FISH_REPRODUCTION_AND_GROWTH, AQUARIUM_TRAIT);
        atom_movable movable = (atom_movable)parent;
        object old_mode = current_mode; //Make sure we don't keep updating temp/fluid on AQUARIUM_MODE_AUTO for this.
        current_mode = NONE;
        foreach (atom_movable moving in Dm.each<atom_movable>(movable.contents))
        {
            if (HAS_TRAIT(moving, TRAIT_AQUARIUM_CONTENT))
                await moving.forceMove(new_movable);
        }
        current_mode = old_mode;
        if (Dm.truthy(reagents_size))
        {
            if (((datum_reagents)new_movable.reagents) == null)
                new_movable.create_reagents(reagents_size, SEALED_CONTAINER);
            await ((datum_reagents)movable.reagents).trans_to(new_movable, Dm.num(((datum_reagents)movable.reagents).total_volume));
        }
    }

    // tg: code/datums/components/aquarium.dm:195
    public override async DmTask<object> PostTransfer(datum new_parent = null)
    {
        await DmTask.CompletedTask;
        if (!ismovable(new_parent))
            return COMPONENT_INCOMPATIBLE;
        return null;
    }

    // tg: code/datums/components/aquarium.dm:199
    public override async DmTask InheritComponent(datum_component C = null, object i_am_original = null, params object[] __extra)
    {
        datum_component_aquarium new_comp = (datum_component_aquarium)C;
        fluid_temp = clamp(new_comp.fluid_temp, min_fluid_temp, max_fluid_temp);
        set_fluid_type(new_comp.fluid_type);
        feeding_interval = new_comp.feeding_interval;
        last_feeding = new_comp.last_feeding;
        atom_movable movable = (atom_movable)parent;
        movable.update_appearance();
        await DmTask.CompletedTask;
    }

    // tg: code/datums/components/aquarium.dm:207
    [SignalHandler]
    public virtual object on_click_alt(atom_movable source = null, mob_living user = null)
    {
        if (!Dm.truthy(Waiting.NoSleep(user.can_perform_action(source))))
            return null;
        bool closing = HAS_TRAIT(parent, TRAIT_AQUARIUM_PANEL_OPEN);
        if (closing)
        {
            REMOVE_TRAIT(parent, TRAIT_AQUARIUM_PANEL_OPEN, AQUARIUM_TRAIT);
            ((datum_reagents)source.reagents).flags = Dm.bitand(((datum_reagents)source.reagents).flags, Dm.bitnot(Dm.bitor(TRANSPARENT, REFILLABLE)));
            SStgui.close_uis(this);
        }
        else
        {
            ADD_TRAIT(parent, TRAIT_AQUARIUM_PANEL_OPEN, AQUARIUM_TRAIT);
            ((datum_reagents)source.reagents).flags = Dm.bitor(((datum_reagents)source.reagents).flags, Dm.bitor(TRANSPARENT, REFILLABLE));
        }

        source.balloon_alert(user, closing ? I18n.T("panel closed") : I18n.T("panel open"));
        source.update_appearance();
        return CLICK_ACTION_SUCCESS;
    }

    ///This proc handles feeding the aquarium and inserting aquarium content.
    // tg: code/datums/components/aquarium.dm:225
    [SignalHandler]
    public virtual object on_item_interaction(atom_movable source = null, mob_living user = null, obj_item item = null, object modifiers = null)
    {
        if (istype(item, typeof(obj_item_reagent_containers_cup_fish_feed)))
        {
            if (((datum_reagents)source.reagents) != null && HAS_TRAIT(source, TRAIT_AQUARIUM_PANEL_OPEN))
                return null; //don't block, we'll be transferring reagents to the feed storage.
            if (!Dm.truthy(((datum_reagents)item.reagents).total_volume))
            {
                source.balloon_alert(user, I18n.T("[] is empty!", item));
                return ITEM_INTERACT_BLOCKING;
            }
            DmList fishes = get_fishes() as DmList;
            if (!Dm.truthy(length(fishes)))
            {
                source.balloon_alert(user, I18n.T("no fish to feed!"));
                return ITEM_INTERACT_BLOCKING;
            }
            Waiting.NoSleep(feed_fishes(item, fishes));
            source.balloon_alert(user, I18n.T("fed the fish"));
            return ITEM_INTERACT_SUCCESS;
        }

        if (!HAS_TRAIT(item, TRAIT_AQUARIUM_CONTENT) || (!isitem(parent) && Dm.truthy(user.combat_mode)))
            return null; //proceed with normal interactions

        bool broken = Dm.num(source.get_integrity_percentage()) <= Dm.num(source.integrity_failure);
        if (!Dm.truthy(can_insert(source, item, user)))
            return ITEM_INTERACT_BLOCKING;
        if (broken)
        {
            source.balloon_alert(user, I18n.T("aquarium is broken!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(Waiting.NoSleep(user.transferItemToLoc(item, source))))
        {
            user.balloon_alert(user, I18n.T("stuck to your hand!"));
            return ITEM_INTERACT_BLOCKING;
        }
        source.balloon_alert(user, I18n.T("added to aquarium"));
        source.update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    ///Called when the feed storage is no longer empty.
    // tg: code/datums/components/aquarium.dm:259
    [SignalHandler]
    public virtual void start_autofeed(datum_reagents source = null)
    {
        UnregisterSignal(source, COMSIG_REAGENTS_HOLDER_UPDATED);
        START_PROCESSING(SSobj, this);
    }

    ///Feed the fish at defined intervals until the feed storage is empty.
    // tg: code/datums/components/aquarium.dm:265
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        //safe mode, no need to feed the fishes
        if (HAS_TRAIT_FROM(parent, TRAIT_STOP_FISH_REPRODUCTION_AND_GROWTH, AQUARIUM_TRAIT))
        {
            last_feeding = Dm.num(last_feeding) + Dm.num(seconds_per_tick) * SECONDS;
            return null;
        }
        atom_movable movable = (atom_movable)parent;
        if (!Dm.truthy(((datum_reagents)movable.reagents)?.total_volume))
        {
            if (((datum_reagents)movable.reagents) != null)
                RegisterSignal(((datum_reagents)movable.reagents), COMSIG_REAGENTS_HOLDER_UPDATED, nameof(start_autofeed));
            return PROCESS_KILL;
        }
        if (world.time < Dm.num(last_feeding) + feeding_interval)
            return null;
        last_feeding = world.time;
        Waiting.NoSleep(feed_fishes(movable));
        return null;
    }

    // tg: code/datums/components/aquarium.dm:280
    public virtual async DmTask feed_fishes(atom_movable movable = null, DmList fishes = null)
    {
        fishes ??= get_fishes() as DmList;
        foreach (obj_item_fish fish in Dm.each<obj_item_fish>(fishes))
            await fish.feed(((datum_reagents)movable.reagents));
        if (Dm.eq(current_mode, AQUARIUM_MODE_AUTO) && Dm.truthy(await is_fish_population_safe()))
            REMOVE_TRAIT(parent, TRAIT_STOP_FISH_REPRODUCTION_AND_GROWTH, AQUARIUM_TRAIT);
    }

    // tg: code/datums/components/aquarium.dm:286
    [SignalHandler]
    public virtual object on_plunger_act(atom_movable source = null, obj_item_plunger plunger = null, mob_living user = null, object reinforced = null)
    {
        if (!HAS_TRAIT(source, TRAIT_AQUARIUM_PANEL_OPEN))
        {
            source.balloon_alert(user, I18n.T("open panel first!"));
            return null;
        }
        Waiting.Detach(do_plunging(source, user)); // INVOKE_ASYNC(src, PROC_REF(do_plunging), source, user)
        return COMPONENT_NO_AFTERATTACK;
    }

    // tg: code/datums/components/aquarium.dm:294
    public virtual async DmTask do_plunging(atom_movable source = null, mob_living user = null)
    {
        user.balloon_alert_to_viewers(I18n.T("plunging..."));
        if (Dm.truthy(await do_after(user, 3 * SECONDS, target: source)))
        {
            user.balloon_alert_to_viewers(I18n.T("finished plunging"));
            await ((datum_reagents)source.reagents).expose(get_turf(source), TOUCH); //splash on the floor
            await ((datum_reagents)source.reagents).clear_reagents();
        }
    }

    // tg: code/datums/components/aquarium.dm:301
    [SignalHandler]
    public virtual void on_examine(atom_movable source = null, mob user = null, DmList examine_list = null)
    {
        Dm.append(examine_list, span_notice(I18n.T("Its temperature and fluid are currently set to [] and [].", EXAMINE_HINT(Dm.text("[] K", fluid_temp)), EXAMINE_HINT(fluid_type))));
        bool panel_open = HAS_TRAIT(source, TRAIT_AQUARIUM_PANEL_OPEN);
        Dm.append(examine_list, span_notice(panel_open
            ? I18n.T("[] to close the control and feed panel.", EXAMINE_HINT(I18n.T("Alt-click")))
            : I18n.T("[] to open the control and feed panel.", EXAMINE_HINT(I18n.T("Alt-click")))));
        if (panel_open && Dm.truthy(((datum_reagents)source.reagents).total_volume))
            Dm.append(examine_list, span_notice(I18n.T("You can use a plunger to empty the feed storage.")));
    }

    ///Check if an item can be inserted into the aquarium
    // tg: code/datums/components/aquarium.dm:310
    public virtual object can_insert(atom_movable source = null, obj_item item = null, mob_living user = null)
    {
        // DEVIATION(DV684): tg raises this on the component, where nothing listens, so the fish tank's size limits never
        // applied; it is raised on the aquarium, the parent the fish tank registers on.
        object return_value = source._listen_lookup?[COMSIG_AQUARIUM_CAN_INSERT] != null ? source._SendSignal(COMSIG_AQUARIUM_CAN_INSERT, Dm.list(source, item, user)) : 0f;
        if (Dm.truthy(Dm.bitand(return_value, COMSIG_CANNOT_INSERT_IN_AQUARIUM)))
            return FALSE;
        if (Dm.truthy(Dm.bitand(return_value, COMSIG_CAN_INSERT_IN_AQUARIUM)))
            return TRUE;

        if (HAS_TRAIT(item, TRAIT_UNIQUE_AQUARIUM_CONTENT))
        {
            foreach (atom_movable content in Dm.each<atom_movable>(source))
            {
                if (content == item)
                    continue;
                if (content.type == item.type)
                {
                    source.balloon_alert(user, I18n.T("cannot add to aquarium!"));
                    return FALSE;
                }
            }
        }
        return TRUE;
    }

    ///Handles aquarium content insertion
    // tg: code/datums/components/aquarium.dm:327
    [SignalHandler]
    public virtual void on_entered(atom_movable source = null, atom_movable entered = null)
    {
        Waiting.NoSleep(get_content_beauty(entered));
        if (!isfish(entered))
            return;
        obj_item_fish fish = (obj_item_fish)entered;
        LAZYADDASSOCLIST(ref tracked_fish_by_type, entered.type, entered);
        if (fish.stable_population < length(tracked_fish_by_type[fish.type]))
        {
            foreach (obj_item_fish anyfin in Dm.each<obj_item_fish>(tracked_fish_by_type[entered.type]))
                anyfin.fish_flags = Dm.bitor(anyfin.fish_flags, FISH_FLAG_OVERPOPULATED);
        }

        if (Dm.eq(current_mode, AQUARIUM_MODE_AUTO) && fish.status != FISH_DEAD)
            Waiting.NoSleep(get_optimal_aquarium_settings());
        else
            check_fluid_and_temperature(fish);
        RegisterSignal(fish, COMSIG_FISH_STATUS_CHANGED, nameof(on_fish_status_changed));
    }

    ///update the beauty_by_content of a 'beauty_by_content' key and then recalculate the beauty.
    // tg: code/datums/components/aquarium.dm:345
    public virtual async DmTask get_content_beauty(atom_movable content = null)
    {
        DmList beauty_holder = Dm.list();
        if (content._listen_lookup?[COMSIG_MOVABLE_GET_AQUARIUM_BEAUTY] != null)
            content._SendSignal(COMSIG_MOVABLE_GET_AQUARIUM_BEAUTY, Dm.list(content, beauty_holder));
        object beauty = beauty_holder[1f];
        if (!Dm.truthy(beauty))
            return;
        float old_beauty = Dm.num(default_beauty);
        foreach (object key in Dm.each(beauty_by_content))
            old_beauty += Dm.num(beauty_by_content[key]);
        LAZYSET(ref beauty_by_content, content, beauty);
        await update_aquarium_beauty(old_beauty);
    }

    ///Handles aquarium content removal.
    // tg: code/datums/components/aquarium.dm:358
    [SignalHandler]
    public virtual void on_exited(atom_movable source = null, atom_movable gone = null)
    {
        object beauty = beauty_by_content?[gone];
        if (Dm.truthy(beauty))
        {
            float old_beauty = Dm.num(default_beauty);
            foreach (object key in Dm.each(beauty_by_content))
                old_beauty += Dm.num(beauty_by_content[key]);
            LAZYREMOVE(ref beauty_by_content, gone);
            Waiting.NoSleep(update_aquarium_beauty(old_beauty));
        }
        if (!isfish(gone))
            return;
        obj_item_fish fish = (obj_item_fish)gone;
        if (fish.stable_population == length(tracked_fish_by_type?[fish.type]))
        {
            foreach (obj_item_fish anyfin in Dm.each<obj_item_fish>(tracked_fish_by_type[fish.type]))
                anyfin.fish_flags = Dm.bitand(anyfin.fish_flags, Dm.bitnot(FISH_FLAG_OVERPOPULATED));
        }
        LAZYREMOVEASSOC(ref tracked_fish_by_type, fish.type, fish);
        fish.fish_flags = Dm.bitand(fish.fish_flags, Dm.bitnot(Dm.bitor(FISH_FLAG_SAFE_TEMPERATURE, FISH_FLAG_SAFE_FLUID)));
        UnregisterSignal(gone, COMSIG_FISH_STATUS_CHANGED);
        if (Dm.eq(current_mode, AQUARIUM_MODE_AUTO) && fish.status != FISH_DEAD)
            Waiting.NoSleep(get_optimal_aquarium_settings());
    }

    ///Return a list of fish which our fishie can reproduce with (including itself if self-reproducing)
    // tg: code/datums/components/aquarium.dm:380
    [SignalHandler]
    public virtual void get_candidates(atom_movable source = null, obj_item_fish fish = null, DmList candidates = null)
    {
        DmList types_to_mate_with = tracked_fish_by_type;
        if (!HAS_TRAIT(fish, TRAIT_FISH_CROSSBREEDER))
        {
            DmList types_to_check = Dm.list(fish.type);
            if (fish.compatible_types != null)
                Dm.combine(ref types_to_check, fish.compatible_types);
            types_to_mate_with = Dm.bitand(types_to_mate_with, types_to_check) as DmList;
        }

        foreach (Type fish_type in Dm.each_cast<Type>(types_to_mate_with))
        {
            DmList type_fishes = tracked_fish_by_type[fish_type] as DmList;
            if (length(type_fishes) >= Dm.initial(fish_type, (obj_item_fish p) => p.stable_population))
                continue;
            Dm.append(candidates, type_fishes);
        }
    }

    ///Check if an offspring of two fish (or one if self-reproducing) can evolve.
    // tg: code/datums/components/aquarium.dm:396
    [SignalHandler]
    public virtual object check_evolution(atom_movable source = null, obj_item_fish fish = null, obj_item_fish mate = null, datum_fish_evolution evolution = null)
    {
        //chances are halved if it isn't asexual reproduction and one parent doesn't have the evolution.
        float real_probability = (mate == null || Dm.@in(evolution.type, mate.evolution_types)) ? Dm.num(evolution.probability) : Dm.num(evolution.probability) * 0.5f;
        if (HAS_TRAIT(fish, TRAIT_FISH_MUTAGENIC) || (mate != null && HAS_TRAIT(mate, TRAIT_FISH_MUTAGENIC)))
            real_probability *= 3;
        if (!prob(real_probability))
            return NONE;
        if (!ISINRANGE(fluid_temp, Dm.num(evolution.required_temperature_min), Dm.num(evolution.required_temperature_max)))
            return NONE;
        return COMPONENT_ALLOW_EVOLUTION;
    }

    /**
     * Toggles a couple flags that determine if the fish is in safe waters so that we won't have to use signals or
     * access this comp in multiple places just to confirm that.
     */
    // tg: code/datums/components/aquarium.dm:412
    public virtual void check_fluid_and_temperature(obj_item_fish fish = null)
    {
        if (Dm.@in(fluid_type, GLOB.fish_compatible_fluid_types[fish.required_fluid_type]) || (fluid_type == AQUARIUM_FLUID_AIR && HAS_TRAIT(fish, TRAIT_FISH_AMPHIBIOUS)))
            fish.fish_flags = Dm.bitor(fish.fish_flags, FISH_FLAG_SAFE_FLUID);
        else
            fish.fish_flags = Dm.bitand(fish.fish_flags, Dm.bitnot(FISH_FLAG_SAFE_FLUID));
        if (ISINRANGE(fluid_temp, fish.required_temperature_min, fish.required_temperature_max))
            fish.fish_flags = Dm.bitor(fish.fish_flags, FISH_FLAG_SAFE_TEMPERATURE);
        else
            fish.fish_flags = Dm.bitand(fish.fish_flags, Dm.bitnot(FISH_FLAG_SAFE_TEMPERATURE));
    }

    ///Fish beauty changes when they're dead, so we need to update the beauty of the aquarium too.
    // tg: code/datums/components/aquarium.dm:423
    public virtual async DmTask on_fish_status_changed(obj_item_fish fish = null)
    {
        await get_content_beauty(fish);
        if (Dm.eq(current_mode, AQUARIUM_MODE_AUTO))
            await get_optimal_aquarium_settings();
    }

    // tg: code/datums/components/aquarium.dm:428
    public virtual async DmTask update_aquarium_beauty(object old_beauty = null)
    {
        if (QDELETED(parent))
            return;
        float old = clamp(Dm.num(old_beauty), MIN_AQUARIUM_BEAUTY, MAX_AQUARIUM_BEAUTY);
        float new_beauty = 0;
        foreach (object key in Dm.each(beauty_by_content))
            new_beauty += Dm.num(beauty_by_content[key]);
        new_beauty = clamp(new_beauty, MIN_AQUARIUM_BEAUTY, MAX_AQUARIUM_BEAUTY);
        if (new_beauty == old)
            return;
        if (old != 0)
            parent.RemoveElement(typeof(datum_element_beauty), old);
        if (new_beauty != 0)
            await parent.AddElement(typeof(datum_element_beauty), new_beauty);
    }

    ///Remove a visual overlay from an aquarium_content comp
    // tg: code/datums/components/aquarium.dm:444
    [SignalHandler]
    public virtual void remove_visual(atom_movable source = null, obj_effect_aquarium visual = null)
    {
        Dm.remove(source.vis_contents, visual);
        Dm.remove(used_layers, visual.layer);
    }

    ///set values for a visual overlay for an aquarium_content comp
    // tg: code/datums/components/aquarium.dm:450
    [SignalHandler]
    public virtual void set_visual(atom_movable source = null, obj_effect_aquarium visual = null)
    {
        Dm.remove(used_layers, visual.layer);
        visual.layer = Dm.num(request_layer(visual.layer_mode));
        visual.aquarium_zone_min_pw = aquarium_zone_min_pw;
        visual.aquarium_zone_max_pw = aquarium_zone_max_pw;
        visual.aquarium_zone_min_pz = aquarium_zone_min_pz;
        visual.aquarium_zone_max_pz = aquarium_zone_max_pz;
        visual.fluid_type = fluid_type;
    }

    // tg: code/datums/components/aquarium.dm:460
    public virtual object request_layer(object layer_type = null)
    {
        atom_movable movable = (atom_movable)parent;
        switch (layer_type)
        {
            case AQUARIUM_LAYER_MODE_BEHIND_GLASS:
                return movable.layer + AQUARIUM_BELOW_GLASS_LAYER;
            case AQUARIUM_LAYER_MODE_BOTTOM:
                return movable.layer + AQUARIUM_MIN_OFFSET;
            case AQUARIUM_LAYER_MODE_TOP:
                return movable.layer + AQUARIUM_MAX_OFFSET;
            case AQUARIUM_LAYER_MODE_AUTO:
                float chosen_layer = AQUARIUM_MIN_OFFSET + AQUARIUM_LAYER_STEP;
                while (Dm.@in(chosen_layer, used_layers) && (chosen_layer <= AQUARIUM_MAX_OFFSET))
                    chosen_layer += AQUARIUM_LAYER_STEP;
                Dm.append(used_layers, chosen_layer);
                return movable.layer + chosen_layer;
        }
        return null;
    }

    // tg: code/datums/components/aquarium.dm:476
    public virtual object get_fishes()
    {
        DmList fishes = Dm.list();
        foreach (object key in Dm.each(tracked_fish_by_type))
            Dm.append(fishes, tracked_fish_by_type[key]);
        return fishes;
    }

    // tg: code/datums/components/aquarium.dm:482
    public virtual async DmTask<object> is_fish_population_safe()
    {
        DmList fishes = get_fishes() as DmList;
        float alive = 0;
        float not_safe = 0;
        foreach (obj_item_fish fishie in Dm.each<obj_item_fish>(fishes))
        {
            if (fishie.status == FISH_DEAD)
                continue;
            if (!Dm.truthy(await fishie.proper_environment()) || Dm.num(fishie.get_hunger()) > FISH_STARVING_THRESHOLD * 0.9f)
                not_safe++;
            alive++;
        }
        return not_safe <= (alive / 2) ? TRUE : FALSE;
    }

    /**
     * Called if the mode is AQUARIUM_MODE_AUTO whenever a fish is added, removed, dies or resurrected.
     *
     * This tries to find what the best combination of fluid and temperature. is to ensure
     * the survival of the highest number of fishes. It also can and will activate stasis if over half of
     * the fish population is at risk of dying. It doesn't care if the fish is big or small, useful
     * or not. All fish are equal before the impartial eye of AQUARIUM_MODE_AUTO.
     */
    // tg: code/datums/components/aquarium.dm:502
    public virtual async DmTask get_optimal_aquarium_settings()
    {
        if (!Dm.truthy(length(tracked_fish_by_type)))
        {
            REMOVE_TRAIT(parent, TRAIT_STOP_FISH_REPRODUCTION_AND_GROWTH, AQUARIUM_TRAIT);
            return;
        }

        DmList can_survive_with = Dm.list();
        DmList checked_types = Dm.list();
        DmList sorted_tracked = sortTim(tracked_fish_by_type.Copy(), "/proc/" + nameof(GlobalProcs.cmp_list_len_dsc), associative: TRUE) as DmList;
        DmList fish_alive_by_type = Dm.list();
        foreach (Type fish_type in Dm.each_cast<Type>(sorted_tracked))
        {
            float our_min_temp = Dm.initial(fish_type, (obj_item_fish p) => p.required_temperature_min);
            float our_max_temp = Dm.initial(fish_type, (obj_item_fish p) => p.required_temperature_max);
            string our_fluid_type = Dm.initial(fish_type, (obj_item_fish p) => p.required_fluid_type);

            Dm.append(checked_types, fish_type);
            // LAZYINITLIST(can_survive_with[fish_type]) (__HELPERS/_lists.dm:52, L ||= list()), expanded: its argument is a list element
            if (!Dm.truthy(can_survive_with[fish_type]))
                can_survive_with[fish_type] = Dm.list();

            float fish_amount = 0;
            foreach (obj_item_fish fish_instance in Dm.each<obj_item_fish>(tracked_fish_by_type[fish_type]))
            {
                if (fish_instance.status == FISH_ALIVE)
                    fish_amount++;
            }

            if (fish_amount == 0) //all fishes of this type are dead, skip it.
                continue;

            fish_alive_by_type[fish_type] = fish_amount;

            //Check if there's an overlap with the temp/fluid requirements of the other types.
            foreach (Type enemy_type in Dm.each_cast<Type>(Dm.sub(tracked_fish_by_type, checked_types)))
            {
                float enem_min_temp = Dm.initial(enemy_type, (obj_item_fish p) => p.required_temperature_min);
                float enem_max_temp = Dm.initial(enemy_type, (obj_item_fish p) => p.required_temperature_max);
                if (our_min_temp > enem_max_temp || our_max_temp < enem_min_temp)
                    continue;
                string enem_fluid_type = Dm.initial(enemy_type, (obj_item_fish p) => p.required_fluid_type);
                if (!Dm.truthy(length(Dm.bitand(GLOB.fish_compatible_fluid_types[our_fluid_type], GLOB.fish_compatible_fluid_types[enem_fluid_type]))))
                    continue;
                DmList ours = can_survive_with[fish_type] as DmList;
                Dm.combine(ref ours, enemy_type);
                // LAZYOR(can_survive_with[enemy_type], fish_type), expanded: its argument is a list element
                DmList theirs = can_survive_with[enemy_type] as DmList;
                LAZYOR(ref theirs, fish_type);
                can_survive_with[enemy_type] = theirs;
            }
        }

        DmList highest_val_list = Dm.list(tracked_fish_by_type[1f]); //In case there's only one type of fish in here...
        float highest_val = -1;
        foreach (object fish_type in Dm.each(can_survive_with))
        {
            DmList compatible_types = can_survive_with[fish_type] as DmList;
            float fish_amount = Dm.num(fish_alive_by_type[fish_type]);
            foreach (obj_item_fish fish_instance in Dm.each<obj_item_fish>(tracked_fish_by_type[fish_type]))
            {
                if (fish_instance.status == FISH_ALIVE)
                    fish_amount++;
            }
            foreach (object ally_type in Dm.each(compatible_types))
            {
                float val = fish_amount + Dm.num(fish_alive_by_type[ally_type]);
                DmList common_allies = Dm.bitand(compatible_types, can_survive_with[ally_type]) as DmList;
                foreach (object third_type in Dm.each(common_allies))
                    val += Dm.num(fish_alive_by_type[third_type]);
                if (val <= highest_val)
                    continue;
                highest_val_list = Dm.add(Dm.list(fish_type, ally_type), common_allies) as DmList;
                highest_val = val;
            }
        }

        float min_temp = MIN_AQUARIUM_TEMP;
        float max_temp = MAX_AQUARIUM_TEMP;
        DmList possible_fluids = fluid_types.Copy();
        foreach (Type chosen_type in Dm.each_cast<Type>(highest_val_list))
        {
            possible_fluids = Dm.bitand(possible_fluids, GLOB.fish_compatible_fluid_types[Dm.initial(chosen_type, (obj_item_fish p) => p.required_fluid_type)]) as DmList;
            min_temp = max(min_temp, Dm.initial(chosen_type, (obj_item_fish p) => p.required_temperature_min));
            max_temp = min(max_temp, Dm.initial(chosen_type, (obj_item_fish p) => p.required_temperature_max));
        }

        fluid_temp = clamp(min_temp + round((max_temp - min_temp) / 2), min_fluid_temp, max_fluid_temp);
        set_fluid_type(possible_fluids[1f]);

        if (!Dm.truthy(await is_fish_population_safe()))
            ADD_TRAIT(parent, TRAIT_STOP_FISH_REPRODUCTION_AND_GROWTH, AQUARIUM_TRAIT);
        else
            REMOVE_TRAIT(parent, TRAIT_STOP_FISH_REPRODUCTION_AND_GROWTH, AQUARIUM_TRAIT);
    }

    // tg: code/datums/components/aquarium.dm:575
    [SignalHandler]
    public virtual void interact(atom_movable source = null, mob user = null)
    {
        if (HAS_TRAIT(source, TRAIT_AQUARIUM_PANEL_OPEN))
        {
            Waiting.Detach(ui_interact(user)); // INVOKE_ASYNC(src, PROC_REF(ui_interact), user)
            return;
        }
        Waiting.Detach(admire(source, user as mob_living)); // INVOKE_ASYNC(src, PROC_REF(admire), source, user)
    }

    // tg: code/datums/components/aquarium.dm:582
    [SignalHandler]
    public virtual void secondary_interact(atom_movable source = null, mob user = null)
    {
        if (HAS_TRAIT(source, TRAIT_AQUARIUM_PANEL_OPEN))
            return;
        Waiting.Detach(admire(source, user as mob_living)); // INVOKE_ASYNC(src, PROC_REF(admire), source, user)
    }

    // tg: code/datums/components/aquarium.dm:588
    [SignalHandler]
    public virtual object on_secondary_attack_hand(obj_item source = null, mob_living user = null)
    {
        Waiting.Detach(admire(source, user)); // INVOKE_ASYNC(src, PROC_REF(admire), source, user)
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/datums/components/aquarium.dm:593
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            atom_movable movable = (atom_movable)parent;
            ui = Dm.@new<datum_tgui>(user, this, "Aquarium", movable.name);
            await ui.open();
        }
        return null;
    }

    // tg: code/datums/components/aquarium.dm:600
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = await base.ui_data(user) as DmList ?? Dm.list();
        atom_movable aquarium = (atom_movable)parent;
        dot["fluidType"] = fluid_type;
        dot["temperature"] = fluid_temp;
        dot["lockedFluidTemp"] = Dm.eq(current_mode, AQUARIUM_MODE_AUTO) ? TRUE : FALSE;
        dot["currentMode"] = current_mode;
        dot["currentTooltip"] = aquarium_modes[current_mode];
        DmList fish_data = Dm.list();
        dot["fishData"] = fish_data;
        dot["feedingInterval"] = feeding_interval / (1 * MINUTES);
        DmList prop_data = Dm.list();
        dot["propData"] = prop_data;
        foreach (atom_movable item in Dm.each<atom_movable>(aquarium.contents))
        {
            if (isfish(item))
            {
                obj_item_fish fish = (obj_item_fish)item;
                Dm.append(ref fish_data, Dm.list(Dm.list(
                    Dm.kv("fish_ref", REF(fish)),
                    Dm.kv("fish_name", uppertext(fish.name)),
                    Dm.kv("fish_happiness", await fish.get_happiness_value()),
                    Dm.kv("fish_icon", Dm.initial(fish, (atom p) => p.icon)),
                    Dm.kv("fish_icon_state", Dm.initial(fish, (atom p) => p.icon_state)),
                    Dm.kv("fish_alive", fish.status == FISH_ALIVE ? TRUE : FALSE))));
                continue;
            }
            Dm.append(ref prop_data, Dm.list(Dm.list(
                Dm.kv("prop_ref", REF(item)),
                Dm.kv("prop_name", item.name),
                Dm.kv("prop_icon", Dm.initial(item, (atom p) => p.icon)),
                Dm.kv("prop_icon_state", Dm.initial(item, (atom p) => p.icon_state)))));
        }
        return dot;
    }

    // tg: code/datums/components/aquarium.dm:630
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList dot = await base.ui_static_data(user) as DmList ?? Dm.list();
        //I guess these should depend on the fluid so lava critters can get high or stuff below water freezing point but let's keep it simple for now.
        dot["minTemperature"] = min_fluid_temp;
        dot["maxTemperature"] = max_fluid_temp;
        dot["fluidTypes"] = fluid_types;
        dot["heartIcon"] = Dm.resource("icons/effects/effects.dmi");
        DmList modes_no_assoc = Dm.list(); //the UI dropdown won't work with assoc lists here
        foreach (object mode in Dm.each(aquarium_modes))
            Dm.append(modes_no_assoc, mode);
        dot["aquariumModes"] = modes_no_assoc;
        return dot;
    }

    // tg: code/datums/components/aquarium.dm:642
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        mob user = usr;
        atom_movable movable = (atom_movable)parent;
        switch (action)
        {
            case "temperature":
                object temperature = @params["temperature"];
                if (isnum(temperature) && Dm.num(temperature) != fluid_temp)
                {
                    fluid_temp = clamp(Dm.num(temperature), min_fluid_temp, max_fluid_temp);
                    foreach (obj_item_fish fish in Dm.each<obj_item_fish>(get_fishes()))
                        check_fluid_and_temperature(fish);
                }
                dot = TRUE;
                break;
            case "fluid":
                if (!Dm.eq(@params["fluid"], fluid_type) && Dm.@in(@params["fluid"], fluid_types))
                    set_fluid_type(@params["fluid"]);
                dot = TRUE;
                break;
            case "change_mode":
                object new_mode = @params["new_mode"];
                await set_aquarium_mode(new_mode);
                dot = TRUE;
                break;
            case "feeding_interval":
                feeding_interval = Dm.num(@params["feeding_interval"]) * MINUTES;
                dot = TRUE;
                break;
            case "pet_fish":
            {
                obj_item_fish fish = locate_ref_in(@params["fish_reference"], movable.contents) as obj_item_fish;
                if (fish != null)
                    await fish.pet_fish(user as mob_living);
                break;
            }
            case "remove_item":
            {
                atom_movable item = locate_ref_in(@params["item_reference"], movable.contents) as atom_movable;
                if (item != null)
                    await item.forceMove((atom)movable.drop_location());
                to_chat(user, span_notice(I18n.T("You take out [] from [].", item, movable)));
                break;
            }
            case "rename_fish":
            {
                object new_name = await sanitize_name(@params["chosen_name"]);
                atom_movable fish = locate_ref_in(@params["fish_reference"], movable.contents) as atom_movable;
                if (fish == null || !Dm.truthy(new_name) || Dm.eq(new_name, fish.name))
                    return null;
                await fish.AddComponent(typeof(datum_component_rename), new_name, fish.desc);
                break;
            }
        }
        return dot;
    }

    // tg: code/datums/components/aquarium.dm:681
    public virtual async DmTask set_aquarium_mode(object new_mode = null)
    {
        if (Dm.eq(new_mode, current_mode))
            return;
        REMOVE_TRAIT(parent, TRAIT_STOP_FISH_REPRODUCTION_AND_GROWTH, AQUARIUM_TRAIT);
        current_mode = new_mode;
        switch (current_mode)
        {
            case AQUARIUM_MODE_SAFE:
                ADD_TRAIT(parent, TRAIT_STOP_FISH_REPRODUCTION_AND_GROWTH, AQUARIUM_TRAIT);
                break;
            case AQUARIUM_MODE_AUTO:
                await get_optimal_aquarium_settings();
                break;
        }
    }

    // tg: code/datums/components/aquarium.dm:692
    public virtual void set_fluid_type(object new_fluid_type = null)
    {
        atom_movable movable = (atom_movable)parent;
        fluid_type = Dm.str(new_fluid_type);
        if (movable._listen_lookup?[COMSIG_AQUARIUM_FLUID_CHANGED] != null)
            movable._SendSignal(COMSIG_AQUARIUM_FLUID_CHANGED, Dm.list(movable, fluid_type));
        foreach (obj_item_fish fish in Dm.each<obj_item_fish>(get_fishes()))
            check_fluid_and_temperature(fish);
    }

    // tg: code/datums/components/aquarium.dm:699
    public virtual async DmTask admire(atom_movable source = null, mob_living user = null)
    {
        if (!isliving(user))
            return;
        source.balloon_alert(user, I18n.T("admiring aquarium..."));
        if (!Dm.truthy(await do_after(user, 5 * SECONDS, target: source)))
            return;
        float alive_fish = 0;
        float dead_fish = 0;
        foreach (obj_item_fish fish in Dm.each<obj_item_fish>(get_fishes()))
        {
            if (fish.status == FISH_ALIVE)
                alive_fish++;
            else
                dead_fish++;
        }

        bool morb = HAS_MIND_TRAIT(user, TRAIT_MORBID);
        //Check if there are live fish - good mood
        //All fish dead - bad mood.
        //No fish - nothing.
        if (alive_fish > 0)
            await user.add_mood_event("aquarium", morb ? typeof(datum_mood_event_morbid_aquarium_bad) : typeof(datum_mood_event_aquarium_positive));
        else if (dead_fish > 0)
            await user.add_mood_event("aquarium", morb ? typeof(datum_mood_event_morbid_aquarium_good) : typeof(datum_mood_event_aquarium_negative));
    }

    // tg: code/datums/components/aquarium.dm:722
    [SignalHandler]
    public virtual object on_requesting_context_from_item(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        bool open_panel = HAS_TRAIT(source, TRAIT_AQUARIUM_PANEL_OPEN);
        bool is_held_item = held_item == source;
        if (held_item == null || is_held_item)
        {
            bool is_item = isitem(source);
            if (!is_item || is_held_item)
                context[SCREENTIP_CONTEXT_LMB] = open_panel ? I18n.T("Adjust settings") : I18n.T("Admire");
            if (is_item)
                context[SCREENTIP_CONTEXT_RMB] = I18n.T("Admire");
            context[SCREENTIP_CONTEXT_ALT_LMB] = !open_panel ? I18n.T("Open settings panel") : I18n.T("Close settings panel");
            return CONTEXTUAL_SCREENTIP_SET;
        }
        if (istype(held_item, typeof(obj_item_plunger)))
        {
            context[SCREENTIP_CONTEXT_LMB] = I18n.T("Empty feed storage");
            return CONTEXTUAL_SCREENTIP_SET;
        }
        if (istype(held_item, typeof(obj_item_reagent_containers_cup_fish_feed)) && (((datum_reagents)source.reagents) == null || !open_panel))
        {
            context[SCREENTIP_CONTEXT_LMB] = I18n.T("Feed fishes");
            return CONTEXTUAL_SCREENTIP_SET;
        }
        if (HAS_TRAIT(held_item, TRAIT_AQUARIUM_CONTENT))
        {
            context[SCREENTIP_CONTEXT_LMB] = I18n.T("Insert in aquarium");
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return null;
    }
}
