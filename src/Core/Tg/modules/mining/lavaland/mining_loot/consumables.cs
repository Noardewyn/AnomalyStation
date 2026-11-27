// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/lavaland/mining_loot/consumables.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the KA modkit design discs are var-only). The var blocks are generated.

// Wisp Lantern
// tg: code/modules/mining/lavaland/mining_loot/consumables.dm:25
public partial class obj_item_wisp_lantern : obj_item
{
    // tg: code/modules/mining/lavaland/mining_loot/consumables.dm:35
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);

        if (wisp == null)
        {
            to_chat(user, span_warning(I18n.T("The wisp has gone missing!")));
            icon_state = "lantern-blue";
            inhand_icon_state = "lantern-blue";
            return dot;
        }

        if (wisp.loc == this)
        {
            to_chat(user, span_notice(I18n.T("You release the wisp. It begins to bob around your head.")));
            icon_state = "lantern-blue";
            inhand_icon_state = "lantern-blue";
            await wisp.orbit(user, 20);
            SSblackbox?.record_feedback("tally", "wisp_lantern", 1, "Freed");
            return dot;
        }

        to_chat(user, span_notice(I18n.T("You return the wisp to the lantern.")));
        icon_state = "lantern-blue-on";
        inhand_icon_state = "lantern-blue-on";
        await wisp.forceMove(this);
        SSblackbox?.record_feedback("tally", "wisp_lantern", 1, "Returned");
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/consumables.dm:58
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        wisp = Dm.@new<obj_effect_wisp>(this);
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/consumables.dm:62
    public override object Destroy(float? force = null)
    {
        if (wisp != null)
        {
            if (wisp.loc == this)
                qdel(wisp);
            else
                wisp.visible_message(span_notice(I18n.T("[] has a sad feeling for a moment, then it passes.", wisp)));
        }
        return base.Destroy(force);
    }
}

// tg: code/modules/mining/lavaland/mining_loot/consumables.dm:70
public partial class obj_effect_wisp : obj_effect
{
    // tg: code/modules/mining/lavaland/mining_loot/consumables.dm:81 (var/static/list/color_cutoffs)
    public static DmList color_cutoffs => TgState.Current.obj_effect_wisp__color_cutoffs ??= Dm.list(10, 25, 25);

    // tg: code/modules/mining/lavaland/mining_loot/consumables.dm:84
    public override async DmTask<object> orbit(atom thing = null, float? radius = null, float? clockwise = null, float? rotation_speed = null, float? rotation_segments = null, float? pre_rotation = null, params object[] __extra)
    {
        object dot = await base.orbit(thing, radius, clockwise, rotation_speed, rotation_segments, pre_rotation, __extra);
        if (!ismob(thing))
            return dot;
        mob being = thing as mob;
        RegisterSignal(being, COMSIG_MOB_UPDATE_SIGHT, nameof(update_user_sight));
        to_chat(being, span_notice(I18n.T("The wisp enhances your vision.")));
        ADD_TRAIT(being, TRAIT_THERMAL_VISION, REF(this));
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/consumables.dm:93
    public override async DmTask stop_orbit(datum_component_orbiter orbits = null, float? refreshing = null)
    {
        refreshing ??= FALSE;
        if (!ismob(orbit_target) || Dm.truthy(refreshing))
        {
            await base.stop_orbit(orbits, refreshing);
            return;
        }
        mob being = orbit_target as mob;
        UnregisterSignal(being, COMSIG_MOB_UPDATE_SIGHT);
        to_chat(being, span_notice(I18n.T("Your vision returns to normal.")));
        REMOVE_TRAIT(being, TRAIT_THERMAL_VISION, REF(this));
        await base.stop_orbit(orbits, refreshing);
    }

    // tg: code/modules/mining/lavaland/mining_loot/consumables.dm:102
    [SignalHandler]
    public virtual void update_user_sight(mob user = null)
    {
        if (color_cutoffs != null)
            user.lighting_color_cutoffs = blend_cutoff_colors(user.lighting_color_cutoffs as DmList, color_cutoffs) as DmList;
    }
}

// Jacob's ladder

// tg: code/modules/mining/lavaland/mining_loot/consumables.dm:109
public partial class obj_item_jacobs_ladder : obj_item
{
    // tg: code/modules/mining/lavaland/mining_loot/consumables.dm:115
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        turf our_loc = get_turf(this);
        float ladder_x = our_loc.x;
        float ladder_y = our_loc.y;
        to_chat(user, span_notice(I18n.T("You unfold the ladder. It extends much farther than you were expecting.")));
        object last_ladder = null;
        for (float i = 1, i__end = world.maxz; i <= i__end; i++)
        {
            if (Dm.truthy(is_centcom_level(i)) || Dm.truthy(is_reserved_level(i)) || Dm.truthy(is_away_level(i)))
                continue;
            turf new_loc = locate(ladder_x, ladder_y, i);
            last_ladder = Dm.@new<obj_structure_ladder_unbreakable_jacob>(new_loc, null, last_ladder);
        }
        qdel(this);
        return null;
    }
}

// Book of Babel

