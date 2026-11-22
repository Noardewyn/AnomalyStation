// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the var block (the M2 contracts step), Initialize and Destroy (M2 P2 body: the /obj/item core that the
// bodyparts and organs, as items, run when they are created and deleted), update_greyscale and the equip callbacks
// on_equipped, visual_equipped, equipped, mob_can_equip and on_outfit_equip (M2 F1: a player's jumpsuit) translated; the
// other procs are roots with Dm.NotTranslated bodies (inventory, dropping, attacks, throwing and tools: M4a; the embedding
// procs get_embed, set_embed and force_embed: M6 slice 3b; get_dismember_sound: M6 slice 4; the grinding and juicing procs
// and /atom/movable/blended, :1011-1131: E15, the plumbing chemical grinder; the rest with their modules).

/// Anything you can pick up and hold.
// tg: code/game/objects/items.dm:2
// [DmPath("/obj/item")] is on the part in code/_onclick/adjacent.dm, the earliest .dme file that declares /obj/item (rules 2.3).
// The var block (items.dm:2-242) is translated in full by the M2 contracts step.
public partial class obj_item : obj
{
    // The var overrides of the type block (rules 10.2), in DM declaration order.
    public obj_item()
    {
        // tg: code/game/objects/items.dm:3
        name = "item";
        // tg: code/game/objects/items.dm:4
        icon = Dm.resource("icons/obj/anomaly.dmi");
        // tg: code/game/objects/items.dm:5
        abstract_type = typeof(obj_item);
        // tg: code/game/objects/items.dm:6
        blocks_emissive = EMISSIVE_BLOCK_GENERIC;
        // tg: code/game/objects/items.dm:7
        burning_particles = typeof(particles_smoke_burning_small);
        // tg: code/game/objects/items.dm:8
        pass_flags_self = PASSITEM;
        // tg: code/game/objects/items.dm:9
        interaction_flags_atom = INTERACT_ATOM_UI_INTERACT;
        // tg: code/game/objects/items.dm:71
        max_integrity = 200;
        // tg: code/game/objects/items.dm:73
        obj_flags = NONE;
        // tg: code/game/objects/items.dm:104
        pass_flags = PASSTABLE;
        // tg: code/game/objects/items.dm:105
        pressure_resistance = 4;
        ///Items can by default thrown up to 10 tiles by TK users
        // tg: code/game/objects/items.dm:179
        tk_throw_range = 10;
        ///the icon to indicate this object is being dragged
        // tg: code/game/objects/items.dm:182
        mouse_drag_pointer = MOUSE_ACTIVE_POINTER;
    }

    /* !!!!!!!!!!!!!!! IMPORTANT !!!!!!!!!!!!!!

        IF YOU ADD MORE ICON CRAP TO THIS
        ENSURE YOU ALSO ADD THE NEW VARS TO CHAMELEON ITEM_ACTION'S update_item() PROC (/datum/action/item_action/chameleon/change/proc/update_item())
        WASHING MASHINE'S dye_item() PROC (/obj/item/proc/dye_item())
        AND ALSO TO THE CHANGELING PROFILE DISGUISE SYSTEMS (/datum/changeling_profile / /datum/antagonist/changeling/proc/create_profile() / /proc/changeling_transform())

        !!!!!!!!!!!!!!! IMPORTANT !!!!!!!!!!!!!! */

    ///icon state for inhand overlays.
    // tg: code/game/objects/items.dm:21
    public object inhand_icon_state = null;
    ///Icon file for left hand inhand overlays
    // tg: code/game/objects/items.dm:23
    public object lefthand_file = Dm.resource("icons/mob/inhands/items_lefthand.dmi");
    ///Icon file for right inhand overlays
    // tg: code/game/objects/items.dm:25
    public object righthand_file = Dm.resource("icons/mob/inhands/items_righthand.dmi");

    /// Angle of the icon, used for piercing and slashing attack animations, clockwise from *east-facing* sprites
    // tg: code/game/objects/items.dm:28
    public float icon_angle = 0;
    ///icon file for an alternate attack icon
    // tg: code/game/objects/items.dm:30
    public object attack_icon;
    ///icon state for an alternate attack icon
    // tg: code/game/objects/items.dm:32
    public object attack_icon_state;

    ///Icon file for mob worn overlays.
    // tg: code/game/objects/items.dm:35  var/icon/worn_icon: object, tg stores icon files (resources) in it (rules 3.2,
    // "Typed vars that hold wider values")
    public object worn_icon;
    ///Icon state for mob worn overlays, if null the normal icon_state will be used.
    // tg: code/game/objects/items.dm:37
    public object worn_icon_state;
    ///Icon state for the belt overlay, if null the normal icon_state will be used.
    // tg: code/game/objects/items.dm:39
    public object inside_belt_icon_state;
    ///Forced mob worn layer instead of the standard preferred size.
    // tg: code/game/objects/items.dm:41
    public object alternate_worn_layer;
    ///The config type to use for greyscaled worn sprites. Both this and greyscale_colors must be assigned to work.
    // tg: code/game/objects/items.dm:43  (V3: tg assigns typepaths)
    public Type greyscale_config_worn;
    ///The config type to use for greyscaled left inhand sprites. Both this and greyscale_colors must be assigned to work.
    // tg: code/game/objects/items.dm:45  (V3: tg assigns typepaths)
    public Type greyscale_config_inhand_left;
    ///The config type to use for greyscaled right inhand sprites. Both this and greyscale_colors must be assigned to work.
    // tg: code/game/objects/items.dm:47  (V3: tg assigns typepaths)
    public Type greyscale_config_inhand_right;
    ///The config type to use for greyscaled belt overlays. Both this and greyscale_colors must be assigned to work.
    // tg: code/game/objects/items.dm:49  (V3: tg assigns typepaths)
    public Type greyscale_config_belt;

    /* !!!!!!!!!!!!!!! IMPORTANT !!!!!!!!!!!!!!

        IF YOU ADD MORE ICON CRAP TO THIS
        ENSURE YOU ALSO ADD THE NEW VARS TO CHAMELEON ITEM_ACTION'S update_item() PROC (/datum/action/item_action/chameleon/change/proc/update_item())
        WASHING MASHINE'S dye_item() PROC (/obj/item/proc/dye_item())
        AND ALSO TO THE CHANGELING PROFILE DISGUISE SYSTEMS (/datum/changeling_profile / /datum/antagonist/changeling/proc/create_profile() / /proc/changeling_transform())

        !!!!!!!!!!!!!!! IMPORTANT !!!!!!!!!!!!!! */

    ///Dimensions of the icon file used when this item is worn, eg: hats.dmi (32x32 sprite, 64x64 sprite, etc.). Allows inhands/worn sprites to be of any size, but still centered on a mob properly
    // tg: code/game/objects/items.dm:61
    public float worn_x_dimension = 32;
    ///Dimensions of the icon file used when this item is worn, eg: hats.dmi (32x32 sprite, 64x64 sprite, etc.). Allows inhands/worn sprites to be of any size, but still centered on a mob properly
    // tg: code/game/objects/items.dm:63
    public float worn_y_dimension = 32;
    ///Same as for [worn_x_dimension][/obj/item/var/worn_x_dimension] but for inhands, uses the lefthand_ and righthand_ file vars
    // tg: code/game/objects/items.dm:65
    public float inhand_x_dimension = 32;
    ///Same as for [worn_y_dimension][/obj/item/var/worn_y_dimension] but for inhands, uses the lefthand_ and righthand_ file vars
    // tg: code/game/objects/items.dm:67
    public float inhand_y_dimension = 32;
    /// Worn overlay will be shifted by this along y axis
    // tg: code/game/objects/items.dm:69
    public float worn_y_offset = 0;

    // (max_integrity = 200, items.dm:71, and obj_flags = NONE, :73: overrides, in the constructor above)
    ///Item flags for the item
    // tg: code/game/objects/items.dm:75
    public float? item_flags = NONE;

    ///Sound played when you hit something with the item
    // tg: code/game/objects/items.dm:78
    public object hitsound;
    ///Played when the item is used, for example tools
    // tg: code/game/objects/items.dm:80
    public object usesound;
    ///Played when item is used for long progress
    // tg: code/game/objects/items.dm:82
    public object operating_sound;
    ///Used when yate into a mob
    // tg: code/game/objects/items.dm:84
    public object mob_throw_hit_sound;
    ///Sound used when equipping the item into a valid slot
    // tg: code/game/objects/items.dm:86
    public object equip_sound;
    ///Sound uses when picking the item up (into your hands)
    // tg: code/game/objects/items.dm:88
    public object pickup_sound;
    ///Sound uses when dropping the item, or when its thrown if a thrown sound isn't specified.
    // tg: code/game/objects/items.dm:90
    public object drop_sound;
    ///Sound used on impact when the item is thrown.
    // tg: code/game/objects/items.dm:92
    public object throw_drop_sound;
    ///Do the drop and pickup sounds vary?
    // tg: code/game/objects/items.dm:94
    public float sound_vary = FALSE;
    ///Whether or not we use stealthy audio levels for this item's attack sounds
    // tg: code/game/objects/items.dm:96
    public float stealthy_audio = FALSE;
    ///Sound which is produced when blocking an attack
    // tg: code/game/objects/items.dm:98
    public object block_sound;

    ///How large is the object, used for stuff like whether it can fit in backpacks or not
    // tg: code/game/objects/items.dm:101
    public float w_class = WEIGHT_CLASS_NORMAL;
    ///This is used to determine on which slots an item can fit.
    // tg: code/game/objects/items.dm:103
    public float? slot_flags = NONE;
    // (pass_flags = PASSTABLE, items.dm:104, and pressure_resistance = 4, :105: overrides, in the constructor above)
    /// This var exists as a weird proxy "owner" ref
    /// It's used in a few places. Stop using it, and optimially replace all uses please
    // tg: code/game/objects/items.dm:108
    public obj_item master = null;

    ///Price of an item in a vending machine, overriding the base vending machine price. Define in terms of paycheck defines as opposed to raw numbers.
    // tg: code/game/objects/items.dm:111
    public object custom_price;
    ///Price of a premium and contraband item in a vending machine, overriding the premium vending machine price. Define in terms of paycheck defines as opposed to raw numbers.
    // tg: code/game/objects/items.dm:113
    public object custom_premium_price;
    ///Whether spessmen with an ID with an age below AGE_MINOR (20 by default) can buy this item
    // tg: code/game/objects/items.dm:115
    public float age_restricted = FALSE;

    ///flags which determine which body parts are protected from heat. [See here][HEAD]
    // tg: code/game/objects/items.dm:118
    public float? heat_protection = 0;
    ///flags which determine which body parts are protected from cold. [See here][HEAD]
    // tg: code/game/objects/items.dm:120
    public float? cold_protection = 0;
    ///Set this variable to determine up to which temperature (IN KELVIN) the item protects against heat damage. Keep at null to disable protection. Only protects areas set by heat_protection flags
    // tg: code/game/objects/items.dm:122
    public object max_heat_protection_temperature;
    ///Set this variable to determine down to which temperature (IN KELVIN) the item protects against cold damage. 0 is NOT an acceptable number due to if(varname) tests!! Keep at null to disable protection. Only protects areas set by cold_protection flags
    // tg: code/game/objects/items.dm:124
    public object min_cold_protection_temperature;

    ///list of /datum/action's that this item has.
    // tg: code/game/objects/items.dm:127
    public DmList actions;
    ///list of paths of action datums to give to the item on New().
    // tg: code/game/objects/items.dm:129
    public DmList actions_types;
    ///Slot flags in which this item grants actions. If null, defaults to the item's slot flags (so actions are granted when worn)
    // tg: code/game/objects/items.dm:131
    public object action_slots = null;

    //Since any item can now be a piece of clothing, this has to be put here so all items share it.
    ///This flag is used to determine when items in someone's inventory cover others. IE helmets making it so you can't see glasses, etc.
    // tg: code/game/objects/items.dm:135
    public object flags_inv;
    ///you can see someone's mask through their transparent visor, but you can't reach it
    // tg: code/game/objects/items.dm:137
    public float transparent_protection = NONE;
    ///Path of type /datum/hair_mask to apply to hair when this item is worn
    ///Used by certain hats to give the appearance of squishing down tall hairstyles without hiding the hair completely
    // tg: code/game/objects/items.dm:140  (V3: tg assigns typepaths)
    public Type hair_mask = null;

    ///flags for what should be done when you click on the item, default is picking it up
    // tg: code/game/objects/items.dm:143
    public float interaction_flags_item = INTERACT_ITEM_ATTACK_HAND_PICKUP;

    ///What body parts are covered by the clothing when you wear it
    // tg: code/game/objects/items.dm:146
    public float body_parts_covered = 0;
    /// for electrical admittance/conductance (electrocution checks and shit)
    // tg: code/game/objects/items.dm:148
    public float siemens_coefficient = 1;
    /// How much clothing is slowing you down. Negative values speeds you up
    // tg: code/game/objects/items.dm:150
    public float slowdown = 0;
    ///percentage of armour effectiveness to remove
    // tg: code/game/objects/items.dm:152
    public float armour_penetration = 0;
    ///Whether or not our object doubles the value of affecting armour
    // tg: code/game/objects/items.dm:154
    public float weak_against_armour = FALSE;
    /// The click cooldown given after attacking. Lower numbers means faster attacks
    // tg: code/game/objects/items.dm:156
    public float attack_speed = CLICK_CD_MELEE;
    /// The click cooldown on secondary attacks. Lower numbers mean faster attacks. Will use attack_speed if undefined.
    // tg: code/game/objects/items.dm:158
    public object secondary_attack_speed;
    ///In deciseconds, how long an item takes to equip; counts only for normal clothing slots, not pockets etc.
    // tg: code/game/objects/items.dm:160
    public float equip_delay_self = 0 * SECONDS;
    ///In deciseconds, how long an item takes to put on another person
    // tg: code/game/objects/items.dm:162
    public float equip_delay_other = 2 * SECONDS;
    ///In deciseconds, how long an item takes to remove from another person
    // tg: code/game/objects/items.dm:164
    public float strip_delay = 4 * SECONDS;
    ///How long it takes to resist out of the item (cuffs and such)
    // tg: code/game/objects/items.dm:166
    public float? breakouttime = 0;

    ///Used in [atom/proc/attackby] to say how something was attacked `"[x] has been [z.attack_verb] by [y] with [z]"`
    // tg: code/game/objects/items.dm:169
    public DmList attack_verb_continuous;
    // tg: code/game/objects/items.dm:170
    public DmList attack_verb_simple;
    ///list() of species types, if a species cannot put items in a certain slot, but species type is in list, it will be able to wear that item
    // tg: code/game/objects/items.dm:172
    public DmList species_exception = null;
    ///This is a bitfield that defines what variations exist for bodyparts like Digi legs. See: code\_DEFINES\inventory.dm
    // tg: code/game/objects/items.dm:174
    public float supports_variations_flags = NONE;
    /// This is a bitfield that defines which bodyshapes this item is capable of rendering, used by build_worn_icon()
    // tg: code/game/objects/items.dm:176
    public float bodyshapes_with_variations = NONE;

    // (tk_throw_range = 10, items.dm:179, and mouse_drag_pointer = MOUSE_ACTIVE_POINTER, :182: overrides, in the
    // constructor above)

