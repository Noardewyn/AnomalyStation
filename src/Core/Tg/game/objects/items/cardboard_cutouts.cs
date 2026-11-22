// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/cardboard_cutouts.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3c, D316). A cutout's picture is the dressed dummy's look itself, not getFlatIcon's 2D flattening of it
// (as the mother of the hallucinations, mother.cs). DV372: a cutout whose outfit or species is not live yet is left out of the
// crayon's menu (cutout_waits below), and a cutout that starts as one stays blank, until its outfit or species is ported. The var
// blocks are generated (D111).

//Cardboard cutouts! They're man-shaped and can be colored with a crayon to look like a human in a certain outfit, although it's limited, discolored, and obvious to more than a cursory glance.
// tg: code/game/objects/items/cardboard_cutouts.dm:2
public partial class obj_item_cardboard_cutout : obj_item
{
    // tg: code/game/objects/items/cardboard_cutouts.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(starting_cutout))
            return INITIALIZE_HINT_LATELOAD;
        if (!Dm.truthy(pushed_over))
            tacticool = Waiting.NoSleep(AddComponent(typeof(datum_component_tactical))) as datum_component_tactical;
        return dot;
    }

    // tg: code/game/objects/items/cardboard_cutouts.dm:28
    public override object Destroy(float? force = null)
    {
        tacticool = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/cardboard_cutouts.dm:32
    public override void LateInitialize(params object[] __extra)
    {
        if (starting_cutout == null)
            throw Dm.CRASH("assertion failed: !isnull(starting_cutout)", "code/game/objects/items/cardboard_cutouts.dm", 33);

        datum_cardboard_cutout cutout = null;
        foreach (Type cutout_subtype in Dm.each_cast<Type>(subtypesof(typeof(datum_cardboard_cutout))))
        {
            if (!Dm.eq(Dm.initial(cutout_subtype, (datum_cardboard_cutout c) => c.name), starting_cutout))
                continue;

            // DV372: a cutout whose outfit or species is not live stays blank
            if (cutout_waits(cutout_subtype))
            {
                Dm.Deferred("code/game/objects/items/cardboard_cutouts.dm:40", Dm.text("LateInitialize: the [] cutout (its outfit or species is not ported yet, DV372)", starting_cutout));
                return;
            }
            cutout = get_cardboard_cutout_instance(cutout_subtype);
        }

        if (cutout == null)
            throw Dm.CRASH(Dm.text("No cutout found with name []", starting_cutout), "code/game/objects/items/cardboard_cutouts.dm", 42);

        cutout.apply(this);
        if (!Dm.truthy(pushed_over))
            tacticool = Waiting.NoSleep(AddComponent(typeof(datum_component_tactical))) as datum_component_tactical;
    }

    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/game/objects/items/cardboard_cutouts.dm:49
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        mob_living living_user = user as mob_living;
        if (!Dm.truthy(living_user?.combat_mode) || Dm.truthy(pushed_over) || !isturf(loc))
            return await base.attack_hand(user, modifiers, __extra);
        user.visible_message(span_warning(I18n.T("[] pushes over []!", user, this)), span_danger(I18n.T("You push over []!", this)));
        playsound(this, Dm.resource("sound/items/weapons/genhit.ogg"), 50, TRUE);
        await push_over();
        return null;
    }

    // tg: code/game/objects/items/cardboard_cutouts.dm:56
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        //Because of the tactical element, the user won't tilt left and right, but it'll still hop.
        await user.AddElementTrait(TRAIT_WADDLING, REF(this), typeof(datum_element_waddling));
    }

    // tg: code/game/objects/items/cardboard_cutouts.dm:61
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        REMOVE_TRAIT(user, TRAIT_WADDLING, REF(this));
        return dot;
    }

    // tg: code/game/objects/items/cardboard_cutouts.dm:65
    public virtual async DmTask push_over()
    {
        appearance = Dm.initial(this, (obj_item_cardboard_cutout c) => c.appearance);
        desc = Dm.text("[] It's been pushed over.", Dm.initial(this, (obj_item_cardboard_cutout c) => c.desc));
        icon_state = "cutout_pushed_over";
        await remove_atom_colour(FIXED_COLOUR_PRIORITY);
        pushed_over = TRUE;
        QDEL_NULL(ref tacticool);
    }

    // tg: code/game/objects/items/cardboard_cutouts.dm:73
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(pushed_over))
            return null;
        to_chat(user, span_notice(I18n.T("You right [].", this)));
        desc = Dm.initial(this, (obj_item_cardboard_cutout c) => c.desc);
        icon = Dm.initial(this, (obj_item_cardboard_cutout c) => c.icon);
        icon_state = Dm.initial(this, (obj_item_cardboard_cutout c) => c.icon_state); //This resets a cutout to its blank state - this is intentional to allow for resetting
        pushed_over = FALSE;
        tacticool = await AddComponent(typeof(datum_component_tactical)) as datum_component_tactical;
        return null;
    }

    // tg: code/game/objects/items/cardboard_cutouts.dm:83
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_toy_crayon crayon)
            return NONE;
        await change_appearance(crayon, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/cardboard_cutouts.dm:89
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        object dot = await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
        float damage_sustained = Dm.truthy(dot) ? Dm.num(dot) : 0;
        if ((Dm.eq(damage_flag, BULLET) || Dm.eq(damage_flag, MELEE)) && Dm.eq(damage_type, BRUTE) && prob(damage_sustained))
            await push_over();
        return dot;
    }

    // tg: code/game/objects/items/cardboard_cutouts.dm:95
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        await Dm.new_async<obj_item_stack_sheet_cardboard>(loc);
    }

    /**
     * change_appearance: Changes a skin of the cardboard cutout based on a user's choice
     *
     * Arguments:
     * * crayon The crayon used to change and recolor the cardboard cutout
     * * user The mob choosing a skin of the cardboard cutout
     */
    // tg: code/game/objects/items/cardboard_cutouts.dm:115
    public virtual async DmTask<object> change_appearance(obj_item_toy_crayon crayon = null, mob_living user = null)
    {
        DmList appearances_by_name = Dm.list();
        DmList possible_appearances = Dm.list();

        foreach (Type cutout_subtype in Dm.each_cast<Type>(subtypesof(typeof(datum_cardboard_cutout))))
        {
            if (cutout_waits(cutout_subtype)) // DV372
                continue;
            datum_cardboard_cutout cutout = get_cardboard_cutout_instance(cutout_subtype);
            appearances_by_name[cutout.name] = cutout;
            possible_appearances[cutout.name] = Dm.image(icon: cutout.preview_appearance);
        }

        object new_appearance = await show_radial_menu(user, this, possible_appearances, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user, crayon), radius: 36, require_near: TRUE);
        if (!Dm.truthy(new_appearance))
            return FALSE;
        if (!Dm.truthy(await do_after(user, 1 * SECONDS, this, timed_action_flags: IGNORE_HELD_ITEM)))
            return FALSE;
        if (!Dm.truthy(check_menu(user, crayon)))
            return FALSE;
        user.visible_message(span_notice(I18n.T("[] gives [] a new look.", user, this)), span_notice(I18n.T("Voila! You give [] a new look.", this)));
        // crayon.use_charges(1): tg passes the 1 as the user (the amount is its default 1)
        await crayon.use_charges(null);
        crayon.check_empty(user);
        alpha = 255;
        icon = Dm.initial(this, (obj_item_cardboard_cutout c) => c.icon);
        if (!Dm.truthy(deceptive))
            await add_atom_colour("#FFD7A7", FIXED_COLOUR_PRIORITY);
        datum_cardboard_cutout chosen = appearances_by_name[new_appearance] as datum_cardboard_cutout;
        chosen.apply(this);
        return TRUE;
    }

    /**
     * check_menu: Checks if we are allowed to interact with a radial menu
     *
     * Arguments:
     * * user The mob interacting with a menu
     * * crayon The crayon used to interact with a menu
     */
    // tg: code/game/objects/items/cardboard_cutouts.dm:151
    public virtual object check_menu(mob_living user = null, obj_item_toy_crayon crayon = null)
    {
        if (user == null)
            return FALSE;
        if (Dm.truthy(user.incapacitated))
            return FALSE;
        if (Dm.truthy(pushed_over))
        {
            to_chat(user, span_warning(I18n.T("Right [] first!", this)));
            return FALSE;
        }
        if (crayon == null || !Dm.truthy(user.is_holding(crayon)))
            return FALSE;
        if (Dm.truthy(crayon.check_empty(user)))
            return FALSE;
        if (Dm.truthy(crayon.is_capped))
        {
            to_chat(user, span_warning(I18n.T("Take the cap off first!")));
            return FALSE;
        }
        return TRUE;
    }

    /// DV372: a cutout is made from a dummy dressed in its outfit, of its species; while either is not live the cutout waits
    public static bool cutout_waits(Type cutout_subtype)
    {
        Type outfit = Dm.initial(cutout_subtype, (datum_cardboard_cutout c) => c.outfit);
        Type species = Dm.initial(cutout_subtype, (datum_cardboard_cutout c) => c.species);
        return (outfit != null && Attribute.IsDefined(outfit, typeof(DmInertAttribute), inherit: false))
            || (species != null && Attribute.IsDefined(species, typeof(DmInertAttribute), inherit: false));
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/cardboard_cutouts.dm:101  var/static/list/cardboard_cutouts
    internal DmList get_cardboard_cutout_instance__cardboard_cutouts;
}

