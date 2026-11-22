// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/mystery_box.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST5 (D333): the file whole. The mystery box: a hand opens it (tg's crate sound and its jingle on a reserved channel), a
// prize rises out of it through vis_contents in tg's rays filter, its picture changing ever slower for five seconds as tg's timers
// pick types from the box's pool; then it is presented (an outline), claimable by a hand on the box or on the prize for eight
// seconds while it sinks back; the claimed item comes into the hand, a gun unlocked with a spare magazine for a ballistic one; the
// box closes, shakes when ready again, and breaks down into planks when its uses are spent. The pools: any sane item, the summoned
// guns (`guns`), tg's arena list (`tdome`), the magic list (`wands`), every sane gun (`wildcard`), the pirate treasure chest
// (`fishing`: three opens a mind). A prize whose own code is not ported yet is a thing only (D132). The var blocks are generated (D111).

// tg: code/game/objects/structures/mystery_box.dm:1-19 (file-local, #undef at :380-387)
public partial class obj_structure_mystery_box
{
    // the different states of the mystery box
    /// Closed, can't interact
    internal const float MYSTERY_BOX_COOLING_DOWN = 0;
    /// Closed, ready to be interacted with
    internal const float MYSTERY_BOX_STANDBY = 1;
    /// The box is choosing the prize
    internal const float MYSTERY_BOX_CHOOSING = 2;
    /// The box is presenting the prize, for someone to claim it
    internal const float MYSTERY_BOX_PRESENTING = 3;