    /// Does it embed and if yes, what kind of embed
    // tg: code/game/objects/items.dm:185  (V3: tg assigns typepaths)
    public Type embed_type;
    /// Stores embedding data
    // tg: code/game/objects/items.dm:187  VAR_PROTECTED/datum/embedding/embed_data
    [DmProtected]
    public datum_embedding embed_data;

    ///for flags such as [GLASSESCOVERSEYES]
    // tg: code/game/objects/items.dm:190
    public float flags_cover = NONE;
    // tg: code/game/objects/items.dm:191
    public float heat = 0;
    /// All items with sharpness of SHARP_EDGED or higher will automatically get the butchering component.
    // tg: code/game/objects/items.dm:193
    public float sharpness = NONE;

    ///How a tool acts when you use it on something, such as wirecutters cutting wires while multitools measure power
    // tg: code/game/objects/items.dm:196
    public object tool_behaviour = null;

    ///How fast does the tool work
    // tg: code/game/objects/items.dm:199
    public float toolspeed = 1;

    ///Chance of blocking incoming attack
    // tg: code/game/objects/items.dm:202
    public float block_chance = 0;
    ///Effect of blocking
    // tg: code/game/objects/items.dm:204
    public Type block_effect = typeof(obj_effect_temp_visual_block);
    // tg: code/game/objects/items.dm:205
    public float hit_reaction_chance = 0; //If you want to have something unrelated to blocking/armour piercing etc. Maybe not needed, but trying to think ahead/allow more freedom
    ///In tiles, how far this weapon can reach; 1 for adjacent, which is default
    // tg: code/game/objects/items.dm:207
    public float reach = 1;

    ///The list of slots by priority. equip_to_appropriate_slot() uses this list. Doesn't matter if a mob type doesn't have a slot. For default list, see [/mob/proc/equip_to_appropriate_slot]
    // tg: code/game/objects/items.dm:210
    public DmList slot_equipment_priority = null;

    ///Reference to the datum that determines whether dogs can wear the item: Needs to be in /obj/item because corgis can wear a lot of non-clothing items
    // tg: code/game/objects/items.dm:213  var/datum/dog_fashion/dog_fashion = null: Type, tg assigns typepaths to it
    // (V3) and creates the datum with new dog_fashion(...)
    public Type dog_fashion = null;

    //Tooltip vars
    ///string form of an item's force. Edit this var only to set a custom force string
    // tg: code/game/objects/items.dm:217
    public object force_string;
    // tg: code/game/objects/items.dm:218
    public float last_force_string_check = 0;
    // tg: code/game/objects/items.dm:219
    public object tip_timer;

    ///Determines who can shoot this
    // tg: code/game/objects/items.dm:222
    public float trigger_guard = TRIGGER_GUARD_NONE;

    ///Used as the dye color source in the washing machine only (at the moment). Can be a hex color or a key corresponding to a registry entry, see washing_machine.dm
    // tg: code/game/objects/items.dm:225
    public object dye_color;
    ///Whether the item is unaffected by standard dying.
    // tg: code/game/objects/items.dm:227
    public float undyeable = FALSE;
    ///What dye registry should be looked at when dying this item; see washing_machine.dm
    // tg: code/game/objects/items.dm:229
    public object dying_key;

    /// Used in obj/item/examine to give additional notes on what the weapon does, separate from the predetermined output variables
    // tg: code/game/objects/items.dm:232
    public object offensive_notes;
    /// Used in obj/item/examine to determines whether or not to detail an item's statistics even if it does not meet the force requirements
    // tg: code/game/objects/items.dm:234
    public float override_notes = FALSE;
    /// Used if we want to have a custom verb text for throwing. "John Spaceman flicks the ciggerate" for example.
    // tg: code/game/objects/items.dm:236
    public object throw_verb;

    /// A lazylist used for applying fantasy values, contains the actual modification applied to a variable.
    // tg: code/game/objects/items.dm:239
    public DmList fantasy_modifications = null;

    /// Do we apply a click cooldown when resisting this object if it is restraining them?
    // tg: code/game/objects/items.dm:242
    public float resist_cooldown = CLICK_CD_BREAKOUT;

    // tg: code/game/objects/items.dm:383
    [DmParams("colors", "new_config", "new_worn_config", "new_inhand_left", "new_inhand_right")]
    public override async DmTask set_greyscale(object colors = null, object new_config = null, params object[] __extra)
    {
        Type new_worn_config = Dm.arg<Type>(__extra, 0);
        Type new_inhand_left = Dm.arg<Type>(__extra, 1);
        Type new_inhand_right = Dm.arg<Type>(__extra, 2);
        if (Dm.truthy(new_worn_config))
            greyscale_config_worn = new_worn_config;
        if (Dm.truthy(new_inhand_left))
            greyscale_config_inhand_left = new_inhand_left;
        if (Dm.truthy(new_inhand_right))
            greyscale_config_inhand_right = new_inhand_right;
        await base.set_greyscale(colors, new_config, Dm.forward(__extra, new_worn_config, new_inhand_left, new_inhand_right));
    }

    /// Checks if this atom uses the GAGS system and if so updates the worn and inhand icons
    // tg: code/game/objects/items.dm:393
    public override async DmTask update_greyscale()
    {
        await base.update_greyscale();
        if (!Dm.truthy(greyscale_colors))
            return;
        if (Dm.truthy(greyscale_config_worn))
            worn_icon = SSgreyscale.GetColoredIconByType(greyscale_config_worn, greyscale_colors);
        if (Dm.truthy(greyscale_config_inhand_left))
            lefthand_file = SSgreyscale.GetColoredIconByType(greyscale_config_inhand_left, greyscale_colors);
        if (Dm.truthy(greyscale_config_inhand_right))
            righthand_file = SSgreyscale.GetColoredIconByType(greyscale_config_inhand_right, greyscale_colors);
    }

    // tg: code/game/objects/items.dm:1308
    /// Returns a numeric value for sorting items used as parts in machines, so they can be replaced by the rped
    public virtual object get_part_rating()
    {
        return 0; // gaps OD3: the RPED's storage asks every item
    }
}

// tg: code/game/objects/items.dm:244
public partial class obj_item : obj
{
    // tg: code/game/objects/items.dm:244
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        if (Dm.truthy(attack_verb_continuous))
            attack_verb_continuous = string_list(attack_verb_continuous) as DmList;
        if (Dm.truthy(attack_verb_simple))
            attack_verb_simple = string_list(attack_verb_simple) as DmList;
        if (Dm.truthy(species_exception))
            species_exception = string_list(species_exception) as DmList;

        if (Dm.truthy(sharpness) && force > 5) //give sharp objects butchering functionality, for consistency
            Waiting.NoSleep(AddComponent(typeof(datum_component_butchering), Dm.named("speed", 8 * SECONDS * toolspeed)));

        if (!Dm.truthy(greyscale_config) && Dm.truthy(greyscale_colors) && (Dm.truthy(greyscale_config_worn) || Dm.truthy(greyscale_config_belt) || Dm.truthy(greyscale_config_inhand_right) || Dm.truthy(greyscale_config_inhand_left)))
            Waiting.NoSleep(update_greyscale());

        dot = base.Initialize(mapload, __extra);

        // Handle adding item associated actions
        foreach (object path in Dm.each(actions_types))
            Waiting.Detach(add_item_action(path));
        actions_types = null;

        if (Dm.truthy(force_string))
            Dm.combine(ref item_flags, FORCE_STRING_OVERRIDE);

        if (!Dm.truthy(hitsound))
        {
            if (damtype == BURN)
                hitsound = Dm.resource("sound/items/tools/welder.ogg");
            if (damtype == BRUTE)
                hitsound = SFX_SWING_HIT;
        }

