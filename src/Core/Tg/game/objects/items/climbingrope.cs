// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/climbingrope.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J6 (D298): the file whole. Meta's station is one level, so the rope is reached on stacked levels (the tests build them). The
// athletics skill came with gaps SL1 (D333). The look-up binding of the examine reads the client's preferences.

// tg: code/game/objects/items/climbingrope.dm:2
public partial class obj_item_climbing_hook : obj_item
{
    // tg: code/game/objects/items/climbingrope.dm:21
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        // A runtime in tg: a mob with no client (a ghost role's body, a test's) reads user.client.prefs.
        DmList look_binds = user.client?.prefs?.key_bindings?["look up"] as DmList;
        Dm.append(ref dot, span_notice(I18n.T("Firstly, look upwards by holding <b>[]!</b>", english_list(look_binds, nothing_text: "(nothing bound)", and_text: " or ", comma_text: ", or "))));
        Dm.append(ref dot, span_notice(I18n.T("Then, click solid ground (or lattice/catwalk) adjacent to the hole above you.")));
        return dot;
    }

    // tg: code/game/objects/items/climbingrope.dm:27
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (HAS_TRAIT(interacting_with, TRAIT_COMBAT_MODE_SKIP_INTERACTION))
            return NONE;
        return await ranged_interact_with_atom(interacting_with, user, modifiers);
    }

    // tg: code/game/objects/items/climbingrope.dm:32
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        turf target = get_turf(interacting_with);
        if (Dm.eq(target.z, user.z))
            return NONE;
        if (!istype(target, typeof(turf_open)) || !isturf(user.loc)) //better safe than sorry
            return ITEM_INTERACT_BLOCKING;

        turf user_turf = get_turf(user);
        turf trans_vertical = (target.z > user.z ? GET_TURF_ABOVE(user_turf) : GET_TURF_ABOVE(target)) as turf;
        // A runtime in tg: climbing with no level above the lower of the two has no hole to read.
        if (trans_vertical == null || Dm.truthy(target_blocked(target, trans_vertical)))
        {
            balloon_alert(user, I18n.T("cant get there!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.num(get_dist(target, trans_vertical)) > reach - 1) //is our rope long enough?
        {
            balloon_alert(user, I18n.T("too far!"));
            return ITEM_INTERACT_BLOCKING;
        }

        object away_dir = get_dir(trans_vertical, target);
        string updown = target.z > user.z ? "up" : "down";
        user.visible_message(span_notice(I18n.T("[] begins climbing [] with [].", user, updown, this)), span_notice(I18n.T("You get to work on properly hooking [] and going [].", this, updown)));
        playsound(target, Dm.resource("sound/effects/pickaxe/picaxe1.ogg"), 50); //plays twice so people above and below can hear
        playsound(user_turf, Dm.resource("sound/effects/pickaxe/picaxe1.ogg"), 50);
        DmList effects = Dm.list(Dm.@new<obj_effect_temp_visual_climbing_hook>(target, away_dir), Dm.@new<obj_effect_temp_visual_climbing_hook>(user_turf, away_dir));

        // Our climbers athletics ability
        object fitness_level = user.mind?.get_skill_level(typeof(datum_skill_athletics));

        // Misc bonuses to the climb speed.
        float misc_multiplier = 1;

        obj_item_organ_cyberimp_chest_spine potential_spine = user.get_organ_slot(ORGAN_SLOT_SPINE) as obj_item_organ_cyberimp_chest_spine;
        if (potential_spine != null)
            misc_multiplier *= Dm.num(potential_spine.athletics_boost_multiplier);

        float final_climb_time = Dm.num(Dm.sub(climb_time, fitness_level)) * misc_multiplier;

        if (Dm.truthy(await do_after(user, final_climb_time, target)))
        {
            await user.forceMove(target);
            user.mind?.adjust_experience(typeof(datum_skill_athletics), round(Dm.num(Dm.div(ATHLETICS_SKILL_MISC_EXP, (fitness_level is var or__1 && Dm.truthy(or__1) ? or__1 : 1))), 1)); //get some experience for our trouble, especially since this costs us a climbing rope use
        }

        QDEL_LIST(effects);
        return ITEM_INTERACT_SUCCESS;
    }

    // didnt want to mess up is_blocked_turf_ignore_climbable
    /// checks if our target is blocked, also checks for border objects facing the above turf and climbable stuff
    // tg: code/game/objects/items/climbingrope.dm:75
    public virtual object target_blocked(turf target = null, turf trans_vertical = null)
    {
        if (Dm.truthy(target.density) || (isopenspaceturf(target) && Dm.truthy(target.zPassOut(DOWN))) || !Dm.truthy(trans_vertical.zPassOut(DOWN)) || Dm.truthy(trans_vertical.density)) // we check if we would fall down from it additionally
            return TRUE;

        foreach (atom_movable atom_content in Dm.each<atom_movable>(target.contents))
        {
            if (isliving(atom_content))
                continue;
            if (HAS_TRAIT(atom_content, TRAIT_CLIMBABLE))
                continue;
            if (Dm.truthy(Dm.bitand(atom_content.flags_1, ON_BORDER_1)) && !Dm.eq(atom_content.dir, get_dir(target, trans_vertical))) //if the border object is facing the hole then it is blocking us, likely
                continue;
            if (Dm.truthy(atom_content.density))
                return TRUE;
        }
        return FALSE;
    }
}

// tg: code/game/objects/items/climbingrope.dm:107
public partial class obj_effect_temp_visual_climbing_hook : obj_effect_temp_visual
{
    // tg: code/game/objects/items/climbingrope.dm:113
    [DmParams("mapload", "direction")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object direction = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload);
        dir = Dm.num(direction);
        return dot;
    }
}