    // delays for the different stages of the box's state, the visuals, and the audio
    /// How long the box takes to decide what the prize is
    internal const float MBOX_DURATION_CHOOSING = 5 * SECONDS;
    /// How long the box takes to start expiring the offer, though it's still valid until MBOX_DURATION_EXPIRING finishes. Timed to the sound clips
    internal const float MBOX_DURATION_PRESENTING = 3.5f * SECONDS;
    /// How long the box takes to start lowering the prize back into itself. When this finishes, the prize is gone
    internal const float MBOX_DURATION_EXPIRING = 4.5f * SECONDS;
    /// How long after the box closes until it can go again
    internal const float MBOX_DURATION_STANDBY = 2.7f * SECONDS;
}

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/game/objects/structures/mystery_box.dm:21
    public DmList mystery_box_guns;

    // tg: code/game/objects/structures/mystery_box.dm:21  (GLOBAL_LIST_INIT(mystery_box_guns, list(...)), __DEFINES/__globals.dm)
    public virtual void InitGlobalmystery_box_guns()
    {
        mystery_box_guns = Dm.list(
            typeof(obj_item_gun_energy_recharge_ebow_large),
            typeof(obj_item_gun_energy_e_gun),
            typeof(obj_item_gun_energy_e_gun_nuclear),
            typeof(obj_item_gun_energy_laser),
            typeof(obj_item_gun_energy_laser_soul),
            typeof(obj_item_gun_energy_laser_pistol),
            typeof(obj_item_gun_energy_laser_assault),
            typeof(obj_item_gun_energy_laser_hellgun),
            typeof(obj_item_gun_energy_laser_captain),
            typeof(obj_item_gun_energy_laser_scatter),
            typeof(obj_item_gun_energy_temperature),
            typeof(obj_item_gun_ballistic_revolver_c38_detective),
            typeof(obj_item_gun_ballistic_revolver_mateba),
            typeof(obj_item_gun_ballistic_automatic_pistol_deagle_camo),
            typeof(obj_item_gun_ballistic_automatic_pistol_suppressed),
            typeof(obj_item_gun_energy_pulse_carbine_taserless),
            typeof(obj_item_gun_energy_pulse_pistol_taserless),
            typeof(obj_item_gun_ballistic_shotgun_lethal),
            typeof(obj_item_gun_ballistic_shotgun_automatic_combat),
            typeof(obj_item_gun_ballistic_shotgun_bulldog_unrestricted),
            typeof(obj_item_gun_ballistic_rifle_boltaction),
            typeof(obj_item_gun_ballistic_automatic_ar),
            typeof(obj_item_gun_ballistic_automatic_proto_unrestricted),
            typeof(obj_item_gun_ballistic_automatic_c20r_unrestricted),
            typeof(obj_item_gun_ballistic_automatic_l6_saw_unrestricted),
            typeof(obj_item_gun_ballistic_automatic_m90_unrestricted),
            typeof(obj_item_gun_ballistic_automatic_tommygun),
            typeof(obj_item_gun_ballistic_automatic_wt550),
            typeof(obj_item_gun_ballistic_automatic_smartgun),
            typeof(obj_item_gun_ballistic_rifle_sniper_rifle),
            typeof(obj_item_gun_ballistic_rifle_boltaction));
        Dm.append(ref gvars_datum_init_order, "mystery_box_guns");
    }

    // tg: code/game/objects/structures/mystery_box.dm:55
    public DmList mystery_box_extended;

    // tg: code/game/objects/structures/mystery_box.dm:55  (GLOBAL_LIST_INIT(mystery_box_extended, list(...)))
    public virtual void InitGlobalmystery_box_extended()
    {
        mystery_box_extended = Dm.list(
            typeof(obj_item_clothing_gloves_tackler_combat),
            typeof(obj_item_clothing_gloves_race),
            typeof(obj_item_clothing_gloves_rapid),
            typeof(obj_item_shield_riot_flash),
            typeof(obj_item_grenade_stingbang_mega),
            typeof(obj_item_storage_belt_sheath_sabre),
            typeof(obj_item_knife_combat),
            typeof(obj_item_melee_baton_security_loaded),
            typeof(obj_item_reagent_containers_hypospray_combat),
            typeof(obj_item_defibrillator_compact_combat_loaded_nanotrasen),
            typeof(obj_item_melee_energy_sword_saber),
            typeof(obj_item_spear),
            typeof(obj_item_circular_saw));
        Dm.append(ref gvars_datum_init_order, "mystery_box_extended");
    }

    // tg: code/game/objects/structures/mystery_box.dm:71
    public DmList mystery_magic;

    // tg: code/game/objects/structures/mystery_box.dm:71  (GLOBAL_LIST_INIT(mystery_magic, list(...)))
    public virtual void InitGlobalmystery_magic()
    {
        mystery_magic = Dm.list(
            typeof(obj_item_gun_magic_wand_arcane_barrage),
            typeof(obj_item_gun_magic_wand_arcane_barrage_blood),
            typeof(obj_item_gun_magic_wand_fireball),
            typeof(obj_item_gun_magic_wand_resurrection),
            typeof(obj_item_gun_magic_wand_teleport),
            typeof(obj_item_gun_magic_wand_door),
            typeof(obj_item_gun_magic_wand_nothing),
            typeof(obj_item_storage_belt_wands_full),
            typeof(obj_item_storage_belt_wands_full_discount),
            typeof(obj_item_gun_magic_staff_healing),
            typeof(obj_item_gun_magic_staff_chaos),
            typeof(obj_item_gun_magic_staff_door),
            typeof(obj_item_gun_magic_staff_honk),
            typeof(obj_item_gun_magic_staff_spellblade),
            typeof(obj_item_gun_magic_staff_flying),
            typeof(obj_item_gun_magic_staff_babel),
            typeof(obj_item_singularityhammer),
            typeof(obj_item_runic_vendor_scepter));
        Dm.append(ref gvars_datum_init_order, "mystery_magic");
    }

    // tg: code/game/objects/structures/mystery_box.dm:92
    public DmList mystery_fishing;

    // tg: code/game/objects/structures/mystery_box.dm:92  (GLOBAL_LIST_INIT(mystery_fishing, list(...)))
    public virtual void InitGlobalmystery_fishing()
    {
        mystery_fishing = Dm.list(
            typeof(obj_item_storage_toolbox_fishing_master),
            typeof(obj_item_storage_box_fish_revival_kit),
            typeof(obj_item_fishing_rod_telescopic_master),
            typeof(obj_item_bait_can_super_baits),
            typeof(obj_item_storage_fish_case_tiziran),
            typeof(obj_item_storage_fish_case_syndicate),
            typeof(obj_item_claymore_cutlass_old),
            typeof(obj_item_gun_energy_laser_retro_old),
            typeof(obj_item_gun_energy_laser_musket),
            typeof(obj_item_gun_energy_disabler_smoothbore),
            typeof(obj_item_gun_ballistic_rifle_boltaction_surplus),
            typeof(obj_item_food_rationpack),
            typeof(obj_item_food_canned_squid_ink),
            typeof(obj_item_reagent_containers_cup_glass_bottle_rum_aged),
            typeof(obj_item_storage_bag_money_dutchmen),
            typeof(obj_item_language_manual_piratespeak),
            typeof(obj_item_clothing_head_costume_pirate_armored),
            typeof(obj_item_clothing_suit_costume_pirate_armored),
            typeof(obj_structure_cannon_mystery_box),
            typeof(obj_item_stack_cannonball_trashball_four),
            typeof(obj_item_stack_cannonball_four));
        Dm.append(ref gvars_datum_init_order, "mystery_fishing");
    }
}