public static partial class GlobalProcs
{
    // tg: code/game/objects/items/cardboard_cutouts.dm:98
    public static datum_cardboard_cutout get_cardboard_cutout_instance(Type cardboard_cutout = null)
    {
        if (!ispath(cardboard_cutout))
            throw Dm.CRASH(Dm.text("[] is not a path of /datum/cardboard_cutout", cardboard_cutout), "code/game/objects/items/cardboard_cutouts.dm", 99);

        DmList cardboard_cutouts = TgState.Current.get_cardboard_cutout_instance__cardboard_cutouts ??= Dm.list();

        if (cardboard_cutouts[cardboard_cutout] == null)
            cardboard_cutouts[cardboard_cutout] = Dm.@new(cardboard_cutout);

        return cardboard_cutouts[cardboard_cutout] as datum_cardboard_cutout;
    }
}

// tg: code/game/objects/items/cardboard_cutouts.dm:175
public partial class datum_cardboard_cutout : datum
{
    // tg: code/game/objects/items/cardboard_cutouts.dm:201
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (Dm.truthy(direct_icon))
            preview_appearance = mutable_appearance(direct_icon, direct_icon_state) as mutable_appearance;
        else
            // tg passes l_hand and r_hand in the other order than the proc's (r_hand, l_hand)
            preview_appearance = await get_dynamic_human_appearance(outfit, species, mob_spawner, l_hand, r_hand, animated: FALSE) as mutable_appearance;
    }

    /// This proc returns the name that the cardboard cutout item will use.
    // tg: code/game/objects/items/cardboard_cutouts.dm:209
    public virtual object get_name() => applied_name;

    /// This proc sets the cardboard cutout item's vars.
    // tg: code/game/objects/items/cardboard_cutouts.dm:213
    public virtual void apply(obj_item_cardboard_cutout cutouts = null)
    {
        // image(fcopy_rsc(getFlatIcon(preview_appearance, no_anim = TRUE))): the look itself (see the file's header)
        applied_appearance ??= Dm.image(icon: preview_appearance);
        applied_appearance.plane = cutouts.plane;
        applied_appearance.layer = cutouts.layer;
        mutable_appearance mirror = Dm.@new<mutable_appearance>(applied_appearance);
        cutouts.appearance = mirror;
        cutouts.name = Dm.str(get_name());
        cutouts.desc = applied_desc;
        cutouts.update_appearance(); //forces an update on the tactical comp's appearance.
    }
}