        // SEND_GLOBAL_SIGNAL(COMSIG_GLOB_NEW_ITEM, src) (__DEFINES/dcs/helpers.dm:7), expanded inline (rules 11.1 (e))
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_NEW_ITEM]))
            SSdcs._SendSignal(COMSIG_GLOB_NEW_ITEM, Dm.list(SSdcs, this));
        return dot;
    }

    // tg: code/game/objects/items.dm:276
    public override object Destroy(float? force = null)
    {
        // This var exists as a weird proxy "owner" ref
        // It's used in a few places. Stop using it, and optimially replace all uses please
        master = null;
        if (ismob(loc))
        {
            mob m = loc as mob;
            Waiting.NoSleep(m.temporarilyRemoveItemFromInventory(this, TRUE));
        }

        // Handle cleaning up our actions list
        foreach (datum_action action in Dm.each_cast<datum_action>(actions))
            remove_item_action(action);

        return base.Destroy(force);
    }

    // tg: code/game/objects/items.dm:299
    // SHOULD_CALL_PARENT(FALSE): no attribute (rules 7.9 marks only TRUE)
    public virtual async DmTask<object> item_ctrl_click(mob user = null)
    {
        return NONE;
    }

    /// Called when an action associated with our item is deleted
    // tg: code/game/objects/items.dm:304
    [SignalHandler]
    public virtual void on_action_deleted(datum source = null)
    {
        if (!Dm.@in(source, actions))
            throw Dm.CRASH(Dm.text("An action ([]) was deleted that was associated with an item ([]), but was not found in the item's actions list.", source.GetType(), this), "code/game/objects/items.dm", 308);

        LAZYREMOVE(ref actions, source);
    }

    /// Adds an item action to our list of item actions.
    /// Item actions are actions linked to our item, that are granted to mobs who equip us.
    /// This also ensures that the actions are properly tracked in the actions list and removed if they're deleted.
    /// Can be be passed a typepath of an action or an instance of an action.
    // tg: code/game/objects/items.dm:316
    public virtual async DmTask<object> add_item_action(object action_or_action_type = null)
    {
        // M4c slice 3 (D146): the action buttons.
        datum_action action;
        if (ispath(action_or_action_type, typeof(datum_action)))
            action = await Dm.new_async((Type)action_or_action_type, this) as datum_action;
        else if (istype(action_or_action_type, typeof(datum_action)))
            action = action_or_action_type as datum_action;
        else
            throw Dm.CRASH("item add_item_action got a type or instance of something that wasn't an action.", "code/game/objects/items.dm", 324);

        LAZYADD(ref actions, action);
        RegisterSignal(action, COMSIG_QDELETING, nameof(on_action_deleted));
        await grant_action_to_bearer(action);
        return action;
    }

    /// Grant the action to anyone who has this item equipped to an appropriate slot
    // tg: code/game/objects/items.dm:332
    public virtual async DmTask grant_action_to_bearer(datum_action action = null)
    {
        if (!ismob(loc))
            return;
        mob holder = loc as mob;
        await give_item_action(action, holder, holder.get_slot_by_item(this));
    }

    /// Removes an instance of an action from our list of item actions.
    // tg: code/game/objects/items.dm:339
    public virtual void remove_item_action(datum_action action = null)
    {
        if (!Dm.truthy(action))
            return;

        UnregisterSignal(action, COMSIG_QDELETING);
        LAZYREMOVE(ref actions, action);
        qdel(action);
    }

    // tg: code/game/objects/items.dm:348
    public virtual object add_stealing_item_objective()
    {
        return null;
    }

    /// Adds the weapon_description element, which shows the 'warning label' for especially dangerous objects. Override this for item types with special notes.
    // tg: code/game/objects/items.dm:352
    public virtual async DmTask add_weapon_description()
    {
        await AddElement(typeof(datum_element_weapon_description));
    }

    // tg: code/game/objects/items.dm:363
    public virtual object check_allowed_items(atom target = null, float? not_inside = null, float? target_self = null)
    {
        not_inside ??= FALSE;
        target_self ??= FALSE;
        if (!Dm.truthy(target_self) && Dm.@in(this, target))
            return FALSE;
        if (Dm.truthy(not_inside) && !isturf(target.loc) && !isturf(target))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/objects/items.dm:380
    public virtual async DmTask<object> suicide_act(mob_living user = null)
    {
        Dm.NotTranslated("code/game/objects/items.dm:380", "suicide_act: M2 P2 (skeleton)");
        return default;
    }

    // tg: code/game/objects/items.dm:404
    public virtual async DmTask __gvb_move_to_top(DmList structured_args = null)
    {
        Dm.NotTranslated("code/game/objects/items.dm:404", "__gvb_move_to_top: M2 P2 (skeleton)");
    }

    // tg: code/game/objects/items.dm:404
    public virtual void move_to_top(params object[] __extra)
    {
        Dm.NotTranslated("code/game/objects/items.dm:404", "move_to_top: M2 P2 (skeleton)");
    }

    // tg: code/game/objects/items.dm:445
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);
        if (HAS_TRAIT(user, TRAIT_RESEARCH_SCANNER))
            Dm.append(ref dot, research_scan(user));
        return dot;
    }

    // tg: code/game/objects/items.dm:450
    public virtual object research_scan(mob user = null)
    {
        // Research prospects, including boostable nodes and point values. Deliver to a console to know whether the boosts have already been used.
        DmList research_msg = Dm.list("<font color='purple'>Research prospects:</font> ");
        // Separator between the items on the list
        string sep = "";
        // Nodes that can be boosted
        DmList boostable_nodes = SSresearch.techweb_unlock_items[type] as DmList;
        if (Dm.truthy(length(boostable_nodes)))
        {
            foreach (object boost_path in Dm.each(boostable_nodes))
            {
                datum_techweb_node node = SSresearch.techweb_nodes[boost_path] as datum_techweb_node;
                Dm.append(ref research_msg, sep);
                Dm.append(ref research_msg, node.display_name);
                sep = ", ";
            }
        }
        DmList points = SSresearch.techweb_point_items[type] as DmList;
        if (Dm.truthy(length(points)))
        {
            sep = ", ";
            Dm.append(ref research_msg, techweb_point_display_generic(points));
        }

        if (!Dm.truthy(sep)) // nothing was shown
            Dm.append(ref research_msg, "None");

        // Extractable materials. Only shows the names, not the amounts.
        Dm.append(ref research_msg, ".<br><font color='purple'>Extractable materials:</font> ");
        if (Dm.truthy(length(custom_materials)))
        {
            sep = "";
            foreach (object mat in Dm.each(custom_materials))
            {
                Dm.append(ref research_msg, sep);
                Dm.append(ref research_msg, CallMaterialName(mat));
                sep = ", ";
            }
        }
        else
            Dm.append(ref research_msg, "None");
        Dm.append(ref research_msg, ".");
        return research_msg.Join("");
    }

    // tg: code/game/objects/items.dm:546
    public virtual async DmTask<object> attempt_pickup(mob_living user = null, float? skip_grav = null)
    {
        skip_grav ??= FALSE;
        // DEVIATION(DV27): the interaction guard (D132): an item whose own pickup, equipped or dropped code is not translated is
        // not picked up at all. The check is made before tg's first line because put_in_hands' refusal (below) would make tg's
        // attempt_pickup drop the item at the user's feet (dropItemToGround) instead of leaving it where it lies.
        if (InertSeam.MissingOverride(this, "pickup", user) || InertSeam.MissingOverride(this, "equipped", user) || InertSeam.MissingOverride(this, "dropped", user))
            return TRUE;
        object dot = null;
        dot = TRUE;

        if (!Dm.truthy(Dm.bitand(interaction_flags_item, INTERACT_ITEM_ATTACK_HAND_PICKUP))) //See if we're supposed to auto pickup.
            return dot;

        if (!Dm.truthy(Dm.bitand(user.mobility_flags, MOBILITY_PICKUP)))
            return dot;

        if (!Dm.truthy(skip_grav))
        {
            //Heavy gravity makes picking up things very slow.
            object grav = user.has_gravity();
            if (Dm.gt(grav, STANDARD_GRAVITY))
            {
                float grav_power = min(3, Dm.num(grav) - STANDARD_GRAVITY);
                to_chat(user, span_notice(I18n.T("You start picking up []...", this)));
                if (!Dm.truthy(await do_after(user, 30 * grav_power, this)))
                    return dot;
            }
        }

        //If the item is in a storage item, take it out
        float outside_storage = Dm.not(loc.atom_storage);
        turf storage_turf = null;
        if (Dm.truthy(loc.atom_storage))
        {
            //We want the pickup animation to play even if we're moving the item between movables. Unless the mob is not located on a turf.
            if (isturf(user.loc))
                storage_turf = get_turf(loc);
            if (!Dm.truthy(await loc.atom_storage.remove_single(user, this, user, silent: TRUE)))
                return dot;
        }
        if (QDELETED(this)) //moving it out of the storage destroyed it.
            return dot;

        if (Dm.truthy(storage_turf))
            do_pickup_animation(user, storage_turf);

        if (Dm.truthy(throwing))
            throwing.finalize(FALSE);
        if (loc == user && Dm.truthy(outside_storage))
        {
            if (!Dm.truthy(can_mob_unequip(user)) || !Dm.truthy(await user.temporarilyRemoveItemFromInventory(this)))
                return dot;
        }

        dot = FALSE;
        await pickup(user);
        // tg: code/game/objects/items.dm:589
        add_fingerprint(user);
        if (!Dm.truthy(await user.put_in_active_hand(this, ignore_animation: Dm.not(outside_storage))))
        {
            await user.dropItemToGround(this);
            return TRUE;
        }
        return dot;
    }

    /// Called when a mob is manually attempting to unequip the item
    /// Returning FALSE will prevent the unequip from happening
    // tg: code/game/objects/items.dm:594
    public virtual object can_mob_unequip(mob user = null)
    {
        return TRUE;
    }

    // tg: code/game/objects/items.dm:613
    public override async DmTask<object> attack_robot(mob user = null, object modifiers = null)
    {
        if (loc is not obj_item_robot_model)
            return null;
        var borg = (mob_living_silicon_robot)user;
        if (Dm.truthy(borg.low_power_mode)) //can't equip modules with an empty cell.
            return null;
        await borg.activate_module(this);
        return null;
    }

    // tg: code/game/objects/items.dm:622
    public virtual async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        attack_text ??= "the attack";
        final_block_chance ??= 0;
        damage ??= 0;
        attack_type ??= MELEE_ATTACK;
        damage_type ??= BRUTE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ITEM_HIT_REACT]) ? NONE : this._SendSignal(COMSIG_ITEM_HIT_REACT, Dm.list(this, owner, hitby, attack_text, final_block_chance, damage, attack_type, damage_type))), COMPONENT_HIT_REACTION_BLOCK)))
            return TRUE;

        if (prob(Dm.num(final_block_chance)))
        {
            owner.visible_message(span_danger(I18n.T("[] blocks [] with []!", owner, attack_text, this)));
            object owner_turf = get_turf(owner);
            Dm.@new(block_effect, owner_turf, COLOR_YELLOW);
            playsound(this, block_sound, BLOCK_SOUND_VOLUME, vary: TRUE); // tg: code/game/objects/items.dm:630
            return TRUE;
        }
        return null;
    }

    /**
     * Called when a mob tries to talk into this item (the radio key of a hand, MODE_R_HAND and MODE_L_HAND)
     *
     * Return a flag that modifies the original message
     */
    // tg: code/game/objects/items.dm:648
    public virtual object talk_into(atom_movable speaker = null, object message = null, object channel = null, DmList spans = null, object language = null, DmList message_mods = null)
    {
        return ((!Dm.truthy(_listen_lookup?[COMSIG_ITEM_TALK_INTO]) ? NONE : _SendSignal(COMSIG_ITEM_TALK_INTO, Dm.list(this, speaker, message, channel, spans, language, message_mods))) is var or__1 && Dm.truthy(or__1) ? or__1 : Dm.bitor(ITALICS, REDUCE_RANGE));
    }

    /* sound procs, made so they can be overriden on subtypes */

    /// executed when this item is thrown and hits a mob
    // tg: code/game/objects/items.dm:654
    public virtual object mob_throw_hit_sound_chain(object target = null, object volume = null)
    {
        if (Dm.truthy(play_mob_throw_hit_sound(target, Dm.num_or_null(volume))))
            return TRUE;
        if (Dm.truthy(play_hit_sound(target, Dm.num_or_null(volume))))
            return TRUE;
        playsound(target as atom, Dm.resource("sound/items/weapons/throwtap.ogg"), volume, TRUE, -1); // tg: code/game/objects/items.dm:659
        return TRUE;
    }

    /// executed when this item is thrown and lands on a turf
    // tg: code/game/objects/items.dm:663
    public virtual object throw_drop_sound_chain(object volume = null)
    {
        if (Dm.truthy(play_throw_drop_sound(Dm.num_or_null(volume))))
            return TRUE;
        if (Dm.truthy(play_drop_sound(Dm.num_or_null(volume))))
            return TRUE;
        return FALSE;
    }

    // tg: code/game/objects/items.dm:670
    public virtual object sound_chain(object sound_to_play = null, float? volume = null, object target = null)
    {
        volume ??= HALFWAY_SOUND_VOLUME;
        target ??= this;
        if (Dm.truthy(sound_to_play))
        {
            playsound(target as atom, sound_to_play, volume, sound_vary, ignore_walls: FALSE); // tg: code/game/objects/items.dm:672
            return TRUE;
        }
        return FALSE;
    }

    /// plays the pickup sound of this item.
    // tg: code/game/objects/items.dm:677
    public virtual object play_pickup_sound(float? volume = null)
    {
        volume ??= PICKUP_SOUND_VOLUME;
        return sound_chain(pickup_sound, volume);
    }

    /// plays the drop sound
    // tg: code/game/objects/items.dm:681
    public virtual object play_drop_sound(float? volume = null)
    {
        volume ??= DROP_SOUND_VOLUME;
        return sound_chain(drop_sound, volume);
    }

    /// plays the throw drop sound
    // tg: code/game/objects/items.dm:685
    public virtual object play_throw_drop_sound(float? volume = null)
    {
        volume ??= YEET_SOUND_VOLUME;
        return sound_chain(throw_drop_sound, volume);
    }

    /// plays the mob throw hit sound
    // tg: code/game/objects/items.dm:689
    public virtual object play_mob_throw_hit_sound(object target = null, float? volume = null)
    {
        volume ??= DROP_SOUND_VOLUME;
        return sound_chain(mob_throw_hit_sound, volume, target);
    }

    /// plays when a mob is hit with this item
    // tg: code/game/objects/items.dm:693
    public virtual object play_hit_sound(object target = null, float? volume = null)
    {
        volume ??= HALFWAY_SOUND_VOLUME;
        return sound_chain(hitsound, volume, target);
    }

    // tg: code/game/objects/items.dm:696
    public virtual object play_equip_sound(float? volume = null)
    {
        volume ??= EQUIP_SOUND_VOLUME;
        return sound_chain(equip_sound, volume);
    }

    /// Called when a mob drops an item.
    // tg: code/game/objects/items.dm:702
    [ShouldCallParent]
    public virtual async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        silent ??= FALSE;

        // Remove any item actions we temporary gave out.
        foreach (datum_action action_item_has in Dm.each_cast<datum_action>(actions))
            await action_item_has.Remove(user);

        if (Dm.truthy(Dm.bitand(item_flags, DROPDEL)) && !QDELETED(this))
            qdel(this);
        UnregisterSignal(this, Dm.list(SIGNAL_ADDTRAIT(TRAIT_NO_WORN_ICON), SIGNAL_REMOVETRAIT(TRAIT_NO_WORN_ICON)));
        if (Dm.truthy(this._listen_lookup?[COMSIG_ITEM_DROPPED]))
            this._SendSignal(COMSIG_ITEM_DROPPED, Dm.list(this, user));
        if (Dm.truthy(user._listen_lookup?[COMSIG_MOB_DROPPED_ITEM]))
            user._SendSignal(COMSIG_MOB_DROPPED_ITEM, Dm.list(user, this));
        if (!Dm.truthy(silent) && Dm.truthy(drop_sound))
            play_drop_sound(DROP_SOUND_VOLUME);
        return null;
    }

    /// called just as an item is picked up (loc is not yet changed)
    // tg: code/game/objects/items.dm:718
    [ShouldCallParent]
    public virtual async DmTask<object> pickup(mob user = null)
    {
        // DEVIATION(DV27): the interaction guard (D132) on the item's own pickup (interaction.md 7, P2's site).
        if (InertSeam.MissingOverride(this, "pickup", user))
            return FALSE;
        if (Dm.truthy(this._listen_lookup?[COMSIG_ITEM_PICKUP]))
            this._SendSignal(COMSIG_ITEM_PICKUP, Dm.list(this, user));
        if (Dm.truthy(user._listen_lookup?[COMSIG_LIVING_PICKED_UP_ITEM]))
            user._SendSignal(COMSIG_LIVING_PICKED_UP_ITEM, Dm.list(user, this));
        return null;
    }

    /// called when "found" in pockets and storage items. Returns 1 if the search should end.
    // tg: code/game/objects/items.dm:724
    public virtual async DmTask<object> on_found(mob finder = null)
    {
        return null;
    }

    // tg: code/game/objects/items.dm:736
    [ShouldNotOverride]
    public virtual async DmTask<object> on_equipped(mob user = null, object slot = null, float? initial = null)
    {
        initial ??= FALSE;
        await equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ITEM_POST_EQUIPPED]) ? NONE : this._SendSignal(COMSIG_ITEM_POST_EQUIPPED, Dm.list(this, user, slot))), COMPONENT_EQUIPPED_FAILED)))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/objects/items.dm:750
    public virtual async DmTask<object> visual_equipped(mob user = null, object slot = null, float? initial = null)
    {
        initial ??= FALSE;
        return TRUE;
    }

    // tg: code/game/objects/items.dm:763
    [ShouldCallParent, DmProtected]
    public virtual async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        initial ??= FALSE;
        await visual_equipped(user, slot, initial);
        if (Dm.truthy(this._listen_lookup?[COMSIG_ITEM_EQUIPPED]))
            this._SendSignal(COMSIG_ITEM_EQUIPPED, Dm.list(this, user, slot));
        if (Dm.truthy(user._listen_lookup?[COMSIG_MOB_EQUIPPED_ITEM]))
            user._SendSignal(COMSIG_MOB_EQUIPPED_ITEM, Dm.list(user, this, slot));

        // Give out actions our item has to people who equip it.
        foreach (datum_action action in Dm.each_cast<datum_action>(actions))
            await give_item_action(action, user, slot);

        RegisterSignals(this, Dm.list(SIGNAL_ADDTRAIT(TRAIT_NO_WORN_ICON), SIGNAL_REMOVETRAIT(TRAIT_NO_WORN_ICON)), nameof(update_slot_icon), @override: TRUE);

        if (!Dm.truthy(initial) && Dm.truthy(Dm.bitand(slot_flags, slot)) && Dm.truthy(play_equip_sound()))
            return;

        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_HANDS)))
            play_pickup_sound();
    }

    /// Gives one of our item actions to a mob, when equipped to a certain slot
    // tg: code/game/objects/items.dm:783
    public virtual async DmTask give_item_action(datum_action action = null, mob to_who = null, object slot = null)
    {
        // Some items only give their actions buttons when in a specific slot.
        if (!Dm.truthy(item_action_slot_check(slot, to_who, action)) || Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ITEM_UI_ACTION_SLOT_CHECKED]) ? NONE : this._SendSignal(COMSIG_ITEM_UI_ACTION_SLOT_CHECKED, Dm.list(this, to_who, action, slot))), COMPONENT_ITEM_ACTION_SLOT_INVALID)))
        {
            // There is a chance we still have our item action currently,
            // and are moving it from a "valid slot" to an "invalid slot".
            // So call Remove() here regardless, even if excessive.
            await action.Remove(to_who);
            return;
        }

        await action.Grant(to_who);
    }

    /// Sometimes we only want to grant the item's action if it's equipped in a specific slot.
    // tg: code/game/objects/items.dm:795
    public virtual object item_action_slot_check(object slot = null, mob user = null, datum_action action = null)
    {
        if (!Dm.truthy(slot)) // Equipped into storage
            return FALSE;
        if (Dm.truthy(Dm.bitand(slot, Dm.bitor(ITEM_SLOT_HANDCUFFED, ITEM_SLOT_LEGCUFFED)))) // These aren't true slots, so avoid granting actions there
            return FALSE;
        if (action_slots != null)
            return Dm.bitand(slot, action_slots);
        else if (Dm.truthy(slot_flags))
            return Dm.bitand(slot, slot_flags);
        return TRUE;
    }

    // tg: code/game/objects/items.dm:817
    public virtual async DmTask<object> mob_can_equip(mob_living M = null, object slot = null, float? disable_warning = null, float? bypass_equip_delay_self = null, float? ignore_equipped = null, float? indirect_action = null)
    {
        disable_warning ??= FALSE;
        bypass_equip_delay_self ??= FALSE;
        ignore_equipped ??= FALSE;
        indirect_action ??= FALSE;
        if (M == null)
            return FALSE;

        return await M.can_equip(this, slot, disable_warning, bypass_equip_delay_self, ignore_equipped, indirect_action: indirect_action);
    }

    // tg: code/game/objects/items.dm:823
    public virtual async DmTask __gvb_verb_pickup(DmList structured_args = null)
    {
        Dm.NotTranslated("code/game/objects/items.dm:823", "__gvb_verb_pickup: M2 P2 (skeleton)");
    }

    // tg: code/game/objects/items.dm:823
    public virtual void verb_pickup(params object[] __extra)
    {
        Dm.NotTranslated("code/game/objects/items.dm:823", "verb_pickup: M2 P2 (skeleton)");
    }

    /**
     *This proc is executed when someone clicks the on-screen UI button.
     *The default action is attack_self().
     *Checks before we get to here are: mob is alive, mob is not restrained, stunned, asleep, resting, laying, item is on the mob.
     */
    // tg: code/game/objects/items.dm:841
    public virtual async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        // DEVIATION(DV27): the interaction guard (D132) on the item's own ui_action_click (M4c slice 3: an item whose tg
        // override of the button's click is not translated refuses it instead of running attack_self).
        if (InertSeam.MissingOverride(this, "ui_action_click", user))
            return null;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ITEM_UI_ACTION_CLICK]) ? NONE : this._SendSignal(COMSIG_ITEM_UI_ACTION_CLICK, Dm.list(this, user, actiontype))), COMPONENT_ACTION_HANDLED)))
            return null;

        await attack_self(user);
        return null;
    }

    // tg: code/game/objects/items.dm:848
    public virtual async DmTask<object> IsReflect(object def_zone = null)
    {
        return FALSE;
    }

    // tg: code/game/objects/items.dm:897
    public virtual async DmTask<object> after_throw(datum_callback callback = null)
    {
        object dot = null;
        if (Dm.truthy(callback)) //call the original callback
            dot = await callback.Invoke();
        Dm.mask(ref item_flags, Dm.bitnot(IN_INVENTORY));
        if (!Dm.truthy(pixel_y) && !Dm.truthy(pixel_x) && !Dm.truthy(Dm.bitand(item_flags, NO_PIXEL_RANDOM_DROP)))
        {
            pixel_x = rand(-8, 8);
            pixel_y = rand(-8, 8);
        }
        return dot;
    }

    /// Takes the location to move the item to, and optionally the mob doing the removing
    /// If no mob is provided, we'll pass in the location, assuming it is a mob
    /// Please use this if you're going to snowflake an item out of a obj/item/storage
    // tg: code/game/objects/items.dm:908
    public virtual async DmTask<object> remove_item_from_storage(atom newLoc = null, mob removing = null)
    {
        if (!Dm.truthy(newLoc))
            return FALSE;
        if (!Dm.truthy(removing))
        {
            if (ismob(newLoc))
                removing = newLoc as mob;
            else
            {
                stack_trace(Dm.text("Tried to remove an item and place it into [] without implicitly or explicitly passing in a mob doing the removing", newLoc), "code/game/objects/items.dm", 915);
                return null;
            }
        }
        if (Dm.truthy(loc.atom_storage))
            return await loc.atom_storage.remove_single(removing, this, newLoc, silent: TRUE);
        return FALSE;
    }

    /// Returns the icon used for overlaying the object on a belt
    // tg: code/game/objects/items.dm:922
    public virtual object get_belt_overlay()
    {
        object icon_state_to_use = Dm.truthy(inside_belt_icon_state) ? inside_belt_icon_state : icon_state;
        if (Dm.truthy(greyscale_config_belt) && Dm.truthy(greyscale_colors))
            return mutable_appearance(SSgreyscale.GetColoredIconByType(greyscale_config_belt, greyscale_colors), icon_state_to_use as string);
        return mutable_appearance(Dm.resource("icons/obj/clothing/belt_overlays.dmi"), icon_state_to_use as string);
    }

    // tg: code/game/objects/items.dm:932
    public virtual object get_surgery_tool_overlay(object tray_extended = null)
    {
        return null;
    }

    // tg: code/game/objects/items.dm:935
    [SignalHandler]
    public virtual void update_slot_icon()
    {
        if (!ismob(loc) || QDELETED(loc))
            return;
        mob owner = loc as mob;
        Waiting.NoSleep(owner.update_clothing(Dm.bitor(slot_flags, owner.get_slot_by_item(this))));
    }

    ///Returns the temperature of src. If you want to know if an item is hot use this proc.
    // tg: code/game/objects/items.dm:943
    public virtual object get_temperature()
    {
        if (Dm.truthy(Dm.bitand(resistance_flags, ON_FIRE)))
            return max(heat, BURNING_ITEM_MINIMUM_TEMPERATURE);
        return heat;
    }

    ///Returns the sharpness of src. If you want to get the sharpness of an item use this.
    // tg: code/game/objects/items.dm:949
    public virtual object get_sharpness()
    {
        return sharpness;
    }

    // tg: code/game/objects/items.dm:952
    public virtual object get_dismember_sound()
    {
        object dot = null;
        if (Dm.eq(damtype, BURN))
            dot = SFX_SEAR;
        else
            dot = SFX_DESECRATION;
        return dot;
    }

    /// Creates an ignition hotspot if item is lit and located on turf, in mask, or in hand
    // tg: code/game/objects/items.dm:959  (M7 lighting slice L1: translated for the flares and candles)
    public virtual void open_flame(float? flame_heat = null, float? mob_slots = null)
    {
        flame_heat ??= 700;
        mob_slots ??= Dm.bitor(ITEM_SLOT_MASK, ITEM_SLOT_HANDS);
        atom location = loc;
        if (ismob(location))
        {
            mob pyromanic = location as mob;
            if (Dm.truthy(Dm.bitand(pyromanic.get_slot_by_item(this), mob_slots)))
                location = get_turf(pyromanic);
        }
        if (isturf(location))
            (location as turf).hotspot_expose(flame_heat, 5);
    }

    /// If an object can successfully be used as a fire starter it will return a message
    // tg: code/game/objects/items.dm:969  (M7 lighting slice L1)
    public virtual async DmTask<object> ignition_effect(atom A = null, mob user = null)
    {
        if (Dm.num(get_temperature()) >= FIRE_MINIMUM_TEMPERATURE_TO_EXIST)
            return span_notice(I18n.T("[] lights [] with [].", user, A, this));
        return "";
    }

    // tg: code/game/objects/items.dm:986  (M5 slice 1: an item burnt down by the burning component leaves ash)
    public override async DmTask burn__proc()
    {
        if (!QDELETED(this))
        {
            turf T = get_turf(this);
            Type ash_type = typeof(obj_effect_decal_cleanable_ash);
            if (w_class == WEIGHT_CLASS_HUGE || w_class == WEIGHT_CLASS_GIGANTIC)
                ash_type = typeof(obj_effect_decal_cleanable_ash_large);
            obj_effect_decal_cleanable_ash A = Dm.@new(ash_type, T) as obj_effect_decal_cleanable_ash;
            A.desc = Dm.str(Dm.add(A.desc, Dm.text("\nLooks like this used to be \\an [] some time ago.", name)));
            await base.burn__proc();
        }
    }

    // tg: code/game/objects/items.dm:996  (M5 slice 1)
    public override async DmTask acid_melt()
    {
        if (!QDELETED(this))
        {
            turf T = get_turf(this);
            obj_effect_decal_cleanable_molten_object MO = Dm.@new<obj_effect_decal_cleanable_molten_object>(T);
            MO.pixel_x = rand(-16, 16);
            MO.pixel_y = rand(-16, 16);
            MO.desc = Dm.text("Looks like this was \\an [] some time ago.", this);
            await base.acid_melt();
        }
    }

    // tg: code/game/objects/items.dm:1005 (gaps H2)
    [ShouldCallParent]
    public virtual object microwave_act(obj_machinery_microwave microwave_source = null, mob microwaver = null, object randomize_pixel_offset = null)
    {
        return !Dm.truthy(_listen_lookup?[COMSIG_ITEM_MICROWAVE_ACT]) ? NONE : _SendSignal(COMSIG_ITEM_MICROWAVE_ACT, Dm.list(this, microwave_source, microwaver, randomize_pixel_offset));
    }

    ///Used to check for extra requirements for blending(grinding or juicing) an object
    // tg: code/game/objects/items.dm:1011  (E15: the item grinding procs, :1011-1131, for the plumbing chemical grinder)
    public virtual object blend_requirements(atom_movable grinder = null, mob_living user = null)
    {
        return TRUE;
    }

    ///Returns a reagent list containing the reagents this item produces when ground up in a grinder
    // tg: code/game/objects/items.dm:1015
    public virtual async DmTask<object> grind_results()
    {
        if (!Dm.truthy(length(custom_materials)) || Dm.truthy(Dm.bitand(material_flags, MATERIAL_NO_REAGENTS)))
            return null;

        DmList dot = Dm.list();
        foreach (object mat_id in Dm.each(custom_materials))
        {
            object amount = custom_materials[mat_id];
            datum_material material = await SSmaterials.get_material(mat_id) as datum_material;
            if (!Dm.truthy(material.material_reagent))
                continue;
            if (!islist(material.material_reagent))
            {
                dot[material.material_reagent] = Dm.add(dot[material.material_reagent], Dm.num(amount) * MATERIAL_REAGENTS_PER_SHEET / SHEET_MATERIAL_AMOUNT);
                continue;
            }
            DmList material_reagents = (DmList)material.material_reagent;
            foreach (object reagent_type in Dm.each(material_reagents))
                dot[reagent_type] = Dm.add(dot[reagent_type], Dm.num(amount) * Dm.num(material_reagents[reagent_type]) / length(material_reagents) * MATERIAL_REAGENTS_PER_SHEET / SHEET_MATERIAL_AMOUNT);
        }
        return dot;
    }

    ///Called BEFORE the object is ground up - use this to change grind results based on conditions. Return "-1" to prevent the grinding from occurring
    // tg: code/game/objects/items.dm:1033
    public virtual async DmTask<object> on_grind()
    {
        return !Dm.truthy(_listen_lookup?[COMSIG_ITEM_ON_GRIND]) ? (object)NONE : _SendSignal(COMSIG_ITEM_ON_GRIND, Dm.list(this));
    }

    ///Grind item, adding grind_results to item's reagents and transfering to target_holder if specified
    // tg: code/game/objects/items.dm:1039
    public virtual async DmTask<object> grind(datum_reagents target_holder = null, mob user = null, atom_movable grinder = null)
    {
        grinder ??= loc as atom_movable;
        object dot = FALSE;
        if (Dm.eq(await on_grind(), -1) || Dm.truthy(target_holder.holder_full()))
            return dot;

        dot = await grind_atom(target_holder, user);

        //reccursive grinding to get all them juices
        object result;
        foreach (obj_item ingredient in Dm.each_cast<obj_item>(get_all_contents_type(typeof(obj_item))))
        {
            if (ingredient == this)
                continue;

            result = await ingredient.grind(target_holder, user);
            if (!Dm.truthy(dot))
                dot = result;
        }

        if (Dm.truthy(dot) && grinder != null)
            return await grinder.blended(this, grinded: TRUE);
        return dot;
    }

    ///Subtypes override his proc for custom grinding
    // tg: code/game/objects/items.dm:1062
    public virtual async DmTask<object> grind_atom(datum_reagents target_holder = null, mob user = null)
    {
        DmList grind_reagents = await grind_results() as DmList;

        object dot = FALSE;
        if (Dm.truthy(length(grind_reagents)))
        {
            await target_holder.add_reagent_list(grind_reagents);
            dot = TRUE;
        }
        if (Dm.truthy(reagents) && Dm.truthy(await ((datum_reagents)reagents).trans_to(target_holder, ((datum_reagents)reagents).total_volume, transferred_by: user)))
            dot = TRUE;
        return dot;
    }

    ///Returns A reagent the nutriments are converted into when the item is juiced.
    // tg: code/game/objects/items.dm:1075
    public virtual object juice_typepath()
    {
        return null;
    }

    ///Called BEFORE the object is ground up - use this to change grind results based on conditions. Return "-1" to prevent the grinding from occurring
    // tg: code/game/objects/items.dm:1081
    public virtual object on_juice()
    {
        if (!Dm.truthy(juice_typepath()))
            return -1;

        return !Dm.truthy(_listen_lookup?[COMSIG_ITEM_ON_JUICE]) ? (object)NONE : _SendSignal(COMSIG_ITEM_ON_JUICE, Dm.list(this));
    }

    ///Juice item, converting nutriments into juice_typepath and transfering to target_holder if specified
    // tg: code/game/objects/items.dm:1090
    public virtual async DmTask<object> juice(datum_reagents target_holder = null, mob user = null, atom_movable juicer = null)
    {
        juicer ??= loc as atom_movable;
        object dot = FALSE;
        if (Dm.eq(on_juice(), -1) || !Dm.truthy(((datum_reagents)reagents)?.total_volume))
            return dot;

        dot = await juice_atom(target_holder, user);

        //reccursive juicing to get all them juices
        object result;
        foreach (obj_item ingredient in Dm.each_cast<obj_item>(get_all_contents_type(typeof(obj_item))))
        {
            if (ingredient == this)
                continue;

            result = await ingredient.juice(target_holder, user);
            if (!Dm.truthy(dot))
                dot = result;
        }

        if (Dm.truthy(dot) && juicer != null)
            return await juicer.blended(this, grinded: FALSE);
        return dot;
    }

    ///Subtypes override his proc for custom juicing
    // tg: code/game/objects/items.dm:1113
    public virtual async DmTask<object> juice_atom(datum_reagents target_holder = null, mob user = null)
    {
        object dot = FALSE;

        object juice_result = juice_typepath();

        if (ispath(juice_result))
        {
            await ((datum_reagents)reagents).convert_reagent(typeof(datum_reagent_consumable_nutriment), juice_result, include_source_subtypes: FALSE);
            await ((datum_reagents)reagents).convert_reagent(typeof(datum_reagent_consumable_nutriment_vitamin), juice_result, include_source_subtypes: FALSE);
            dot = TRUE;
        }

        if (!QDELETED(target_holder))
            await ((datum_reagents)reagents).trans_to(target_holder, ((datum_reagents)reagents).total_volume, transferred_by: user);
        return dot;
    }

    // tg: code/game/objects/items.dm:1134
    public virtual void set_force_string()
    {
        float sw = Dm.num(force);
        if (sw >= 0 && sw <= 4)
            force_string = "very low";
        else if (sw >= 4 && sw <= 7)
            force_string = "low";
        else if (sw >= 7 && sw <= 10)
            force_string = "medium";
        else if (sw >= 10 && sw <= 11)
            force_string = "high";
        else if (sw >= 11 && sw <= 20) //12 is the force of a toolbox
            force_string = "robust";
        else if (sw >= 20 && sw <= 25)
            force_string = "very robust";
        else
            force_string = "exceptionally robust";
        last_force_string_check = Dm.num(force);
    }

    // tg: code/game/objects/items.dm:1152
    public virtual void openTip(object location = null, object control = null, object @params = null, object user = null)
    {
        if (!Dm.eq(last_force_string_check, force) && !Dm.truthy(Dm.bitand(item_flags, FORCE_STRING_OVERRIDE)))
            set_force_string();
        if (!Dm.truthy(Dm.bitand(item_flags, FORCE_STRING_OVERRIDE)))
            openToolTip(user as mob, this, @params, title: name, content: Dm.text("[]<br>[]", desc, Dm.truthy(force) ? Dm.text("<b>Force:</b> []", force_string) : ""), theme: "");
        else
            openToolTip(user as mob, this, @params, title: name, content: Dm.text("[]<br><b>Force:</b> []", desc, force_string), theme: "");
    }

    // tg: code/game/objects/items.dm:1185
    public virtual object apply_outline(object outline_color = null)
    {
        Dm.NotTranslated("code/game/objects/items.dm:1185", "apply_outline: M2 P2 (skeleton)");
        return default;
    }

    /// Called when a mob tries to use the item as a tool. Handles most checks.
    // tg: code/game/objects/items.dm:1217
    // DEVIATION(DV348): timed_action_flags reach use_tool's do_after, which tg calls without any
    public virtual async DmTask<object> use_tool(atom target = null, mob_living user = null, object delay = null, float? amount = null, float? volume = null, datum_callback extra_checks = null, float? timed_action_flags = null)
    {
        amount ??= 0;
        volume ??= 0;
        // No delay means there is no start message, and no reason to call tool_start_check before use_tool.
        // Run the start check here so we wouldn't have to call it manually.
        if (!Dm.truthy(delay) && !Dm.truthy(tool_start_check(user, amount)))
            return null;

        object skill_modifier = 1;

        if (Dm.eq(tool_behaviour, TOOL_MINING))
        {
            if (Dm.truthy(user.mind))
            {
                skill_modifier = user.mind.get_skill_modifier(typeof(datum_skill_mining), SKILL_SPEED_MODIFIER);

                if (Dm.ge(user.mind.get_skill_level(typeof(datum_skill_mining)), SKILL_LEVEL_JOURNEYMAN) && prob(Dm.num(user.mind.get_skill_modifier(typeof(datum_skill_mining), SKILL_PROBS_MODIFIER)))) // we check if the skill level is greater than Journeyman and then we check for the probality for that specific level.
                    mineral_scan_pulse(get_turf(user), SKILL_LEVEL_JOURNEYMAN - 2, scanner: this); //SKILL_LEVEL_JOURNEYMAN = 3 So to get range of 1+ we have to subtract 2 from it,.
            }
        }

        delay = Dm.mul(delay, Dm.mul(toolspeed, skill_modifier));


        // Play tool sound at the beginning of tool usage.
        play_tool_sound(target, volume);

        if (Dm.truthy(delay))
        {
            // Create a callback with checks that would be called every tick by do_after.
            datum_callback tool_check = Dm.@new<datum_callback>(this, nameof(tool_check_callback), user, amount, extra_checks);

            if (Dm.num(delay) >= MIN_TOOL_OPERATING_DELAY)
                play_tool_operating_sound(target, volume);

            if (!Dm.truthy(await do_after(user, delay, target: target, timed_action_flags: timed_action_flags, extra_checks: tool_check)))
                return null;
        }
        else
        {
            // Invoke the extra checks once, just in case.
            if (Dm.truthy(extra_checks) && !Dm.truthy(await extra_checks.Invoke()))
                return null;
        }

        // Use tool's fuel, stack sheets or charges if amount is set.
        if (Dm.truthy(amount) && !Dm.truthy(await use(amount)))
            return null;

        // Play tool sound at the end of tool usage,
        // but only if the delay between the beginning and the end is not too small
        if (Dm.num(delay) >= MIN_TOOL_SOUND_DELAY)
            play_tool_sound(target, volume);

        return TRUE;
    }

    /// Called before [obj/item/proc/use_tool] if there is a delay, or by [obj/item/proc/use_tool] if there isn't. Only ever used by welding tools and stacks, so it's not added on any other [obj/item/proc/use_tool] checks.
    // tg: code/game/objects/items.dm:1264
    public virtual object tool_start_check(mob_living user = null, float? amount = null, float? heat_required = null)
    {
        amount ??= 0;
        heat_required ??= 0;
        object dot = null;
        dot = tool_use_check(user, amount, heat_required);
        if (Dm.truthy(dot))
        {
            if (Dm.truthy(this._listen_lookup?[COMSIG_TOOL_START_USE]))
                this._SendSignal(COMSIG_TOOL_START_USE, Dm.list(this, user));
        }
        return dot;
    }

    /// A check called by [/obj/item/proc/tool_start_check] once, and by use_tool on every tick of delay.
    // tg: code/game/objects/items.dm:1270
    public virtual object tool_use_check(mob_living user = null, object amount = null, object heat_required = null)
    {
        return Dm.not(amount);
    }

    /// Generic use proc. Depending on the item, it uses up fuel, charges, sheets, etc. Returns TRUE on success, FALSE on failure.
    // tg: code/game/objects/items.dm:1274
    public virtual async DmTask<object> use(object used = null, params object[] __extra)
    {
        return Dm.not(used);
    }

    /// Plays item's usesound, if any.
    // tg: code/game/objects/items.dm:1278
    public virtual void play_tool_sound(atom target = null, float? volume = null)
    {
        volume ??= 50;
        if (Dm.truthy(target) && Dm.truthy(usesound) && Dm.truthy(volume))
        {
            object played_sound = usesound;

            if (islist(usesound))
                played_sound = pick(usesound as DmList);

            playsound(target, played_sound, volume, TRUE); // tg: code/game/objects/items.dm:1285
        }
    }

    ///Play item's operating sound
    // tg: code/game/objects/items.dm:1288
    public virtual void play_tool_operating_sound(atom target = null, float? volume = null)
    {
        volume ??= 50;
        if (Dm.truthy(target) && Dm.truthy(operating_sound) && Dm.truthy(volume))
        {
            object played_sound = operating_sound;

            if (islist(operating_sound))
                played_sound = pick(operating_sound as DmList);

            if (Dm.truthy(TIMER_COOLDOWN_RUNNING(this, COOLDOWN_TOOL_SOUND))) // !TIMER_COOLDOWN_FINISHED (__DEFINES/cooldowns.dm)
                return;
            playsound(target, played_sound, volume, TRUE); // tg: code/game/objects/items.dm:1297
            // TIMER_COOLDOWN_START(src, COOLDOWN_TOOL_SOUND, 4 SECONDS) (__DEFINES/cooldowns.dm:77, it expands addtimer: inline,
            // rules 11.1 (f)) //based on our longest sound clip
            LAZYSET(ref cooldowns, COOLDOWN_TOOL_SOUND, _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_cooldown), this, COOLDOWN_TOOL_SOUND), 4 * SECONDS, file: "code/game/objects/items.dm", line: 1298));
        }
    }

    /// Used in a callback that is passed by use_tool into do_after call. Do not override, do not call manually.
    // tg: code/game/objects/items.dm:1301
    [ShouldNotOverride]
    public virtual async DmTask<object> tool_check_callback(mob_living user = null, object amount = null, datum_callback extra_checks = null)
    {
        object dot = null;
        dot = Dm.num(Dm.truthy(tool_use_check(user, amount)) && (!Dm.truthy(extra_checks) || Dm.truthy(await extra_checks.Invoke())));
        if (Dm.truthy(dot))
        {
            if (Dm.truthy(this._listen_lookup?[COMSIG_TOOL_IN_USE]))
                this._SendSignal(COMSIG_TOOL_IN_USE, Dm.list(this, user));
        }
        return dot;
    }

    // tg: code/game/objects/items.dm:1319 (M8 slice C: a held pill force-moved into a stomach leaves its hand)
    public override async DmTask<object> doMove(atom destination = null)
    {
        if (!Dm.truthy(Dm.bitand(item_flags, IN_INVENTORY)))
            return await base.doMove(destination);

        if (!ismob(loc))
        {
            stack_trace(Dm.text("[] had the IN_INVENTORY flag but the location was not a mob!", this), "code/game/objects/items.dm", 1324);
            Dm.mask(ref item_flags, Dm.bitnot(IN_INVENTORY));
            return await base.doMove(destination);
        }

        mob owner = (mob)loc;
        // This should remove the IN_INVENTORY flag. Otherwise we'll end up having a loop
        await owner.transferItemToLoc(this, destination, force: TRUE, silent: TRUE, animated: FALSE);
        return null;
    }

    // tg: code/game/objects/items.dm:1332  (M8z slice B)
    [ShouldBePure]
    public virtual object canStrip(mob stripper = null, mob owner = null)
    {
        return !HAS_TRAIT(this, TRAIT_NODROP) && !Dm.truthy(Dm.bitand(item_flags, ABSTRACT)) ? TRUE : FALSE;
    }

    // tg: code/game/objects/items.dm:1336  (M8z slice B)
    public virtual async DmTask<object> doStrip(mob stripper = null, mob owner = null)
    {
        return await owner.dropItemToGround(this);
    }

    // tg: code/game/objects/items.dm:1340
    public virtual async DmTask<object> on_thrown(mob_living_carbon user = null, atom target = null)
    {
        if (Dm.truthy(Dm.bitand(item_flags, ABSTRACT)) || HAS_TRAIT(this, TRAIT_NODROP))
            return null;
        await user.dropItemToGround(this, silent: TRUE);
        // DM parse: (throwforce && HAS_TRAIT(user, TRAIT_PACIFISM)) || HAS_TRAIT(user, TRAIT_NO_THROWING)
        if ((Dm.truthy(throwforce) && HAS_TRAIT(user, TRAIT_PACIFISM)) || HAS_TRAIT(user, TRAIT_NO_THROWING))
        {
            to_chat(user, span_notice(I18n.T("You set [] down gently on the ground.", this)));
            return null;
        }
        return this;
    }

    // tg: code/game/objects/items.dm:1369
    public virtual async DmTask<object> on_accidental_consumption(mob_living_carbon victim = null, mob_living_carbon user = null, obj_item source_item = null, float? discover_after = null)
    {
        Dm.NotTranslated("code/game/objects/items.dm:1369", "on_accidental_consumption: M2 P2 (skeleton)");
        return default;
    }

    /**
     * Updates all action buttons associated with this item
     *
     * Arguments:
     * * update_flags - Which flags of the action should we update
     * * force - Force buttons update even if the given button icon state has not changed
     */
    // tg: code/game/objects/items.dm:1453
    public virtual async DmTask update_item_action_buttons(float? update_flags = null, float? force = null)
    {
        update_flags ??= ALL;
        force ??= FALSE;
        foreach (datum_action current_action in Dm.each_cast<datum_action>(actions))
            await current_action.build_all_button_icons(update_flags, force);
    }

    // Update icons if this is being carried by a mob
    // tg: code/game/objects/items.dm:1458  (M4b: the fire extinguisher's water washes what it reaches)
    public override async DmTask<object> wash(object clean_types = null, params object[] __extra)
    {
        object dot = await base.wash(clean_types, __extra);
        if (!Dm.truthy(dot)) // we don't need mob updates when the item was already clean
            return dot;
        if (ismob(loc))
        {
            mob mob_loc = (mob)loc;
            await mob_loc.update_clothing(slot_flags);
        }
        return dot;
    }

    // tg: code/game/objects/items.dm:1467
    public virtual async DmTask handle_openspace_click(turf target = null, mob user = null, DmList modifiers = null)
    {
        stack_trace("Undefined handle_openspace_click() behaviour. Ascertain the openspace_item_click_handler element has been attached to the right item and that its proc override doesn't call parent.", "code/game/objects/items.dm", 1468);
        await DmTask.CompletedTask;
    }

    // tg: code/game/objects/items.dm:1479
    public virtual async DmTask<object> on_offered(mob_living offerer = null, mob_living offered = null)
    {
        if (offered == null) // item has just been offered to anyone around
        {
            if (!HAS_TRAIT(offerer, TRAIT_CAN_HOLD_ITEMS))
                return TRUE;
        }
        else if (!(HAS_TRAIT(offerer, TRAIT_CAN_HOLD_ITEMS) && HAS_TRAIT(offered, TRAIT_CAN_HOLD_ITEMS)))
            return TRUE; // both must be able to hold items for this to make sense
        if (Dm.truthy(Dm.bitand(!Dm.truthy(this._listen_lookup?[COMSIG_ITEM_OFFERING]) ? (object)NONE : this._SendSignal(COMSIG_ITEM_OFFERING, Dm.list(this, offerer)), COMPONENT_OFFER_INTERRUPT)))
            return TRUE;
        return null;
    }

    // tg: code/game/objects/items.dm:1497
    public virtual async DmTask<object> on_offer_taken(mob_living offerer = null, mob_living taker = null)
    {
        if (!HAS_TRAIT(offerer, TRAIT_CAN_HOLD_ITEMS) && !HAS_TRAIT(this, TRAIT_BORG_GIVE) && HAS_TRAIT(taker, TRAIT_CAN_HOLD_ITEMS))
            return TRUE; // both must be able to hold items for this to make sense
        if (Dm.truthy(Dm.bitand(!Dm.truthy(this._listen_lookup?[COMSIG_ITEM_OFFER_TAKEN]) ? (object)NONE : this._SendSignal(COMSIG_ITEM_OFFER_TAKEN, Dm.list(this, offerer, taker)), COMPONENT_OFFER_INTERRUPT)))
            return TRUE;
        return null;
    }

    // tg: code/game/objects/items.dm:1504
    [ShouldCallParent]
    public virtual async DmTask on_outfit_equip(mob_living_carbon_human outfit_wearer = null, object visuals_only = null, object item_slot = null)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_ITEM_EQUIPPED_AS_OUTFIT]))
            this._SendSignal(COMSIG_ITEM_EQUIPPED_AS_OUTFIT, Dm.list(this, outfit_wearer, visuals_only, item_slot));
    }

    // tg: code/game/objects/items.dm:1508
    public virtual void do_pickup_animation(atom target = null, atom source = null)
    {
        if (!Dm.truthy(source))
        {
            if (!(loc is turf))
                return;
            source = loc as turf;
        }
        if (Dm.truthy(this._listen_lookup?[COMSIG_ITEM_BEFORE_PICKUP_ANIMATION]))
            this._SendSignal(COMSIG_ITEM_BEFORE_PICKUP_ANIMATION, Dm.list(this));
        image pickup_animation = image(icon: this);
        SET_PLANE(pickup_animation, GAME_PLANE, source);
        // (BYOND: a read of transform is a copy, so this Scale changes nothing; kept as tg wrote it.)
        ((matrix)pickup_animation.transform).Scale(0.75f);
        pickup_animation.appearance_flags = APPEARANCE_UI_IGNORE_ALPHA;

        float direction = get_dir(source, target);
        float to_x = target.base_pixel_x + target.base_pixel_w;
        float to_y = target.base_pixel_y + target.base_pixel_z;

        if (Dm.truthy(Dm.bitand(direction, NORTH)))
            to_y += 32;
        else if (Dm.truthy(Dm.bitand(direction, SOUTH)))
            to_y -= 32;
        if (Dm.truthy(Dm.bitand(direction, EAST)))
            to_x += 32;
        else if (Dm.truthy(Dm.bitand(direction, WEST)))
            to_x -= 32;
        if (!Dm.truthy(direction))
        {
            to_y += 10;
            pickup_animation.pixel_x += 6 * (prob(50) ? 1 : -1); //6 to the right or left, helps break up the straight upward move
        }

        atom_movable_flick_visual pickup = source.flick_overlay_view(pickup_animation, 0.4f * SECONDS) as atom_movable_flick_visual;
        matrix animation_matrix = Dm.@new<matrix>(pickup.transform);
        animation_matrix.Turn(Dm.num(pick(-30, 30)));
        animation_matrix.Scale(0.65f);

        animate(pickup, Dm.named("alpha", 175), Dm.named("pixel_x", to_x), Dm.named("pixel_y", to_y), Dm.named("time", 0.3f * SECONDS), Dm.named("transform", animation_matrix), Dm.named("easing", CUBIC_EASING));
        animate(Dm.named("alpha", 0), Dm.named("transform", Dm.matrix().Scale(0.7f)), Dm.named("time", 0.1f * SECONDS));
    }

    // tg: code/game/objects/items.dm:1543
    public virtual void do_drop_animation(atom moving_from = null)
    {
        if (!(loc is turf))
            return;

        if (!(moving_from is atom))
            return;

        if (Dm.truthy(this._listen_lookup?[COMSIG_ITEM_BEFORE_DROP_ANIMATION]))
            this._SendSignal(COMSIG_ITEM_BEFORE_DROP_ANIMATION, Dm.list(this));
        turf current_turf = get_turf(this);
        float direction = get_dir(moving_from, current_turf);
        float from_x = moving_from.base_pixel_x;
        float from_y = moving_from.base_pixel_y;

        if (Dm.truthy(Dm.bitand(direction, NORTH)))
            from_y -= 32;
        else if (Dm.truthy(Dm.bitand(direction, SOUTH)))
            from_y += 32;
        if (Dm.truthy(Dm.bitand(direction, EAST)))
            from_x -= 32;
        else if (Dm.truthy(Dm.bitand(direction, WEST)))
            from_x += 32;
        if (!Dm.truthy(direction))
        {
            from_y += 10;
            from_x += 6 * (prob(50) ? 1 : -1); //6 to the right or left, helps break up the straight upward move
        }

        //We're moving from these chords to our current ones
        float old_x = pixel_x;
        float old_y = pixel_y;
        float old_alpha = alpha;
        matrix old_transform = transform as matrix;
        matrix animation_matrix = Dm.@new<matrix>(old_transform);
        animation_matrix.Turn(Dm.num(pick(-30, 30)));
        animation_matrix.Scale(0.7f); // Shrink to start, end up normal sized

        pixel_x = from_x;
        pixel_y = from_y;
        alpha = 0;
        transform = animation_matrix;

        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_TEMPORARY_ANIMATION_START]))
            this._SendSignal(COMSIG_ATOM_TEMPORARY_ANIMATION_START, Dm.list(this, 3));
        // This is instant on byond's end, but to our clients this looks like a quick drop
        animate(this, Dm.named("alpha", old_alpha), Dm.named("pixel_x", old_x), Dm.named("pixel_y", old_y), Dm.named("transform", old_transform), Dm.named("time", 3), Dm.named("easing", CUBIC_EASING));
    }

    // tg: code/game/objects/items.dm:1604
    public virtual void animate_attack(atom_movable attacker = null, atom attacked_atom = null, object animation_type = null)
    {
        // The owner's play test (2026-10-09): the 3D client also draws the hit as a burst of pixel particles (HitEffectSeam);
        // since M4v slice 3 tg's own attack image below reaches the client too.
        HitEffectSeam.Report(attacked_atom);
        DmList image_override = Dm.list();
        DmList animation_override = Dm.list();
        float used_icon_angle = icon_angle;
        DmList angle_override = Dm.list();
        if (Dm.truthy(this._listen_lookup?[COMSIG_ITEM_ATTACK_ANIMATION]))
            this._SendSignal(COMSIG_ITEM_ATTACK_ANIMATION, Dm.list(this, attacker, attacked_atom, animation_type, image_override, animation_override, angle_override));
        image attack_image = null;
        if (!Dm.truthy(length(image_override)))
            attack_image = attack_icon == null ? image(icon: this) : image(icon: attack_icon, icon_state: attack_icon_state);
        else
            attack_image = image_override[1] as image;

        if (Dm.truthy(length(animation_override)))
            animation_type = animation_override[1];
        else if (!Dm.truthy(animation_type))
        {
            object sw__1 = get_sharpness();
            if (Dm.eq(sw__1, SHARP_EDGED))
                animation_type = ATTACK_ANIMATION_SLASH;
            else if (Dm.eq(sw__1, SHARP_POINTY))
                animation_type = ATTACK_ANIMATION_PIERCE;
            else
                animation_type = ATTACK_ANIMATION_BLUNT;
        }

        if (Dm.truthy(length(angle_override)))
            used_icon_angle = Dm.num(angle_override[1]);

        attack_image.plane = attacked_atom.plane + 1;
        attack_image.pixel_w = attacker.base_pixel_x + attacker.base_pixel_w - attacked_atom.base_pixel_x - attacked_atom.base_pixel_w;
        attack_image.pixel_z = attacker.base_pixel_y + attacker.base_pixel_z - attacked_atom.base_pixel_y - attacked_atom.base_pixel_z;
        // Scale the icon.
        attack_image.transform = Dm.mul(attack_image.transform, 0.5f);
        // The icon should not rotate.
        attack_image.appearance_flags = APPEARANCE_UI;

        atom_movable_flick_visual attack = attacked_atom.flick_overlay_view(attack_image, 1 * SECONDS) as atom_movable_flick_visual;
        MeleeGeometry.Set(attack, attacker, attacked_atom);
        matrix copy_transform = Dm.@new<matrix>(attacker.transform);
        float x_sign = 0;
        float y_sign = 0;
        float direction = get_dir(attacker, attacked_atom);
        if (Dm.truthy(Dm.bitand(direction, NORTH)))
            y_sign = -1;
        else if (Dm.truthy(Dm.bitand(direction, SOUTH)))
            y_sign = 1;

        if (Dm.truthy(Dm.bitand(direction, EAST)))
            x_sign = -1;
        else if (Dm.truthy(Dm.bitand(direction, WEST)))
            x_sign = 1;

        // Attacking self, or something on the same turf as us
        if (!Dm.truthy(direction))
        {
            y_sign = 1;
            // Not a fan of this, but its the "cleanest" way to animate this
            x_sign = 0.25f * (prob(50) ? 1 : -1);
            // For piercing attacks
            direction = SOUTH;
        }

        // And animate the attack!
        object sw__2 = animation_type;
        if (Dm.eq(sw__2, ATTACK_ANIMATION_BLUNT))
        {
            attack.pixel_x = 14 * x_sign;
            attack.pixel_y = 12 * y_sign;
            animate(attack, Dm.named("alpha", 175), Dm.named("transform", copy_transform.Scale(0.75f)), Dm.named("pixel_x", 4 * x_sign), Dm.named("pixel_y", 3 * y_sign), Dm.named("time", 0.2f * SECONDS));
            animate(Dm.named("time", 0.1f * SECONDS));
            animate(Dm.named("alpha", 0), Dm.named("time", 0.1f * SECONDS), Dm.named("easing", Dm.bitor(CIRCULAR_EASING, EASE_OUT)));
        }
        else if (Dm.eq(sw__2, ATTACK_ANIMATION_PIERCE))
        {
            float attack_angle = Dm.num(dir2angle(direction)) + rand(-7, 7);
            // Deducting 90 because we're assuming that icon_angle of 0 means an east-facing sprite
            float anim_angle = attack_angle - 90 - used_icon_angle;
            float angle_mult = 1;
            if (Dm.truthy(x_sign) && Dm.truthy(y_sign))
                angle_mult = 1.4f;
            attack.pixel_x = 22 * x_sign * angle_mult;
            attack.pixel_y = 18 * y_sign * angle_mult;
            attack.transform = ((matrix)attack.transform).Turn(anim_angle);
            copy_transform = copy_transform.Turn(anim_angle);
            animate(
                attack,
                Dm.named("pixel_x", (22 * x_sign - 12 * sin(attack_angle)) * angle_mult),
                Dm.named("pixel_y", (18 * y_sign - 8 * cos(attack_angle)) * angle_mult),
                Dm.named("time", 0.1f * SECONDS),
                Dm.named("easing", Dm.bitor(CUBIC_EASING, EASE_IN)));
            animate(
                attack,
                Dm.named("alpha", 175),
                Dm.named("transform", copy_transform.Scale(0.75f)),
                Dm.named("pixel_x", (22 * x_sign + 26 * sin(attack_angle)) * angle_mult),
                Dm.named("pixel_y", (18 * y_sign + 22 * cos(attack_angle)) * angle_mult),
                Dm.named("time", 0.3f * SECONDS),
                Dm.named("easing", Dm.bitor(CUBIC_EASING, EASE_OUT)));
            animate(
                Dm.named("alpha", 0),
                Dm.named("pixel_x", -3 * -(x_sign + sin(attack_angle))),
                Dm.named("pixel_y", -2 * -(y_sign + cos(attack_angle))),
                Dm.named("time", 0.1f * SECONDS),
                Dm.named("easing", Dm.bitor(CIRCULAR_EASING, EASE_OUT)));
        }
        else if (Dm.eq(sw__2, ATTACK_ANIMATION_SLASH))
        {
            attack.pixel_x = 18 * x_sign;
            attack.pixel_y = 14 * y_sign;
            float x_rot_sign = 0;
            float y_rot_sign = 0;
            float attack_dir = (prob(50) ? 1 : -1);
            float anim_angle = Dm.num(dir2angle(direction)) - 90 - used_icon_angle;

            if (Dm.truthy(x_sign))
                y_rot_sign = attack_dir;
            if (Dm.truthy(y_sign))
                x_rot_sign = attack_dir;

            // Animations are flipped, so flip us too!
            if (x_sign > 0 || y_sign < 0)
                attack_dir *= -1;

            // We're swinging diagonally, use separate logic
            float anim_dir = attack_dir;
            if (Dm.truthy(x_sign) && Dm.truthy(y_sign))
            {
                if (attack_dir < 0)
                {
                    x_rot_sign = -x_sign * 1.4f;
                    y_rot_sign = 0;
                }
                else
                {
                    x_rot_sign = 0;
                    y_rot_sign = -y_sign * 1.4f;
                }

                // Flip us if we've been flipped *unless* we're flipped due to both axis
                if ((x_sign < 0 && y_sign > 0) || (x_sign > 0 && y_sign < 0))
                    anim_dir *= -1;
            }

            attack.pixel_x += 10 * x_rot_sign;
            attack.pixel_y += 8 * y_rot_sign;
            attack.transform = ((matrix)attack.transform).Turn(anim_angle - 45 * anim_dir);
            copy_transform = copy_transform.Scale(0.75f);
            animate(attack, Dm.named("alpha", 175), Dm.named("time", 0.3f * SECONDS), Dm.named("flags", ANIMATION_PARALLEL));
            animate(Dm.named("time", 0.1f * SECONDS));
            animate(Dm.named("alpha", 0), Dm.named("time", 0.1f * SECONDS), Dm.named("easing", Dm.bitor(CIRCULAR_EASING, EASE_OUT)));

            animate(attack, Dm.named("transform", copy_transform.Turn(anim_angle + 45 * anim_dir)), Dm.named("time", 0.3f * SECONDS), Dm.named("flags", ANIMATION_PARALLEL));

            float x_return = 10 * -x_rot_sign;
            float y_return = 8 * -y_rot_sign;

            if (!Dm.truthy(x_rot_sign))
                x_return = 18 * x_sign;
            if (!Dm.truthy(y_rot_sign))
                y_return = 14 * y_sign;

            float angle_mult = 1;
            if (Dm.truthy(x_sign) && Dm.truthy(y_sign))
            {
                angle_mult = 1.4f;
                if (attack_dir > 0)
                {
                    x_return = 8 * x_sign;
                    y_return = 14 * y_sign;
                }
                else
                {
                    x_return = 18 * x_sign;
                    y_return = 6 * y_sign;
                }
            }

            animate(attack, Dm.named("pixel_x", 4 * x_sign * angle_mult), Dm.named("time", 0.2f * SECONDS), Dm.named("easing", Dm.bitor(CIRCULAR_EASING, EASE_IN)), Dm.named("flags", ANIMATION_PARALLEL));
            animate(Dm.named("pixel_x", x_return), Dm.named("time", 0.2f * SECONDS), Dm.named("easing", Dm.bitor(CIRCULAR_EASING, EASE_OUT)));

            animate(attack, Dm.named("pixel_y", 3 * y_sign * angle_mult), Dm.named("time", 0.2f * SECONDS), Dm.named("easing", Dm.bitor(CIRCULAR_EASING, EASE_IN)), Dm.named("flags", ANIMATION_PARALLEL));
            animate(Dm.named("pixel_y", y_return), Dm.named("time", 0.2f * SECONDS), Dm.named("easing", Dm.bitor(CIRCULAR_EASING, EASE_OUT)));
        }
    }

    // tg: code/game/objects/items.dm:1771
    public virtual async DmTask pick_painting_tool_color(mob user = null, object default_color = null)
    {
        // engineering E14 (the decal painter's custom colour)
        object chosen_color = await tgui_color_picker(user, I18n.T("Pick new color"), I18n.T("[]", this), default_color);
        if (!Dm.truthy(chosen_color) || QDELETED(this) || Dm.truthy(user.incapacitated) || !Dm.truthy(user.is_holding(this)))
            return;
        set_painting_tool_color(chosen_color);
    }

    // tg: code/game/objects/items.dm:1777
    public virtual void set_painting_tool_color(object chosen_color = null)
    {
        // engineering E14
        if (Dm.truthy(this._listen_lookup?[COMSIG_PAINTING_TOOL_SET_COLOR]))
            this._SendSignal(COMSIG_PAINTING_TOOL_SET_COLOR, Dm.list(this, chosen_color));
    }

    // tg: code/game/objects/items.dm:1784
    public virtual async DmTask<object> get_writing_implement_details()
    {
        return null; // gaps S3
    }

    // tg: code/game/objects/items.dm:1791
    public virtual object compare_zone_to_item_slot(object zone = null)
    {
        Dm.NotTranslated("code/game/objects/items.dm:1791", "compare_zone_to_item_slot: M2 P2 (skeleton)");
        return default;
    }

    // tg: code/game/objects/items.dm:1813
    public virtual void item_start_equip(atom target = null, obj_item equipping = null, mob user = null, float? show_visible_message = null)
    {
        show_visible_message ??= TRUE;

        if (Dm.truthy(show_visible_message))
        {
            if (HAS_TRAIT(equipping, TRAIT_DANGEROUS_EQUIP))
            {
                target.visible_message(
                    span_danger(I18n.T("[] tries to put [] on [].", user, equipping, target)),
                    span_userdanger(I18n.T("[] tries to put [] on you.", user, equipping)),
                    ignored_mobs: user);
            }
            else
            {
                target.visible_message(
                    span_notice(I18n.T("[] tries to put [] on [].", user, equipping, target)),
                    span_notice(I18n.T("[] tries to put [] on you.", user, equipping)),
                    ignored_mobs: user);
            }

            if (ishuman(target))
            {
                mob_living_carbon_human victim_human = target as mob_living_carbon_human;
                if (Dm.truthy(victim_human.key) && victim_human.client == null) // AKA braindead
                {
                    if (!IS_UNCONSCIOUS(victim_human) && LAZYLEN(victim_human.afk_thefts) <= AFK_THEFT_MAX_MESSAGES)
                    {
                        DmList new_entry = Dm.list(Dm.list(user.name, I18n.T("tried equipping you with []", equipping), world.time));
                        LAZYADD(ref victim_human.afk_thefts, new_entry);
                    }
                }
                else if (Dm.truthy(victim_human.is_blind()))
                    to_chat(target, span_userdanger(I18n.T("You feel someone trying to put something on you.")));
            }
        }
        user.do_item_attack_animation(target, used_item: equipping, animation_type: ATTACK_ANIMATION_BLUNT);

        to_chat(user, span_notice(I18n.T("You try to put [] on []...", equipping, target)));

        user.log_message(Dm.text("is putting [] on []", equipping, key_name(target)), LOG_ATTACK, color: "red");
        target.log_message(Dm.text("is having [] put on them by []", equipping, key_name(user)), LOG_VICTIM, color: "orange", log_globally: FALSE);
    }

    // tg: code/game/objects/items.dm:1851
    public virtual object modify_fantasy_variable(object variable_key = null, object value = null, object bonus = null, float? minimum = null)
    {
        Dm.NotTranslated("code/game/objects/items.dm:1851", "modify_fantasy_variable: M2 P2 (skeleton)");
        return default;
    }

    // tg: code/game/objects/items.dm:1868
    public virtual object reset_fantasy_variable(object variable_key = null, object current_value = null)
    {
        Dm.NotTranslated("code/game/objects/items.dm:1868", "reset_fantasy_variable: M2 P2 (skeleton)");
        return default;
    }

    // tg: code/game/objects/items.dm:1880
    public virtual async DmTask apply_fantasy_bonuses(object bonus = null)
    {
        Dm.NotTranslated("code/game/objects/items.dm:1880", "apply_fantasy_bonuses: M2 P2 (skeleton)");
    }

    // tg: code/game/objects/items.dm:1889
    public virtual async DmTask remove_fantasy_bonuses(object bonus = null)
    {
        Dm.NotTranslated("code/game/objects/items.dm:1889", "remove_fantasy_bonuses: M2 P2 (skeleton)");
    }

    // tg: code/game/objects/items.dm:1899
    public virtual object get_all_tool_behaviours()
    {
        if (tool_behaviour != null)
            return Dm.list(tool_behaviour);
        return null;
    }

    // tg: code/game/objects/items.dm:1917
    public virtual object update_weight_class(object new_w_class = null)
    {
        if (Dm.eq(w_class, new_w_class))
            return FALSE;

        float old_w_class = w_class;
        w_class = Dm.num(new_w_class);
        if (Dm.truthy(this._listen_lookup?[COMSIG_ITEM_WEIGHT_CLASS_CHANGED]))
            this._SendSignal(COMSIG_ITEM_WEIGHT_CLASS_CHANGED, Dm.list(this, old_w_class, new_w_class));
        if (loc != null)
        {
            atom t__1 = loc as atom;
            if (Dm.truthy(t__1._listen_lookup?[COMSIG_ATOM_CONTENTS_WEIGHT_CLASS_CHANGED]))
                t__1._SendSignal(COMSIG_ATOM_CONTENTS_WEIGHT_CLASS_CHANGED, Dm.list(t__1, this, old_w_class, new_w_class));
        }
        return TRUE;
    }

    /**
     * Used to determine if an item should be considered contraband by N-spect scanners or scanner gates.
     * Returns true when an item has the contraband trait, or is included in the traitor uplink.
     */
    // tg: code/game/objects/items.dm:1932  (gaps DE1: the N-spect scanner's)
    public virtual object is_contraband()
    {
        if (HAS_TRAIT(this, TRAIT_CONTRABAND))
            return TRUE;
        foreach (datum_uplink_item traitor_item in Dm.each_cast<datum_uplink_item>(SStraitor.uplink_items))
        {
            if (istype(this, traitor_item.item))
            {
                if (!Dm.truthy(Dm.bitand(traitor_item.uplink_item_flags, SYNDIE_TRIPS_CONTRABAND)))
                    return FALSE;
                return TRUE;
            }
        }
        return FALSE;
    }

    // tg: code/game/objects/items.dm:1942
    public override async DmTask apply_main_material_effects(datum_material main_material = null, object amount = null, object multiplier = null)
    {
        await base.apply_main_material_effects(main_material, amount, multiplier);
        if (Dm.truthy(Dm.bitand(material_flags, MATERIAL_GREYSCALE)))
        {
            Type main_mat_type = main_material.type;
            object worn_path = get_material_greyscale_config(main_mat_type, greyscale_config_worn);
            object lefthand_path = get_material_greyscale_config(main_mat_type, greyscale_config_inhand_left);
            object righthand_path = get_material_greyscale_config(main_mat_type, greyscale_config_inhand_right);
            await set_greyscale(null, null, worn_path, lefthand_path, righthand_path);
        }

        if (Dm.truthy(Dm.bitand(material_flags, MATERIAL_AFFECT_STATISTICS)) && !Dm.truthy(Dm.bitand(material_flags, MATERIAL_NO_SLOWDOWN)))
        {
            object flexibility = main_material.get_property(MATERIAL_FLEXIBILITY);
            // If the item applies slowdown only when worn, poor flexibility will increase our slowdown
            if (!Dm.truthy(Dm.bitand(item_flags, SLOWS_WHILE_IN_HAND)) && Dm.num(flexibility) < 6)
                slowdown = max(slowdown >= 0 ? 0 : slowdown, slowdown + (Dm.num(flexibility) - 6) * 0.025f * Dm.num(multiplier));
        }

        if (!Dm.truthy(main_material.item_sound_override))
            return;

        hitsound = main_material.item_sound_override;
        usesound = main_material.item_sound_override;
        mob_throw_hit_sound = main_material.item_sound_override;
        equip_sound = main_material.item_sound_override;
        pickup_sound = main_material.item_sound_override;
        drop_sound = main_material.item_sound_override;
    }

    // tg: code/game/objects/items.dm:1971
    public override async DmTask remove_main_material_effects(datum_material main_material = null, object amount = null, object multipier = null)
    {
        await base.remove_main_material_effects(main_material, amount, multipier);
        if (Dm.truthy(Dm.bitand(material_flags, MATERIAL_GREYSCALE)))
        {
            await set_greyscale(null, null,
                Dm.initial(this, (obj_item i) => i.greyscale_config_worn),
                Dm.initial(this, (obj_item i) => i.greyscale_config_inhand_left),
                Dm.initial(this, (obj_item i) => i.greyscale_config_inhand_right)
            );
        }

        if (Dm.truthy(Dm.bitand(material_flags, MATERIAL_AFFECT_STATISTICS)) && !Dm.truthy(Dm.bitand(material_flags, MATERIAL_NO_SLOWDOWN)))
        {
            object flexibility = main_material.get_property(MATERIAL_FLEXIBILITY);
            // If the item applies slowdown only when worn, poor flexibility will increase our slowdown
            if (!Dm.truthy(Dm.bitand(item_flags, SLOWS_WHILE_IN_HAND)) && Dm.num(flexibility) < 6)
                slowdown = min(Dm.initial(this, (obj_item i) => i.slowdown), slowdown - (Dm.num(flexibility) - 6) * 0.025f * Dm.num(multipier));
        }

        if (!Dm.truthy(main_material.item_sound_override))
            return;

        hitsound = Dm.initial(this, (obj_item i) => i.hitsound);
        usesound = Dm.initial(this, (obj_item i) => i.usesound);
        mob_throw_hit_sound = Dm.initial(this, (obj_item i) => i.mob_throw_hit_sound);
        equip_sound = Dm.initial(this, (obj_item i) => i.equip_sound);
        pickup_sound = Dm.initial(this, (obj_item i) => i.pickup_sound);
        drop_sound = Dm.initial(this, (obj_item i) => i.drop_sound);
    }

    // tg: code/game/objects/items.dm:1996
    public override async DmTask apply_single_mat_effect(datum_material material = null, object amount = null, object multiplier = null)
    {
        await base.apply_single_mat_effect(material, amount, multiplier);
        if (!Dm.truthy(Dm.bitand(material_flags, MATERIAL_AFFECT_STATISTICS)))
            return;

        float siemens_modifier = Dm.num(material.get_property(MATERIAL_INSULATION));
        // Cannot use the base formula as it would make any item with glass not conduct electricity
        if (siemens_modifier > 1)
            siemens_coefficient *= 1 + (siemens_modifier - 1) * Dm.num(multiplier);
        else
            siemens_coefficient *= max(0, 1 - (1 - siemens_modifier) * Dm.num(multiplier));

        if (siemens_coefficient == 0)
            Dm.mask(ref obj_flags, Dm.bitnot(CONDUCTS_ELECTRICITY));

        if (!Dm.truthy(Dm.bitand(material_flags, MATERIAL_NO_SLOWDOWN)))
            change_material_slowdown(material, amount, multiplier);
    }

    // tg: code/game/objects/items.dm:2014
    public override async DmTask remove_single_mat_effect(datum_material material = null, object amount = null, object multiplier = null)
    {
        await base.remove_single_mat_effect(material, amount, multiplier);
        if (!Dm.truthy(Dm.bitand(material_flags, MATERIAL_AFFECT_STATISTICS)))
            return;

        float siemens_modifier = Dm.num(material.get_property(MATERIAL_INSULATION));
        // Cannot use the base formula as it would make any item with glass not conduct electricity
        if (siemens_modifier > 1)
            siemens_coefficient /= 1 + (siemens_modifier - 1) * Dm.num(multiplier);
        else
        {
            float used_mult = 1 - (1 - siemens_modifier) * Dm.num(multiplier);
            if (used_mult > 0) // Perfect insulators need to be restored in finalize
                siemens_coefficient /= used_mult;
        }

        if (siemens_coefficient > 0 && Dm.truthy(Dm.bitand(Dm.initial(this, (obj o) => o.obj_flags), CONDUCTS_ELECTRICITY)) && !Dm.truthy(Dm.bitand(obj_flags, CONDUCTS_ELECTRICITY)))
            Dm.combine(ref obj_flags, CONDUCTS_ELECTRICITY);

        if (!Dm.truthy(Dm.bitand(material_flags, MATERIAL_NO_SLOWDOWN)))
            change_material_slowdown(material, amount, multiplier, removing: TRUE);
    }

    // tg: code/game/objects/items.dm:2034
    public virtual void change_material_slowdown(datum_material material = null, object mat_amount = null, object multiplier = null, float? removing = null)
    {
        removing ??= FALSE;
        // Density above 6 adds slowdown, density below 3 can reduce existing slowdown
        float density = Dm.num(material.get_property(MATERIAL_DENSITY));
        float slowdown_change = 0;

        if (density > 6)
            slowdown_change = (density - 6) * MATERIAL_DENSITY_SLOWDOWN * Dm.num(mat_amount) / SHEET_MATERIAL_AMOUNT;
        else if (density < 4)
            slowdown_change = (4 - density) * -MATERIAL_DENSITY_SLOWDOWN * Dm.num(mat_amount) / SHEET_MATERIAL_AMOUNT;

        if (!Dm.truthy(removing))
        {
            // Slowdown cannot be reduced below 0 if the item slows you down, or at all if the item speeds you up
            if (Dm.truthy(slowdown_change))
                slowdown = max(slowdown >= 0 ? 0 : slowdown, slowdown + slowdown_change * Dm.num(multiplier));
            return;
        }

        if (slowdown_change > 0)
            slowdown -= slowdown_change * Dm.num(multiplier);
        else if (slowdown_change < 0)
            // Not guaranteed to be correct if something modified our slowdown buuuut about as good as we can get
            slowdown = min(Dm.initial(this, (obj_item i) => i.slowdown), slowdown - slowdown_change * Dm.num(multiplier));
    }

    // tg: code/game/objects/items.dm:2056
    public override async DmTask finalize_remove_material_effects(DmList materials = null)
    {
        await base.finalize_remove_material_effects(materials);
        if (!Dm.truthy(Dm.bitand(material_flags, MATERIAL_AFFECT_STATISTICS)) || Dm.initial(this, (obj_item i) => i.siemens_coefficient) == 0 || siemens_coefficient != 0)
            return;
        // If we were made from an insulator we cannot restore via division
        siemens_coefficient = Dm.initial(this, (obj_item i) => i.siemens_coefficient);
        if (siemens_coefficient > 0 && Dm.truthy(Dm.bitand(Dm.initial(this, (obj o) => o.obj_flags), CONDUCTS_ELECTRICITY)) && !Dm.truthy(Dm.bitand(obj_flags, CONDUCTS_ELECTRICITY)))
            Dm.combine(ref obj_flags, CONDUCTS_ELECTRICITY);
    }

    // tg: code/game/objects/items.dm:2065
    public override void change_material_strength(datum_material material = null, object mat_amount = null, object multiplier = null, float? remove = null)
    {
        remove ??= FALSE;
        object force_mod = get_material_force_modifier(material);
        object throwforce_mod = get_material_throwforce_modifier(material);

        if (!Dm.truthy(remove))
        {
            force *= GET_MATERIAL_MODIFIER(force_mod, multiplier);
            throwforce *= GET_MATERIAL_MODIFIER(throwforce_mod, multiplier);
        }
        else
        {
            force /= GET_MATERIAL_MODIFIER(force_mod, multiplier);
            throwforce /= GET_MATERIAL_MODIFIER(throwforce_mod, multiplier);
        }
    }

    /// Returns a force multiplier from a material for a given sharpness
    // tg: code/game/objects/items.dm:2077
    public virtual object get_material_force_modifier(datum_material material = null, object item_sharpness = null)
    {
        item_sharpness ??= get_sharpness();
        float density = Dm.num(material.get_property(MATERIAL_DENSITY));
        float hardness = Dm.num(material.get_property(MATERIAL_HARDNESS));
        float flexibility = Dm.num(material.get_property(MATERIAL_FLEXIBILITY));
        float force_mod = 1;
        object sw__1 = item_sharpness;
        if (Dm.eq(sw__1, NONE))
        {
            // Blunt items are really hurt by all the flexing
            force_mod = (1 + (density - 4) * 0.1f) / (1 + flexibility * 0.1f);
        }
        else if (Dm.eq(sw__1, SHARP_EDGED))
        {
            // Sharp items don't care about density and need high hardness to get a real bonus, but can tolerate (and benefit from) some flex
            force_mod = 1 + (hardness - 4) * 0.1f;

            // Peaks out at 20% at flexibility of 1, drops off up to -80% at 10
            if (flexibility < 2)
                force_mod *= 1 + (1 - abs(1 - flexibility)) * 0.2f;
            else
                force_mod *= 1 - (flexibility - 2) * 0.1f;
        }
        else if (Dm.eq(sw__1, SHARP_POINTY))
        {
            // Pointy items care about both density and hardness
            force_mod = 1 + MATERIAL_PROPERTY_DIVERGENCE(density, 4, 6) * 0.05f + (hardness - 4) * 0.1f;
            // But are not affected by flexibility until higher values, although they don't benefit from it either
            if (flexibility > 4)
                force_mod *= (1 - (flexibility - 4) * 0.2f);
        }

        // Just for sanity in case something breaks
        force_mod = round(clamp(force_mod, MATERIAL_MIN_FORCE_MULTIPLIER, MATERIAL_MAX_FORCE_MULTIPLIER), 0.01f);
        return force_mod;
    }

    /// Returns a force multiplier from a material for a given sharpness
    // tg: code/game/objects/items.dm:2109
    public virtual object get_material_throwforce_modifier(datum_material material = null, object item_sharpness = null)
    {
        item_sharpness ??= get_sharpness();
        float density = Dm.num(material.get_property(MATERIAL_DENSITY));
        float hardness = Dm.num(material.get_property(MATERIAL_HARDNESS));
        float flexibility = Dm.num(material.get_property(MATERIAL_FLEXIBILITY));
        float throwforce_mod = 1;
        object sw__1 = item_sharpness;
        if (Dm.eq(sw__1, NONE))
        {
            // Blunt items are really hurt by all the flexing
            throwforce_mod = 1 + (density - 4) * 0.1f - flexibility * 0.1f;
        }
        else if (Dm.eq(sw__1, SHARP_EDGED))
        {
            // Sharp items don't care about density and need high hardness to get a real bonus, but can tolerate (and benefit from) some flex
            throwforce_mod = 1 + (hardness - 4) * 0.1f;

            // Peaks out at 20% at flexibility of 1, drops off up to -80% at 10
            if (flexibility < 2)
                throwforce_mod += (1 - abs(1 - flexibility)) * 0.2f;
            else
                throwforce_mod -= (flexibility - 2) * 0.1f;
        }
        else if (Dm.eq(sw__1, SHARP_POINTY))
        {
            // Pointy items care about both density and hardness
            throwforce_mod = 1 + MATERIAL_PROPERTY_DIVERGENCE(density, 4, 6) * 0.05f * 0.05f + (hardness - 4) * 0.1f;
            // But are not affected by flexibility until higher values, although they don't benefit from it either
            if (flexibility > 4)
                throwforce_mod -= (flexibility - 4) * 0.2f;
        }

        // Just for sanity in case something breaks
        throwforce_mod = round(clamp(throwforce_mod, MATERIAL_MIN_FORCE_MULTIPLIER, MATERIAL_MAX_FORCE_MULTIPLIER), 0.01f);
        return throwforce_mod;
    }

    // tg: code/game/objects/items.dm:2148
    public virtual object get_proxy_attacker_for(atom target = null, mob user = null)
    {
        return this;
    }

    // tg: code/game/objects/items.dm:2154
    public virtual object check_bait(obj_item_fish fish = null)
    {
        Dm.NotTranslated("code/game/objects/items.dm:2154", "check_bait: M2 P2 (skeleton)");
        return default;
    }

    /// Fetches, or lazyloads, our embedding datum
    // tg: code/game/objects/items.dm:2200
    public virtual object get_embed()
    {
        // Something may call this during qdeleting, which would cause a harddel
        if (QDELETED(this))
            return null;
        if (embed_data != null)
            return embed_data;
        if (embed_type != null)
            embed_data = (datum_embedding)Dm.@new(embed_type, this);
        return embed_data;
    }

    /// Sets our embedding datum to a different one. Can also take types
    // tg: code/game/objects/items.dm:2212
    public virtual void set_embed(object new_embed = null)
    {
        if (new_embed == embed_data)
            return;

        // Needs to be QDELETED as embed data uses this to clean itself up from its parent (us)
        if (!QDELETED(embed_data))
            qdel(embed_data);

        if (ispath(new_embed))
            new_embed = Dm.@new((Type)new_embed, this);

        embed_data = new_embed as datum_embedding;
        if (Dm.truthy(this._listen_lookup?[COMSIG_ITEM_EMBEDDING_UPDATE]))
            this._SendSignal(COMSIG_ITEM_EMBEDDING_UPDATE, Dm.list(this));
    }

    /// Embed ourselves into an object if we possess embedding data
    // tg: code/game/objects/items.dm:2227
    public virtual async DmTask<object> force_embed(mob_living_carbon victim = null, object target_limb = null)
    {
        if (victim is not mob_living_carbon)
            return FALSE;

        if (target_limb is not obj_item_bodypart)
            target_limb = victim.get_bodypart(target_limb as string) is var by_zone && Dm.truthy(by_zone) ? by_zone : victim.get_bodypart();

        return get_embed() is datum_embedding embed ? await embed.embed_into(victim, target_limb as obj_item_bodypart) : null;
    }

    // tg: code/game/objects/items.dm:2237
    public virtual async DmTask<object> can_insert_container(mob_living user = null, obj_machinery chem_machine = null)
    {
        // gaps P5 (the chemistry machines)
        return Dm.truthy(is_chem_container()) && Dm.truthy(await chem_machine.can_interact(user)) && Dm.truthy(await user.can_perform_action(chem_machine, Dm.bitor(ALLOW_SILICON_REACH, FORBID_TELEKINESIS_REACH))) ? TRUE : FALSE;
    }

    // tg: code/game/objects/items.dm:2241
    public virtual object is_chem_container()
    {
        return FALSE; // gaps P5 (the chemistry machines)
    }
}