// tg: code/game/objects/structures/mystery_box.dm:116
public partial class obj_structure_mystery_box : obj_structure
{
    // tg: code/game/objects/structures/mystery_box.dm:152
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        generate_valid_types();
        return dot;
    }

    // tg: code/game/objects/structures/mystery_box.dm:156
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref presented_item);
        if (Dm.truthy(current_sound_channel))
            SSsounds.free_sound_channel(current_sound_channel);
        minds_that_opened_us = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/mystery_box.dm:163
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        switch (box_state)
        {
            case MYSTERY_BOX_STANDBY:
                activate(user as mob_living);
                break;

            case MYSTERY_BOX_PRESENTING:
                if (Dm.truthy(presented_item.claimable))
                    await grant_weapon(user as mob_living);
                break;
        }
        return dot;
    }

    // tg: code/game/objects/structures/mystery_box.dm:173
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", Dm.initial(this, (atom p) => p.icon_state), box_state > MYSTERY_BOX_STANDBY ? "open" : "");
        return await base.update_icon_state(__extra);
    }

    /// This proc is used to define what item types valid_types is filled with
    // tg: code/game/objects/structures/mystery_box.dm:178
    public virtual void generate_valid_types()
    {
        valid_types = get_sane_item_types(selectable_base_type) as DmList;
    }

    // tg: code/game/objects/structures/mystery_box.dm:181
    public override object IsContainedAtomAccessible(atom contained = null, atom_movable user = null) => TRUE;

    /// The box has been activated, play the sound and spawn the prop item
    // tg: code/game/objects/structures/mystery_box.dm:185
    public virtual void activate(mob_living user = null)
    {
        box_state = MYSTERY_BOX_CHOOSING;
        Waiting.NoSleep(update_icon_state());
        presented_item = Dm.@new<obj_effect_abstract_mystery_box_item>(this);
        presented_item.vis_flags = VIS_INHERIT_PLANE;
        Dm.append(vis_contents, presented_item);
        presented_item.start_animation(this);
        current_sound_channel = SSsounds.reserve_sound_channel_for_datum(this);
        playsound(this, open_sound, 70, FALSE, channel: Dm.num_or_null(current_sound_channel), falloff_exponent: 10);
        playsound(this, crate_open_sound, 80);
        if (user.mind != null)
        {
            LAZYINITLIST(ref minds_that_opened_us);
            datum_weakref @ref = WEAKREF(user.mind) as datum_weakref;
            minds_that_opened_us[@ref] = Dm.num(minds_that_opened_us[@ref]) + 1;
        }
        uses_left--;
    }

    /// The box has finished choosing, mark it as available for grabbing
    // tg: code/game/objects/structures/mystery_box.dm:202
    public virtual void present_weapon()
    {
        visible_message(span_notice(I18n.T("[] presents []!", this, presented_item)), vision_distance: COMBAT_MESSAGE_RANGE);
        box_state = MYSTERY_BOX_PRESENTING;
        box_expire_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(start_expire_offer)), MBOX_DURATION_PRESENTING, TIMER_STOPPABLE, file: "code/game/objects/structures/mystery_box.dm", line: 205);
    }

    /// The prize is still claimable, but the animation will show it start to recede back into the box
    // tg: code/game/objects/structures/mystery_box.dm:208
    public virtual void start_expire_offer()
    {
        presented_item.expire_animation();
        box_close_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(close_box)), MBOX_DURATION_EXPIRING, TIMER_STOPPABLE, file: "code/game/objects/structures/mystery_box.dm", line: 210);
    }

    /// The box is closed, whether because the prize fully expired, or it was claimed. Start resetting all of the state stuff
    // tg: code/game/objects/structures/mystery_box.dm:213
    public virtual async DmTask close_box()
    {
        box_state = MYSTERY_BOX_COOLING_DOWN;
        await update_icon_state();
        QDEL_NULL(ref presented_item);
        deltimer(box_close_timer);
        deltimer(box_expire_timer);
        playsound(this, crate_close_sound, 100);
        box_close_timer = null;
        box_expire_timer = null;
        _addtimer(Dm.@new<datum_callback>(this, nameof(ready_again)), MBOX_DURATION_STANDBY, file: "code/game/objects/structures/mystery_box.dm", line: 222);
        if (uses_left <= 0)
        {
            visible_message(I18n.T("[] breaks down.", this));
            await deconstruct(disassembled: FALSE);
        }
    }

    /// The cooldown between activations has finished, shake to show that
    // tg: code/game/objects/structures/mystery_box.dm:228
    public virtual void ready_again()
    {
        SSsounds.free_sound_channel(current_sound_channel);
        current_sound_channel = null;
        box_state = MYSTERY_BOX_STANDBY;
        Shake(3, 0, 0.5f * SECONDS);
    }

    /// Someone attacked the box with an empty hand, spawn the shown prize and give it to them, then close the box
    // tg: code/game/objects/structures/mystery_box.dm:235
    public virtual async DmTask grant_weapon(mob_living user = null)
    {
        atom_movable instantiated_weapon = Dm.@new(presented_item.selected_path, loc) as atom_movable;
        user.visible_message(span_notice(I18n.T("[] takes [] from [].", user, presented_item, this)), span_notice(I18n.T("You take [] from [].", presented_item, this)), vision_distance: COMBAT_MESSAGE_RANGE);
        playsound(this, grant_sound, 70, FALSE, channel: Dm.num_or_null(current_sound_channel), falloff_exponent: 10);
        await close_box();

        if (!isitem(instantiated_weapon))
            return;
        await user.put_in_hands((obj_item)instantiated_weapon);

        if (!isgun(instantiated_weapon))
            return;
        // handle pins + possibly extra ammo
        obj_item_gun instantiated_gun = (obj_item_gun)instantiated_weapon;
        await instantiated_gun.unlock();
        if (!Dm.truthy(grant_extra_mag) || instantiated_gun is not obj_item_gun_ballistic)
            return;
        obj_item_gun_ballistic instantiated_ballistic = (obj_item_gun_ballistic)instantiated_gun;
        if (!Dm.truthy(instantiated_ballistic.internal_magazine))
        {
            obj_item_ammo_box_magazine extra_mag = Dm.@new(instantiated_ballistic.spawn_magazine_type, loc) as obj_item_ammo_box_magazine;
            await user.put_in_hands(extra_mag);
        }
    }

    // tg: code/game/objects/structures/mystery_box.dm:292
    public override async DmTask handle_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        Dm.@new<obj_item_stack_sheet_mineral_wood>(drop_location(), 2);
        await base.handle_deconstruct(disassembled);
    }
}