// tg: code/modules/mining/lavaland/mining_loot/consumables.dm:135
public partial class obj_item_book_of_babel : obj_item
{
    // tg: code/modules/mining/lavaland/mining_loot/consumables.dm:142
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.is_blind()))
        {
            to_chat(user, span_warning(I18n.T("You are blind and can't read anything!")));
            return FALSE;
        }
        if (!Dm.truthy(user.can_read(this)))
            return FALSE;
        to_chat(user, span_notice(I18n.T("You flip through the pages of the book, quickly and conveniently learning every language in existence. Somewhat less conveniently, the aging book crumbles to dust in the process. Whoops.")));
        cure_curse_of_babel(user as mob_living_carbon); // removes tower of babel if we have it
        user.grant_all_languages(source: LANGUAGE_BABEL);
        user.remove_blocked_language(GLOB.all_languages, source: LANGUAGE_ALL);
        if (user.mind != null)
            ADD_TRAIT(user.mind, TRAIT_TOWER_OF_BABEL, MAGIC_TRAIT); // this makes you immune to babel effects
        Dm.@new<obj_effect_decal_cleanable_ash>(get_turf(user));
        qdel(this);
        await DmTask.CompletedTask;
        return null;
    }
}

// Potion of Flight

// tg: code/modules/mining/lavaland/mining_loot/consumables.dm:160
public partial class obj_item_reagent_containers_cup_bottle_potion : obj_item_reagent_containers_cup_bottle
{
    // tg: code/modules/mining/lavaland/mining_loot/consumables.dm:167
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy((reagents as datum_reagents)?.total_volume))
            Dm.append(ref dot, "potionflask_cap");
        return dot;
    }
}

// tg: code/modules/mining/lavaland/mining_loot/consumables.dm:177
public partial class datum_reagent_flightpotion : datum_reagent
{
    // tg: code/modules/mining/lavaland/mining_loot/consumables.dm:182
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (!ishuman(exposed_mob) || exposed_mob.stat == DEAD)
            return;
        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(INGEST, TOUCH))))
            return;
        mob_living_carbon_human exposed_human = exposed_mob as mob_living_carbon_human;
        obj_item_bodypart_chest chest = exposed_human.get_bodypart(BODY_ZONE_CHEST) as obj_item_bodypart_chest;
        if (!Dm.truthy(chest.wing_types) || Dm.num(reac_volume) < 5 || exposed_human.dna == null)
        {
            if (Dm.truthy(Dm.bitand(methods, INGEST)) && Dm.truthy(show_message))
                to_chat(exposed_human, span_notice(I18n.T("<i>You feel nothing but a terrible aftertaste.</i>")));
            return;
        }
        object had_wings = exposed_human.get_organ_slot(ORGAN_SLOT_EXTERNAL_WINGS);
        object wing_type = await get_wing_choice(exposed_human, chest);
        if (!Dm.truthy(wing_type))
            return;
        obj_item_organ_wings wings = Dm.@new((Type)wing_type) as obj_item_organ_wings;
        await wings.Insert(exposed_human);
        if (Dm.truthy(had_wings))
            to_chat(exposed_human, span_userdanger(I18n.T("A terrible pain travels down your back as your wings change shape!")));
        else
            to_chat(exposed_human, span_userdanger(I18n.T("A terrible pain travels down your back as wings burst out!")));
        playsound(exposed_human.loc, Dm.resource("sound/items/poster/poster_ripped.ogg"), 50, TRUE, -1);
        await exposed_human.apply_damage(20, def_zone: BODY_ZONE_CHEST, wound_bonus: CANT_WOUND);
        await exposed_human.emote("scream");
    }

    // tg: code/modules/mining/lavaland/mining_loot/consumables.dm:208
    public virtual async DmTask<object> get_wing_choice(mob_living_carbon_human needs_wings = null, obj_item_bodypart_chest chest = null)
    {
        DmList wing_types = chest.wing_types.Copy();
        if (wing_types.len == 1 || needs_wings.client == null)
            return wing_types[1];
        DmList radial_wings = Dm.list();
        DmList name2type = Dm.list();
        foreach (object possible_type in Dm.each(wing_types))
        {
            Type accessory_type = Dm.initial((Type)possible_type, (obj_item_organ p) => p.sprite_accessory_override);
            datum_sprite_accessory accessory = (SSaccessories.feature_list[FEATURE_WINGS] as DmList)[Dm.initial(accessory_type, (datum_sprite_accessory p) => p.name)] as datum_sprite_accessory; //get the singleton instance
            image img = image(icon: accessory.icon, icon_state: Dm.text("m_wingsopen_[]_BEHIND", accessory.icon_state)); //Process the HUD elements
            img.transform = Dm.mul(img.transform, 0.5f);
            img.pixel_w = -32;
            if (Dm.truthy(radial_wings[accessory.name]))
                stack_trace("Different wing types with repeated names. Please fix as this may cause issues.", "code/modules/mining/lavaland/mining_loot/consumables.dm", 220);
            else
            {
                radial_wings[accessory.name] = img;
                name2type[accessory.name] = possible_type;
            }
        }
        object wing_name = await show_radial_menu(needs_wings, needs_wings, radial_wings, tooltips: TRUE);
        object wing_type = name2type[wing_name];
        // If our chest gets replaced in the meanwile this can end up breaking, so we need to re-fetch it just to make sure
        obj_item_bodypart_chest new_chest = needs_wings.get_bodypart(BODY_ZONE_CHEST) as obj_item_bodypart_chest;
        if (new_chest != chest)
        {
            chest = new_chest;
            wing_type = null;
        }
        if (!Dm.truthy(wing_type))
        {
            if (!Dm.truthy(length(chest.wing_types)))
                return null;
            wing_type = pick(chest.wing_types);
        }
        return wing_type;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mining/lavaland/mining_loot/consumables.dm:81 (var/static/list/color_cutoffs)
    internal DmList obj_effect_wisp__color_cutoffs;
}