// tg: code/game/objects/items.dm:1129
public partial class atom_movable : atom
{
    ///What should The atom that blended an object do with it afterwards? Default behaviour is to delete it
    // tg: code/game/objects/items.dm:1129  (E15)
    public virtual async DmTask<object> blended(obj_item blended_item = null, object grinded = null)
    {
        qdel(blended_item);

        return TRUE;
    }

    // tg: code/game/objects/items.dm:1586
    public virtual void do_item_attack_animation(atom attacked_atom = null, object visual_effect_icon = null, obj_item used_item = null, object animation_type = null)
    {
        if (!Dm.truthy(visual_effect_icon))
        {
            if (Dm.truthy(used_item))
                used_item.animate_attack(this, attacked_atom, animation_type);
            return;
        }

        // The owner's play test (2026-10-09): the 3D client also draws the hit as a burst of pixel particles (HitEffectSeam);
        // since M4v slice 3 tg's own effect image below reaches the client too.
        HitEffectSeam.Report(attacked_atom);
        image attack_image = image(icon: "icons/effects/effects.dmi", icon_state: visual_effect_icon);
        attack_image.plane = attacked_atom.plane + 1;
        // Scale the icon.
        attack_image.transform = Dm.mul(attack_image.transform, 0.4f);
        // The icon should not rotate.
        attack_image.appearance_flags = APPEARANCE_UI;
        atom_movable_flick_visual attack = attacked_atom.flick_overlay_view(attack_image, 1 * SECONDS) as atom_movable_flick_visual;
        MeleeGeometry.Set(attack, this, attacked_atom);
        matrix copy_transform = Dm.@new<matrix>(transform);
        animate(attack, Dm.named("alpha", 175), Dm.named("transform", copy_transform.Scale(0.75f)), Dm.named("time", 0.3f * SECONDS));
        animate(Dm.named("time", 0.1f * SECONDS));
        animate(Dm.named("alpha", 0), Dm.named("time", 0.3f * SECONDS), Dm.named("easing", Dm.bitor(CIRCULAR_EASING, EASE_OUT)));
    }
}