// tg: code/game/objects/structures/mystery_box.dm:257
public partial class obj_structure_mystery_box_guns : obj_structure_mystery_box
{
    // tg: code/game/objects/structures/mystery_box.dm:260
    public override void generate_valid_types()
    {
        valid_types = GLOB.summoned_guns;
    }
}

// tg: code/game/objects/structures/mystery_box.dm:263
public partial class obj_structure_mystery_box_tdome : obj_structure_mystery_box
{
    // tg: code/game/objects/structures/mystery_box.dm:266
    public override void generate_valid_types()
    {
        valid_types = Dm.add(GLOB.mystery_box_guns, GLOB.mystery_box_extended) as DmList;
    }
}

// tg: code/game/objects/structures/mystery_box.dm:269
public partial class obj_structure_mystery_box_wands : obj_structure_mystery_box
{
    // tg: code/game/objects/structures/mystery_box.dm:272
    public override void generate_valid_types()
    {
        valid_types = GLOB.mystery_magic;
    }
}

// tg: code/game/objects/structures/mystery_box.dm:275
public partial class obj_structure_mystery_box_wildcard : obj_structure_mystery_box
{
    // tg: code/game/objects/structures/mystery_box.dm:278
    public override void generate_valid_types()
    {
        valid_types = GLOB.summoned_all_guns;
    }
}

///A fishing and pirate-themed mystery box, rarely found by fishing in the ocean, then another cannot be caught for the next 30 minutes.
// tg: code/game/objects/structures/mystery_box.dm:282
public partial class obj_structure_mystery_box_fishing : obj_structure_mystery_box
{
    // tg: code/game/objects/structures/mystery_box.dm:296
    public override void generate_valid_types()
    {
        valid_types = GLOB.mystery_fishing;
    }

