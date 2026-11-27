// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/maintenance.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST5 (D333): the file whole. The moisture trap (a maintenance crate spawner's pick): its drip loop, a hidden item in three of
// four (tg's weighted loot), reached by a mob lying down after two seconds in the cold water (or a bite from what lives in it), an
// item hidden in it, a container filled with its water, a monkey cube grown in it; its cell lines are the swabable element's and its fishing spot waits for the rods (Dm.Deferred, not-translated.md 213), and it keeps its fish alive (the fish-safe storage), tg's random
// draws for them kept. The strange pants altar (a crate spawner's rarest pick; its cult structure base is cult_structures.cs): a
// radial to recolour or create, three stages of nine and a half seconds (the pants overlay in the chosen colour, the nausea, the
// dizziness), then the flash and the strange pants, a minute's cooldown. The steam vent (Meta's six spawners): a hand opens or shuts
// its valve, a mob leaving its tile blows tg's steam smoke with its hiss every twenty (fast: ten) seconds; wrenched apart when shut.
// The var blocks are generated (D111).

/** This structure acts as a source of moisture loving cell lines,
as well as a location where a hidden item can sometimes be retrieved
at the cost of risking a vicious bite.**/
// tg: code/game/objects/structures/maintenance.dm:4
public partial class obj_structure_moisture_trap : obj_structure
{
    ///weighted loot table for what loot you can find inside the moisture trap.
    ///the actual loot isn't that great and should probably be improved and expanded later.
    // tg: code/game/objects/structures/maintenance.dm:18  var/static/list/loot_table (a type-level static, rules 10.6)
    public static DmList loot_table => TgState.Current.obj_structure_moisture_trap__loot_table ??= Dm.list(
        Dm.kv(typeof(obj_item_food_meat_slab_human_mutant_skeleton), 35),
        Dm.kv(typeof(obj_item_food_meat_slab_human_mutant_zombie), 15),
        Dm.kv(typeof(obj_item_trash_can), 15),
        Dm.kv(typeof(obj_item_clothing_head_helmet_skull), 10),
        Dm.kv(typeof(obj_item_restraints_handcuffs), 4),
        Dm.kv(typeof(obj_item_restraints_handcuffs_cable_red), 1),
        Dm.kv(typeof(obj_item_restraints_handcuffs_cable_blue), 1),
        Dm.kv(typeof(obj_item_restraints_handcuffs_cable_green), 1),
        Dm.kv(typeof(obj_item_restraints_handcuffs_cable_pink), 1),
        Dm.kv(typeof(obj_item_restraints_handcuffs_alien), 2),
        Dm.kv(typeof(obj_item_coin_bananium), 10),
        Dm.kv(typeof(obj_item_knife_butcher), 5));