// tg: code/game/objects/items.dm:2173 (global procs)
public static partial class GlobalProcs
{
    // tg: code/game/objects/items.dm:2173
    public static object is_matching_bait(obj_item bait = null, object identifier = null)
    {
        Dm.NotTranslated("code/game/objects/items.dm:2173", "is_matching_bait: M2 P2 (skeleton)");
        return default;
    }
}

// tg: code/game/objects/items.dm:290
public partial class obj_item : obj
{
    // tg: code/game/objects/items.dm:290
    [ShouldNotOverride]
    public override async DmTask<object> click_ctrl(mob user = null)
    {
        object dot = null;
        //If the item is on the ground & not anchored we allow the player to drag it
        dot = await item_ctrl_click(user);
        if (Dm.truthy(Dm.bitand(dot, CLICK_ACTION_ANY)))
            return (isturf(loc) && !Dm.truthy(anchored)) ? NONE : dot; //allow the object to get dragged on the floor
        return dot;
    }

    // tg: code/game/objects/items.dm:418
    public override object examine_tags(mob user = null)
    {
        DmList dot = null;
        DmList parent_tags = base.examine_tags(user) as DmList;
        parent_tags.Insert(1, weight_class_to_text(w_class)); // To make size display first, otherwise it looks goofy
        dot = parent_tags;
        dot[weight_class_to_text(w_class)] = weight_class_to_tooltip(w_class);

        if (Dm.truthy(Dm.bitand(item_flags, CRUEL_IMPLEMENT)))
            dot[span_red("morbid")] = "It seems quite practical for particularly morbid procedures and experiments.";
        if (HAS_TRAIT(this, TRAIT_BLUESPACE_INTERFERENCE))
            dot["bluespace-active"] = "It is highly active in bluespace and will cause malfunctions in teleporters.";
        if (siemens_coefficient == 0)
            dot["insulated"] = "It is made from a robust electrical insulator and will block any electricity passing through it!";
        else if (siemens_coefficient <= 0.5f)
            dot["partially insulated"] = "It is made from a poor insulator that will dampen (but not fully block) electric shocks passing through it.";
        if (Dm.truthy(Dm.bitand(item_flags, CAN_BE_OVERSLOT)))
            dot["form-fitting"] = "It does not block MODsuits from deploying when worn.";
        return dot;
    }