    // tg: code/game/objects/structures/mystery_box.dm:299
    public override void activate(mob_living user = null)
    {
        if (user.mind != null && Dm.num(minds_that_opened_us?[WEAKREF(user.mind)]) >= 3)
        {
            to_chat(user, span_warning(I18n.T("[] refuses to open to you anymore. Perhaps you should present it to someone else...", this)));
            return;
        }
        base.activate(user);
    }
}

/// This represents the item that comes out of the box and is constantly changing before the box finishes deciding. Can probably be just an /atom or /movable.
// tg: code/game/objects/structures/mystery_box.dm:306
public partial class obj_effect_abstract_mystery_box_item : obj_effect_abstract
{
    // tg: code/game/objects/structures/mystery_box.dm:322
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        matrix starting = Dm.@new<matrix>();
        starting.Scale(0.5f, 0.5f);
        transform = starting;
        add_filter("weapon_rays", 3, Dm.list(Dm.kv("type", "rays"), Dm.kv("size", 28), Dm.kv("color", COLOR_VIVID_YELLOW)));
        return dot;
    }

    // tg: code/game/objects/structures/mystery_box.dm:329
    public override object Destroy(float? force = null)
    {
        parent_box = null;
        return base.Destroy(force);
    }

    // this way, clicking on the prize will work the same as clicking on the box
    // tg: code/game/objects/structures/mystery_box.dm:334
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(claimable))
            await parent_box.grant_weapon(user as mob_living);
        return dot;
    }

    /// Start pushing the prize up
    // tg: code/game/objects/structures/mystery_box.dm:340
    public virtual void start_animation(atom parent = null)
    {
        parent_box = parent as obj_structure_mystery_box;
        loop_icon_changes();
    }

    /// Keep changing the icon and selected path
    // tg: code/game/objects/structures/mystery_box.dm:345
    public virtual void loop_icon_changes()
    {
        float change_delay = 1; // the running count of the delay
        float change_delay_delta = 1; // How much to increment the delay per step so the changing slows down
        float change_counter = 0; // The running count of the running count

        matrix starting = Dm.@new<matrix>();
        animate(this, Dm.named("pixel_z", 10), Dm.named("transform", starting), Dm.named("time", obj_structure_mystery_box.MBOX_DURATION_CHOOSING), Dm.named("easing", Dm.bitor(QUAD_EASING, EASE_OUT)));

        while ((change_counter + change_delay_delta + change_delay) < obj_structure_mystery_box.MBOX_DURATION_CHOOSING)
        {
            change_delay += change_delay_delta;
            change_counter += change_delay;
            selected_path = pick(parent_box.valid_types) as Type;
            _addtimer(Dm.@new<datum_callback>(this, nameof(update_random_icon), selected_path), change_counter, file: "code/game/objects/structures/mystery_box.dm", line: 357);
        }

        _addtimer(Dm.@new<datum_callback>(this, nameof(present_item)), obj_structure_mystery_box.MBOX_DURATION_CHOOSING, file: "code/game/objects/structures/mystery_box.dm", line: 359);
    }

    /// animate() isn't up to the task for queueing up icon changes, so this is the proc we call with timers to update our icon
    // tg: code/game/objects/structures/mystery_box.dm:362
    public virtual void update_random_icon(object new_item_type = null)
    {
        Type new_item = new_item_type as Type;
        icon = Dm.initial(new_item, (atom p) => p.icon);
        icon_state = Dm.initial(new_item, (atom p) => p.icon_state);
    }

    // tg: code/game/objects/structures/mystery_box.dm:367
    public virtual void present_item()
    {
        Type selected_item = selected_path;
        add_filter("ready_outline", 2, Dm.list(Dm.kv("type", "outline"), Dm.kv("color", COLOR_VIVID_YELLOW), Dm.kv("size", 0.2f)));
        name = Dm.initial(selected_item, (atom p) => p.name);
        parent_box.present_weapon();
        claimable = TRUE;
    }

    /// Sink back into the box
    // tg: code/game/objects/structures/mystery_box.dm:375
    public virtual void expire_animation()
    {
        matrix shrink_back = Dm.@new<matrix>();
        shrink_back.Scale(0.5f, 0.5f);
        animate(this, Dm.named("pixel_z", -4), Dm.named("transform", shrink_back), Dm.named("time", obj_structure_mystery_box.MBOX_DURATION_EXPIRING));
    }
}