// tg: code/game/objects/items/cardboard_cutouts.dm:224
public partial class datum_cardboard_cutout_assistant : datum_cardboard_cutout
{
    // tg: code/game/objects/items/cardboard_cutouts.dm:230
    public override object get_name() => Dm.text("[] []", pick(GLOB.first_names_male), pick(GLOB.last_names));
}

// tg: code/game/objects/items/cardboard_cutouts.dm:233
public partial class datum_cardboard_cutout_clown : datum_cardboard_cutout
{
    // tg: code/game/objects/items/cardboard_cutouts.dm:239
    public override object get_name() => pick(GLOB.clown_names);
}

// tg: code/game/objects/items/cardboard_cutouts.dm:242
public partial class datum_cardboard_cutout_mime : datum_cardboard_cutout
{
    // tg: code/game/objects/items/cardboard_cutouts.dm:248
    public override object get_name() => pick(GLOB.mime_names);
}

// tg: code/game/objects/items/cardboard_cutouts.dm:251
public partial class datum_cardboard_cutout_traitor : datum_cardboard_cutout
{
    // tg: code/game/objects/items/cardboard_cutouts.dm:257
    public override object get_name() => pick("Unknown", "Captain");
}

// tg: code/game/objects/items/cardboard_cutouts.dm:260
public partial class datum_cardboard_cutout_nuclear_operative : datum_cardboard_cutout
{
    // tg: code/game/objects/items/cardboard_cutouts.dm:266
    public override object get_name() => pick("Unknown", "COMMS", "Telecomms", "AI", "stealthy op", "STEALTH", "sneakybeaky", "MEDIC", "Medic");
}