    // tg: code/game/objects/items.dm:435
    public override object examine_descriptor(mob user = null)
    {
        return "item";
    }

    // tg: code/game/objects/items.dm:438
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        // lazily initialize the weapon description element if it hasn't been already
        if (!Dm.truthy(Dm.bitand(item_flags, WEAPON_DESCRIPTION_INITIALIZED)))
        {
            await add_weapon_description();
            Dm.combine(ref item_flags, WEAPON_DESCRIPTION_INITIALIZED);
        }
        return await base.examine(user, __extra);
    }

    // tg: code/game/objects/items.dm:597 (gaps package C5: a monkey's paw)
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        if (InertSeam.MissingOverride(this, "attack_paw", user))
            return null;
        object dot = await base.attack_paw(user, modifiers);
        if (Dm.truthy(dot) || user == null || Dm.truthy(anchored))
            return dot;
        return await attempt_pickup(user as mob_living);
    }

    // tg: code/game/objects/items.dm:603  (gaps X5: an alien's claws pick only what its picky component lets it)
    public override async DmTask<object> attack_alien(mob_living_carbon user = null, DmList modifiers = null)
    {
        mob_living_carbon_alien ayy = user as mob_living_carbon_alien;

        if (!Dm.truthy(ayy.can_hold_items(this)))
        {
            if (Dm.@in(this, ayy.contents)) // To stop Aliens having items stuck in their pockets
                await ayy.dropItemToGround(this);
            to_chat(user, span_warning(I18n.T("Your claws aren't capable of such fine manipulation!")));
            return null;
        }
        await attack_paw(ayy, modifiers);
        return null;
    }

    // tg: code/game/objects/items.dm:540
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        // DEVIATION(DV27): the interaction guard (D132) on the item's own attack_hand. /obj/item is the base definition every
        // item's click with an empty hand reaches; the guard of /atom/attack_hand below returns tg's "not handled", after
        // which this definition would pick the item up, so the refusal is made here first.
        if (InertSeam.MissingOverride(this, "attack_hand", user))
            return null;
        object dot = null;
        dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot) || !Dm.truthy(user) || Dm.truthy(anchored))
            return dot;
        return await attempt_pickup(user as mob_living);
    }

    // tg: code/game/objects/items.dm:863
    public override object pre_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null)
    {
        get_embed(); // Ensure that embedding is lazyloaded before we impact the target, if we can have it
        object impact_flags = base.pre_impact(hit_atom, throwingdatum);
        if (w_class < WEIGHT_CLASS_BULKY)
            impact_flags = Dm.bitor(impact_flags, COMPONENT_MOVABLE_IMPACT_FLIP_HITPUSH);
        if (!Dm.truthy(Dm.bitand(impact_flags, COMPONENT_MOVABLE_IMPACT_NEVERMIND)) && Dm.truthy(get_temperature()) && isliving(hit_atom))
        {
            mob_living victim = hit_atom as mob_living;
            victim.ignite_mob();
        }
        return impact_flags;
    }

    // tg: code/game/objects/items.dm:873  (waitfor = FALSE inherited from /atom/movable/throw_impact; this definition can
    // sleep only through ..(), so it needs no wrapper of its own: waiting-model.md 5.4)
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        object dot = null;
        dot = base.throw_impact(hit_atom, throwingdatum, __extra);

        if (!isliving(hit_atom)) //Living mobs handle hit sounds differently.
        {
            throw_drop_sound_chain(YEET_SOUND_VOLUME);
            return dot;
        }

        if (Dm.truthy(dot)) //it's been caught.
            return dot;

        object volume = get_volume_by_throwforce_and_or_w_class();
        if (!Dm.truthy(volume))
            return dot;
        if (throwforce > 0 || HAS_TRAIT(this, TRAIT_CUSTOM_TAP_SOUND))
            mob_throw_hit_sound_chain(hit_atom, volume);
        else
        {
            playsound(hit_atom, Dm.resource("sound/items/weapons/throwtap.ogg"), volume, TRUE, -1); // tg: code/game/objects/items.dm:889
        }
        return dot;
    }

    // tg: code/game/objects/items.dm:891
    [DmParams("target", "range", "speed", "thrower", "spin", "diagonals_first", "callback", "force", "gentle", "quickstart", "throw_type_path")]
    public override async DmTask<object> throw_at(atom target = null, object range = null, object speed = null, atom thrower = null, float? spin = null, float? diagonals_first = null, datum_callback callback = null, float? force = null, float? gentle = null, object quickstart = null, Type throw_datum_typepath = null, params object[] __extra)
    {
        spin ??= 1;
        diagonals_first ??= 0;
        gentle ??= FALSE;
        quickstart ??= TRUE;
        throw_datum_typepath ??= typeof(datum_thrownthing);
        object dot = null;
        // The interaction guard (D132) for a thrown item sits at /atom/movable/throw_impact and /atom/hitby: an item whose own
        // impact code is not translated lands with tg's base impact only.
        if (HAS_TRAIT(this, TRAIT_NODROP))
            return dot;
        callback = Dm.@new<datum_callback>(this, nameof(after_throw), callback); //replace their callback with our own
        dot = await base.throw_at(target, range, speed, thrower, spin, diagonals_first, callback, force, gentle, quickstart: quickstart);
        return dot;
    }

    // tg: code/game/objects/items.dm:975
    public override async DmTask<object> hitby(atom_movable hitting_atom = null, object skipcatch = null, object hitpush = null, object blocked = null, datum_thrownthing throwingdatum = null)
    {
        return (!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_HITBY]) ? NONE : this._SendSignal(COMSIG_ATOM_HITBY, Dm.list(this, hitting_atom, skipcatch, hitpush, blocked, throwingdatum)));
    }

    // tg: code/game/objects/items.dm:1160
    public override async DmTask MouseEntered(object location = null, object control = null, object @params = null)
    {
        await base.MouseEntered(location, control, @params);
        if (((get__item(this, typeof(mob)) == usr) || Dm.truthy(loc?.atom_storage) || Dm.truthy(Dm.bitand(item_flags, IN_STORAGE))) && !QDELETED(this)) //nullspace exists.
        {
            // DEVIATION(DV47): usr.client.prefs.read_preference(/datum/preference/toggle/enable_tooltips) and
            // read_preference(/datum/preference/numeric/tooltip_delay) are read as the preferences' defaults, TRUE and 500
            // (modules/client/preferences/tooltips.dm:9, _preference.dm:561): the preferences are not translated (client/New's
            // prefs are a deferred leaf, M9) and a null prefs would show no item tooltip.
            // GAP(lead, modules/client/preferences.cs): /datum/preferences/read_preference.
            object enable_tooltips = TRUE;
            if (Dm.truthy(enable_tooltips))
            {
                float timedelay = 500 / 100f;
                tip_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(openTip), location, control, @params, usr), timedelay, TIMER_STOPPABLE, file: "code/game/objects/items.dm", line: 1166); //timer takes delay in deciseconds, but the pref is in milliseconds. dividing by 100 converts it.
            }
            // if(usr.client.prefs.read_preference(/datum/preference/toggle/item_outlines)) apply_outline(...) (:1167-1171): the
            // hover outline is a filter; the 3D client draws its own hover highlight (interaction.md 4); presentation, a deferred
            // leaf.
            Dm.Deferred("code/game/objects/items.dm:1167", "MouseEntered: the hover outline (apply_outline, a filter; presentation)");
        }
    }

    // tg: code/game/objects/items.dm:1180
    public override void MouseExited(object location = null, object control = null, object @params = null)
    {
        deltimer(tip_timer); //delete any in-progress timer if the mouse is moved off the item before it finishes
        closeToolTip(usr);
        remove_filter(HOVER_OUTLINE_FILTER);
    }

    // GAP(lead): tg's global proc get(atom/loc, type) (__HELPERS/atoms.dm:312) has no GlobalProcs member yet; its body,
    // a local copy for MouseEntered.
    // tg: code/__HELPERS/atoms.dm:312
    private static atom get__item(atom loc, Type type)
    {
        while (Dm.truthy(loc))
        {
            if (istype(loc, type))
                return loc;
            loc = loc.loc;
        }
        return null;
    }
}

// tg: code/game/objects/items.dm:851 (interleaved types, rules 1.9; M5x slice 5)
public partial class obj_item : obj
{
    // tg: code/game/objects/items.dm:851
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        await base.singularity_pull(singularity, current_size);
        if (QDELETED(this))
            return null;

        if (Dm.num(current_size) >= STAGE_FOUR)
            await throw_at(singularity, 14, 3, spin: 0);
        return null;
    }
}

// Gaps package C1.
public partial class obj_item : obj
{
    // tg: code/game/objects/items.dm:981
    public override async DmTask<object> attack_animal(mob user = null, DmList modifiers = null)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, CAN_BE_HIT)))
            return await base.attack_animal(user, modifiers);
        return 0;
    }
}