    // tg: code/game/objects/structures/maintenance.dm:34
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_fish_safe_storage)));
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_MOIST, CELL_VIRUS_TABLE_GENERIC, rand(2, 4), 20));
        if (prob(40))
            critter_infested = FALSE;
        if (prob(75))
        {
            object picked_item = pick_weight(loot_table);
            hidden_item = Dm.@new((Type)picked_item, this) as obj_item;
        }

        datum_fish_source_moisture_trap fish_source = Dm.@new<datum_fish_source_moisture_trap>();
        if (prob(50)) // 50% chance there's another item to fish out of there
        {
            object picked_item = pick_weight(loot_table);
            fish_source.fish_table[picked_item] = 5;
            fish_source.fish_counts[picked_item] = 1;
        }
        Dm.Deferred("code/game/objects/structures/maintenance.dm:49", "Initialize: AddComponent(/datum/component/fishing_spot, fish_source): fishing in the trap waits for the fishing");
        drip_sfx = Dm.@new<datum_looping_sound_drip>(this);
        drip_sfx.start();
        return dot;
    }

    // tg: code/game/objects/structures/maintenance.dm:54
    public override object Destroy(float? force = null)
    {
        if (hidden_item != null)
            QDEL_NULL(ref hidden_item);
        drip_sfx?.stop(TRUE);
        return base.Destroy(force);
    }

    ///This proc checks if we are able to reach inside the trap to interact with it.
    // tg: code/game/objects/structures/maintenance.dm:62
    public virtual object CanReachInside(mob user = null)
    {
        if (!isliving(user))
            return FALSE;
        mob_living living_user = (mob_living)user;
        if (Dm.eq(living_user.body_position, STANDING_UP) && ishuman(living_user)) //I don't think monkeys can crawl on command.
            return FALSE;
        return TRUE;
    }

    // tg: code/game/objects/structures/maintenance.dm:71
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (iscyborg(user) || isalien(user))
            return dot;
        if (!Dm.truthy(CanReachInside(user)))
        {
            to_chat(user, span_warning(I18n.T("You need to lie down to reach into [].", this)));
            return dot;
        }
        to_chat(user, span_notice(I18n.T("You reach down into the cold water of the basin.")));
        playsound(this, Dm.resource("sound/effects/submerge.ogg"), 25, TRUE);
        if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: this)))
            return dot;
        if (hidden_item != null)
        {
            await user.put_in_hands(hidden_item);
            to_chat(user, span_notice(I18n.T("As you poke around inside [] you feel the contours of something hidden below the murky waters.</span>\n<span class='nicegreen'>You retrieve [] from [].", this, hidden_item, this)));
            hidden_item = null;
            return dot;
        }
        if (Dm.truthy(critter_infested) && prob(50) && iscarbon(user))
        {
            mob_living_carbon bite_victim = (mob_living_carbon)user;
            obj_item_bodypart affecting = bite_victim.get_active_hand() as obj_item_bodypart;
            to_chat(user, span_danger(I18n.T("You feel a sharp pain as an unseen creature sinks its [] into your []!", pick(I18n.T("fangs"), I18n.T("beak"), I18n.T("proboscis")), affecting.plaintext_zone)));
            await bite_victim.apply_damage(30, BRUTE, affecting);
            playsound(this, Dm.resource("sound/items/weapons/bite.ogg"), 70, TRUE);
            return dot;
        }
        to_chat(user, span_warning(I18n.T("You find nothing of value...")));
        return dot;
    }

    // tg: code/game/objects/structures/maintenance.dm:96
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (iscyborg(user) || isalien(user) || !Dm.truthy(CanReachInside(user)))
            return NONE;

        add_fingerprint(user);
        if (is_reagent_container(tool))
        {
            if (tool is obj_item_food_monkeycube cube)
            {
                await cube.Expand();
                return ITEM_INTERACT_SUCCESS;
            }

            obj_item_reagent_containers reagent_container = (obj_item_reagent_containers)tool;
            if (Dm.truthy(reagent_container.is_open_container()))
            {
                datum_reagents container_reagents = reagent_container.reagents as datum_reagents;
                await container_reagents.add_reagent(typeof(datum_reagent_water), min(Dm.num(reagent_container.volume) - Dm.num(container_reagents.total_volume), Dm.num(reagent_container.amount_per_transfer_from_this)));
                to_chat(user, span_notice(I18n.T("You fill [] from [].", reagent_container, this)));
                return ITEM_INTERACT_SUCCESS;
            }
        }

        if (hidden_item != null)
        {
            to_chat(user, span_warning(I18n.T("There is already something inside [].", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
        {
            to_chat(user, span_warning(I18n.T("\\The [] is stuck to your hand, you cannot put it in []!", tool, this)));
            return ITEM_INTERACT_BLOCKING;
        }

        hidden_item = tool;
        to_chat(user, span_notice(I18n.T("You hide [] inside the basin.", tool)));
        playsound(this, Dm.resource("sound/effects/splash.ogg"), 55, TRUE);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/game/objects/structures/maintenance.dm:126-130 (file-local, #undef at :251-255)
public partial class obj_structure_destructible_cult_pants_altar
{
    internal const float ALTAR_INACTIVE = 0;
    internal const float ALTAR_STAGEONE = 1;
    internal const float ALTAR_STAGETWO = 2;
    internal const float ALTAR_STAGETHREE = 3;
    internal const float ALTAR_TIME = 9.5f * SECONDS;
}

// tg: code/game/objects/structures/maintenance.dm:132
public partial class obj_structure_destructible_cult_pants_altar : obj_structure_destructible_cult
{
    // tg: code/game/objects/structures/maintenance.dm:147
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_melee_cultblade_dagger || !Dm.truthy(IS_CULTIST(user)) || !Dm.truthy(status))
            return NONE;
        to_chat(user, span_notice(I18n.T("[] is creating something, you can't move it!", this)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/maintenance.dm:153
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        DmList altar_options = Dm.list(
            Dm.kv(I18n.T("Change Color"), image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_recolor")),
            Dm.kv(I18n.T("Create Artefact"), image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_create")));
        object altar_result = await show_radial_menu(user, this, altar_options, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user), require_near: TRUE, tooltips: TRUE);
        if (Dm.eq(altar_result, I18n.T("Change Color")))
        {
            object chosen_color = await tgui_color_picker(user, "", I18n.T("Choose Color"), pants_color);
            if (chosen_color != null && Dm.truthy(await user.can_perform_action(this)))
                pants_color = Dm.str(chosen_color);
        }
        else if (Dm.eq(altar_result, I18n.T("Create Artefact")))
        {
            if (!(use_cooldown <= world.time) || status != ALTAR_INACTIVE) // COOLDOWN_FINISHED(src, use_cooldown), inline (rules 11.1 (a))
            {
                to_chat(user, span_warning(I18n.T("[] is not ready to create something new yet...", this)));
                return dot;
            }
            await pants_stageone();
        }
        return TRUE;
    }

    // tg: code/game/objects/structures/maintenance.dm:174
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (!(use_cooldown <= world.time)) // COOLDOWN_FINISHED(src, use_cooldown)
            icon_state = "altar_off";
        else
            icon_state = "altar";
        return dot;
    }

    // tg: code/game/objects/structures/maintenance.dm:181
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList ?? Dm.list();
        string overlayicon = null;
        if (status == ALTAR_INACTIVE)
            return dot;
        else if (status == ALTAR_STAGEONE)
            overlayicon = "altar_pants1";
        else if (status == ALTAR_STAGETWO)
            overlayicon = "altar_pants2";
        else if (status == ALTAR_STAGETHREE)
            overlayicon = "altar_pants3";
        mutable_appearance pants_overlay = mutable_appearance(icon, overlayicon, appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)) as mutable_appearance;
        pants_overlay.color = pants_color;
        Dm.append(ref dot, pants_overlay);
        return dot;
    }

    /// Starts creating the pants, plays the sound.
    // tg: code/game/objects/structures/maintenance.dm:198
    public virtual async DmTask pants_stageone()
    {
        status = ALTAR_STAGEONE;
        await update_icon();
        visible_message(span_warning(I18n.T("[] starts creating something...", this)));
        playsound(this, Dm.resource("sound/effects/magic/pantsaltar.ogg"), 60);
        _addtimer(Dm.@new<datum_callback>(this, nameof(pants_stagetwo)), ALTAR_TIME, file: "code/game/objects/structures/maintenance.dm", line: 203);
    }

    /// Continues the creation, making every mob nearby nauseous.
    // tg: code/game/objects/structures/maintenance.dm:206
    public virtual async DmTask pants_stagetwo()
    {
        status = ALTAR_STAGETWO;
        await update_icon();
        visible_message(span_warning(I18n.T("You start feeling nauseous...")));
        foreach (mob_living viewing_mob in Dm.each<mob_living>(viewers(7, this)))
        {
            await viewing_mob.set_eye_blur_if_lower(20 * SECONDS);
            await viewing_mob.adjust_confusion(10 * SECONDS);
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(pants_stagethree)), ALTAR_TIME, file: "code/game/objects/structures/maintenance.dm", line: 213);
    }

    /// Continues the creation, making every mob nearby dizzy
    // tg: code/game/objects/structures/maintenance.dm:216
    public virtual async DmTask pants_stagethree()
    {
        status = ALTAR_STAGETHREE;
        await update_icon();
        visible_message(span_warning(I18n.T("You start feeling horrible...")));
        foreach (mob_living viewing_mob in Dm.each<mob_living>(viewers(7, this)))
            await viewing_mob.set_dizzy_if_lower(20 * SECONDS);
        _addtimer(Dm.@new<datum_callback>(this, nameof(pants_create)), ALTAR_TIME, file: "code/game/objects/structures/maintenance.dm", line: 222);
    }

    /// Finishes the creation, creating the item itself, setting the cooldowns and flashing every mob nearby.
    // tg: code/game/objects/structures/maintenance.dm:225
    public virtual async DmTask pants_create()
    {
        status = ALTAR_INACTIVE;
        await update_icon();
        visible_message(span_danger(I18n.T("[] emits a flash of light and creates... pants?", this)));
        foreach (mob_living viewing_mob in Dm.each<mob_living>(viewers(7, this)))
            await viewing_mob.flash_act();
        obj_item_clothing_under_pants_slacks_altar pants = Dm.@new<obj_item_clothing_under_pants_slacks_altar>(get_turf(this));
        await pants.add_atom_colour(pants_color, ADMIN_COLOUR_PRIORITY);
        use_cooldown = world.time + use_cooldown_duration; // COOLDOWN_START(src, use_cooldown, use_cooldown_duration), inline (rules 11.1 (a))
        _addtimer(Dm.@new<datum_callback>(this, nameof(update_icon)), 1 * MINUTES + 0.1f * SECONDS, file: "code/game/objects/structures/maintenance.dm", line: 234);
        await update_icon();
    }

    // tg: code/game/objects/structures/maintenance.dm:237
    public virtual async DmTask<object> check_menu(mob user = null)
    {
        if (user == null) // !istype(user): the parameter is typed mob
            return FALSE;
        if (Dm.truthy(user.incapacitated) || !Dm.truthy(await user.Adjacent(this)))
            return FALSE;
        return TRUE;
    }
}

/**
 * Spawns in maint shafts, and blocks lines of sight perodically when active.
 */
// tg: code/game/objects/structures/maintenance.dm:260
public partial class obj_structure_steam_vent : obj_structure
{
    // tg: code/game/objects/structures/maintenance.dm:274
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (prob(75))
            vent_active = FALSE;
        DmList loc_connections = TgState.Current.obj_structure_steam_vent__Initialize__loc_connections ??= Dm.list(
            Dm.kv(COMSIG_ATOM_EXIT, nameof(blow_steam)));
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), loc_connections));
        register_context();
        Waiting.NoSleep(update_icon_state());
        return dot;
    }

    // tg: code/game/objects/structures/maintenance.dm:285
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (!(steam_vent_interact <= world.time)) // COOLDOWN_FINISHED(src, steam_vent_interact)
        {
            balloon_alert(user, I18n.T("not ready to adjust!"));
            return dot;
        }
        vent_active = Dm.not(vent_active);
        await update_icon_state();
        if (Dm.truthy(vent_active))
            balloon_alert(user, I18n.T("vent on"));
        else
        {
            balloon_alert(user, I18n.T("vent off"));
            return dot;
        }
        blow_steam();
        return dot;
    }

    // tg: code/game/objects/structures/maintenance.dm:299
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);
        if (held_item == null)
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.truthy(vent_active) ? "Close valve" : "Open valve";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        if (Dm.eq(held_item.tool_behaviour, TOOL_WRENCH))
        {
            context[SCREENTIP_CONTEXT_RMB] = "Deconstruct";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/game/objects/structures/maintenance.dm:309
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.wrench_act_secondary(user, tool);
        if (Dm.truthy(vent_active))
        {
            balloon_alert(user, I18n.T("must be off!"));
            return dot;
        }
        if (Dm.truthy(await tool.use_tool(this, user, 3 * SECONDS)))
        {
            playsound(loc, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
            await deconstruct();
            return TRUE;
        }
        return dot;
    }

    // tg: code/game/objects/structures/maintenance.dm:319
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        Dm.@new<obj_item_stack_sheet_iron>(loc);
        Dm.@new<obj_item_stock_parts_water_recycler>(loc, 1);
        await DmTask.CompletedTask;
    }

    /**
     * Creates "steam" smoke, and determines when the vent needs to block line of sight via reset_opacity.
     */
    // tg: code/game/objects/structures/maintenance.dm:326
    [SignalHandler]
    public virtual void blow_steam(datum source = null, atom_movable leaving = null, object direction = null)
    {
        if (!Dm.truthy(vent_active))
            return;
        if (!(steam_vent_interact <= world.time)) // COOLDOWN_FINISHED(src, steam_vent_interact)
            return;
        if (!ismob(leaving))
            return;
        Waiting.NoSleep(do_smoke(1, this, loc));
        playsound(this, Dm.resource("sound/machines/steam_hiss.ogg"), 75, TRUE, -2);
        steam_vent_interact = world.time + steam_speed; // COOLDOWN_START(src, steam_vent_interact, steam_speed)
    }

    // tg: code/game/objects/structures/maintenance.dm:338
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("steam_vent[]", Dm.truthy(vent_active) ? "" : "_off");
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/structures/maintenance.dm:18  /obj/structure/moisture_trap's var/static/list/loot_table
    internal DmList obj_structure_moisture_trap__loot_table;

    // tg: code/game/objects/structures/maintenance.dm:278  /obj/structure/steam_vent/Initialize's var/static/list/loc_connections
    internal DmList obj_structure_steam_vent__Initialize__loc_connections;
}