// tg: code/game/objects/items/cardboard_cutouts.dm:281
public partial class datum_cardboard_cutout_wizard : datum_cardboard_cutout
{
    // tg: code/game/objects/items/cardboard_cutouts.dm:287
    public override object get_name() => Dm.text("[] []", pick(GLOB.wizard_first), pick(GLOB.wizard_second));
}

// tg: code/game/objects/items/cardboard_cutouts.dm:290
public partial class datum_cardboard_cutout_nightmare : datum_cardboard_cutout
{
    // tg: code/game/objects/items/cardboard_cutouts.dm:296
    public override object get_name() => pick(GLOB.nightmare_names);
}

// tg: code/game/objects/items/cardboard_cutouts.dm:299
public partial class datum_cardboard_cutout_xenomorph : datum_cardboard_cutout
{
    // tg: code/game/objects/items/cardboard_cutouts.dm:306
    public override object get_name() => applied_name + Dm.text(" ([])", rand(1, 999));
}

// tg: code/game/objects/items/cardboard_cutouts.dm:309
public partial class datum_cardboard_cutout_xenomorph_maid : datum_cardboard_cutout
{
    // tg: code/game/objects/items/cardboard_cutouts.dm:316
    public override object get_name() => applied_name + Dm.text(" ([])", rand(1, 999));
}

// tg: code/game/objects/items/cardboard_cutouts.dm:319
public partial class datum_cardboard_cutout_ash_walker : datum_cardboard_cutout
{
    // tg: code/game/objects/items/cardboard_cutouts.dm:326
    public override object get_name() => generate_random_name_species_based(species_type: typeof(datum_species_lizard));
}

// tg: code/game/objects/items/cardboard_cutouts.dm:329
public partial class datum_cardboard_cutout_death_squad : datum_cardboard_cutout
{
    // tg: code/game/objects/items/cardboard_cutouts.dm:335
    public override object get_name() => pick(GLOB.commando_names);
}
