// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/fish_mount.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the fish mount, a wall trophy that shows a fish off with its catcher and date, and its frame. Keeping the
// bar's trophy between rounds (SSpersistence's trophy fish) is owner-deferred (D332): a persistent mount starts with tg's
// first fish, as it does on an empty database. The var blocks are generated (D111).

// tg: code/modules/fishing/fish_mount.dm:1
public partial class obj_item_wallframe_fish : obj_item_wallframe
{
    // tg: code/modules/fishing/fish_mount.dm:12
    public override void after_attach(obj attached_to = null)
    {
        base.after_attach(attached_to);
        obj_structure_fish_mount mount = (obj_structure_fish_mount)attached_to;
        mount.persistence_id = persistence_id;
    }
}

///A wallmounted structure on which a fish can be attached to be used as room decoration.
// tg: code/modules/fishing/fish_mount.dm:17
public partial class obj_structure_fish_mount : obj_structure
{
    // tg: code/modules/fishing/fish_mount.dm:37
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        //Mounted fish shouldn't flop. It should also show size and weight to everyone.
        add_traits(Dm.list(TRAIT_STOP_FISH_FLOPPING, TRAIT_EXAMINE_FISH), INNATE_TRAIT);
        if (Dm.truthy(mapload))
            Waiting.NoSleep(find_and_mount_on_atom());
        if (!Dm.truthy(persistence_id))
            return dot;
        // PORT(harness): a world without the fishing subsystem (the toy worlds) loads the trophy at once.
        if (SSfishing == null || Dm.truthy(SSfishing.initialized))
            load_trophy_fish();
        else
            RegisterSignal(SSfishing, COMSIG_SUBSYSTEM_POST_INITIALIZE, nameof(load_trophy_fish));
        return dot;
    }

    // tg: code/modules/fishing/fish_mount.dm:50
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref mounted_fish);
        return base.Destroy(force);
    }

    // tg: code/modules/fishing/fish_mount.dm:54
    [SignalHandler]
    public virtual void load_trophy_fish(datum source = null)
    {
        Dm.Deferred("code/modules/fishing/fish_mount.dm:56", "load_trophy_fish: SSpersistence.load_trophy_fish, the trophy fish kept between rounds (persistence is owner-deferred, D332)");
        if (SSfishing != null)
            UnregisterSignal(SSfishing, COMSIG_SUBSYSTEM_POST_INITIALIZE);
        if (mounted_fish == null)
            Waiting.NoSleep(add_first_fish());
    }

    // tg: code/modules/fishing/fish_mount.dm:61
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await base.screwdriver_act(user, tool);
        balloon_alert(user, "removing mount...");
        if (!Dm.truthy(await tool.use_tool(this, user, 3 * SECONDS, volume: 50)))
            return ITEM_INTERACT_BLOCKING;
        playsound(this, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
        balloon_alert_to_viewers("fish mount removed");
        await deconstruct();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/fishing/fish_mount.dm:71
    public virtual async DmTask add_first_fish()
    {
        DmList valid_picks = Dm.sub(Dm.sub(subtypesof(typeof(obj_item_fish)), typesof(typeof(obj_item_fish_holo))), typeof(obj_item_fish_starfish_chrystarfish)) as DmList; // chrystarfish immediately shatters when placed
        Type fish_path = pick(valid_picks) as Type;
        Type redirect = Dm.initial(fish_path, (obj_item_fish p) => p.fish_id_redirect_path);
        if (redirect != null)
            fish_path = redirect;
        object fluff_name = pick("John Trasen III", "a nameless intern", "Pun Pun", AQUARIUM_COMPANY, "Unknown", "Central Command");
        await add_fish(await Dm.new_async(fish_path, loc) as obj_item_fish, from_persistence: TRUE, catcher: fluff_name);
        await mounted_fish.randomize_size_and_weight();
        await mounted_fish.set_status(FISH_DEAD);
        Dm.Deferred("code/modules/fishing/fish_mount.dm:80", "add_first_fish: SSpersistence.save_trophy_fish, the trophy fish kept between rounds (persistence is owner-deferred, D332)");
    }

    // tg: code/modules/fishing/fish_mount.dm:82
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        await base.atom_deconstruct(disassembled);
        if (Dm.truthy(disassembled))
        {
            obj_item_wallframe_fish frame = await Dm.new_async<obj_item_wallframe_fish>(loc);
            frame.persistence_id = persistence_id;
        }
        if (mounted_fish != null)
            await mounted_fish.forceMove(loc);
    }

    // tg: code/modules/fishing/fish_mount.dm:89
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!isfish(tool) || Dm.truthy(user.combat_mode))
            return await base.item_interaction(user, tool, modifiers, __extra);
        if (mounted_fish != null)
        {
            balloon_alert(user, "remove other fish first!");
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(Dm.bitand(tool.flags_1, HOLOGRAM_1)))
        {
            balloon_alert(user, "fish not mountable!");
            return ITEM_INTERACT_BLOCKING;
        }
        balloon_alert(user, "mounting fish...");
        if (!Dm.truthy(await do_after(user, 3 * SECONDS, this)) || mounted_fish != null)
            return ITEM_INTERACT_BLOCKING;
        await add_fish((obj_item_fish)tool, catcher: user.name);
        balloon_alert_to_viewers("fish mounted");
        playsound(loc, Dm.resource("sound/machines/click.ogg"), 30, TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/fishing/fish_mount.dm:106
    public override object IsContainedAtomAccessible(atom contained = null, atom_movable user = null)
    {
        return TRUE;
    }

    // tg: code/modules/fishing/fish_mount.dm:109
    public virtual async DmTask add_fish(obj_item_fish fish = null, float? from_persistence = null, object catcher = null)
    {
        from_persistence ??= FALSE;
        if (QDELETED(this)) // don't ever try to add a fish to one of these that's already been deleted - and get rid of the one that was created
        {
            qdel(fish);
            return;
        }
        if (QDELETED(fish)) // no adding deleted fishies either
            return;
        if (mounted_fish != null)
            await mounted_fish.forceMove(loc);
        await fish.forceMove(this);
        Dm.combine(vis_contents, fish);
        fish.vis_flags = Dm.bitor(fish.vis_flags, Dm.bitor(VIS_INHERIT_PLANE, VIS_INHERIT_LAYER));
        Dm.mask(ref fish.interaction_flags_item, Dm.bitnot(INTERACT_ITEM_ATTACK_HAND_PICKUP));
        Dm.mask(ref fish.obj_flags, Dm.bitnot(UNIQUE_RENAME));
        fish.remove_fillet_type();
        fish.anchored = TRUE;
        mounted_fish = fish;

        if (!Dm.truthy(fish.catcher_name))
            fish.catcher_name = catcher;
        if (!Dm.truthy(fish.catch_date))
            fish.catch_date = $"{time2text(world.realtime, "Day, Month DD", NO_TIMEZONE)}, {Dm.str(CURRENT_STATION_YEAR)}";

        await AddElement(typeof(datum_element_beauty), await get_fish_beauty());
        RegisterSignals(fish, Dm.list(COMSIG_ATOM_ATTACK_HAND, COMSIG_ATOM_ATTACK_PAW), nameof(on_fish_attack_hand));
        if (Dm.truthy(from_persistence))
        {
            persistence_loaded_fish = TRUE;
            fish.add_traits(Dm.list(TRAIT_NO_FISHING_ACHIEVEMENT, TRAIT_FISH_LOW_PRICE), INNATE_TRAIT);
        }
        else if (Dm.truthy(persistence_id))
            Dm.Deferred("code/modules/fishing/fish_mount.dm:137", "add_fish: SSpersistence.save_trophy_fish, the trophy fish kept between rounds (persistence is owner-deferred, D332)");
    }

    // tg: code/modules/fishing/fish_mount.dm:139
    public virtual async DmTask<object> get_fish_beauty()
    {
        float beauty = 100 + mounted_fish.beauty * 1.2f;
        datum_material main_material = await mounted_fish.get_master_material() as datum_material;
        if (main_material != null)
            beauty += Dm.num(main_material.get_property(MATERIAL_BEAUTY)) * Dm.num(mounted_fish.weight);
        return round(beauty);
    }

    // tg: code/modules/fishing/fish_mount.dm:146
    [SignalHandler]
    public virtual object on_fish_attack_hand(datum source = null, mob_living user = null)
    {
        Waiting.Detach(remove_fish(user));
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/fishing/fish_mount.dm:151
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        await base.attack_hand_secondary(user, modifiers);
        if (mounted_fish == null)
            balloon_alert(user, "no fish mounted!");
        else
            await remove_fish(user as mob_living);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/fishing/fish_mount.dm:159
    public virtual async DmTask remove_fish(mob_living user = null)
    {
        balloon_alert(user, "removing fish...");
        if (!Dm.truthy(await do_after(user, (Dm.truthy(persistence_loaded_fish) ? 6 : 3) * SECONDS, this)) || mounted_fish == null)
            return;

        obj_item_fish fish_reference = mounted_fish;
        if (Dm.truthy(persistence_loaded_fish))
            roll_for_safe_removal(user);

        if (QDELETED(fish_reference))
        {
            Type ash_type = typeof(obj_effect_decal_cleanable_ash);
            if (fish_reference.w_class >= WEIGHT_CLASS_BULKY)
                ash_type = typeof(obj_effect_decal_cleanable_ash_large);
            await Dm.new_async(ash_type, loc);
            visible_message(I18n.T("[] turns into dust as [] removed from [].", fish_reference, fish_reference.p_theyre(), this));
        }
        else
            await user.put_in_hands(mounted_fish);
        balloon_alert_to_viewers("fish removed");
    }

    // tg: code/modules/fishing/fish_mount.dm:178
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (gone != mounted_fish)
            return await base.Exited(gone, direction);
        RemoveElement(typeof(datum_element_beauty), await get_fish_beauty());
        if (!QDELETED(mounted_fish) && (!Dm.truthy(persistence_loaded_fish) || Dm.truthy(roll_for_safe_removal())))
        {
            UnregisterSignal(mounted_fish, Dm.list(COMSIG_ATOM_ATTACK_HAND, COMSIG_ATOM_ATTACK_PAW));
            mounted_fish.vis_flags = Dm.bitand(mounted_fish.vis_flags, Dm.bitnot(Dm.bitor(VIS_INHERIT_PLANE, VIS_INHERIT_LAYER)));
            Dm.combine(ref mounted_fish.interaction_flags_item, INTERACT_ITEM_ATTACK_HAND_PICKUP);
            Dm.combine(ref mounted_fish.obj_flags, UNIQUE_RENAME);
            await mounted_fish.add_fillet_type();
            mounted_fish.anchored = FALSE;
        }
        persistence_loaded_fish = FALSE;
        mounted_fish = null;
        return await base.Exited(gone, direction);
    }

    // tg: code/modules/fishing/fish_mount.dm:193
    public virtual object roll_for_safe_removal(mob_living user = null)
    {
        if (mounted_fish == null)
            return FALSE;

        ///the base success rate is calculated considering the item inventory size and the heaviness of the fish.
        float success_prob = 100 / (mounted_fish.w_class + GET_FISH_WEIGHT_RANK(mounted_fish.weight));
        float fishing_prowess = Dm.num(user?.mind?.get_skill_level(typeof(datum_skill_fishing)));
        success_prob += fishing_prowess * 4; // up to 28% fixed bonus chance to safely retrieve the trophy depending on skill.
        if (!prob(success_prob))
        {
            qdel(mounted_fish);
            return FALSE;
        }

        persistence_loaded_fish = FALSE; //this way we don't roll again on Exited()
        return TRUE;
    }
}
