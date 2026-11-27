// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/cytology/vatbeast.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: cytology's vatbeast, its tentacle slap and its AI (the compiled behaviour tree, DV251). The var
// blocks are generated (D111).

/// A mob that slaps people around and can be tamed as a mount
// tg: code/modules/mob/living/basic/cytology/vatbeast.dm:2
public partial class mob_living_basic_vatbeast : mob_living_basic
{
    /// What can you feed a vatbeast to tame it?
    // tg: code/modules/mob/living/basic/cytology/vatbeast.dm:32  var/static/list/enjoyed_food (rules 10.6)
    public static ref DmList enjoyed_food => ref TgState.Current.mob_living_basic_vatbeast__enjoyed_food;

    // tg: code/modules/mob/living/basic/cytology/vatbeast.dm:39
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        add_cell_sample();
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_ai_target_timer)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_tameable), enjoyed_food, 30, 0));

        datum_action_cooldown_tentacle_slap slapper = Dm.@new<datum_action_cooldown_tentacle_slap>(this);
        Waiting.NoSleep(slapper.Grant(this));

        datum_ai_controller controller = ai_controller as datum_ai_controller;
        controller.set_blackboard_key(BB_GENERIC_ACTION, slapper);
        controller.set_blackboard_key(BB_BASIC_FOODS, typecacheof(enjoyed_food));
        return dot;
    }

    // tg: code/modules/mob/living/basic/cytology/vatbeast.dm:53
    public override async DmTask tamed(mob_living tamer = null, obj_item food = null, params object[] __extra)
    {
        await base.tamed(tamer, food, __extra);
        buckle_lying = 0;
        await AddElement(typeof(datum_element_ridable), typeof(datum_component_riding_creature_vatbeast));
        set_faction(Dm.list(FACTION_NEUTRAL));
    }

    // tg: code/modules/mob/living/basic/cytology/vatbeast.dm:59
    public virtual void add_cell_sample()
    {
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_VATBEAST, CELL_VIRUS_TABLE_GENERIC_MOB, 1, 5));
    }

    // tg: code/modules/mob/living/basic/cytology/vatbeast.dm:62
    public override object get_bloodtype()
    {
        return get_blood_type(typeof(datum_blood_type_lizard)); // Green and alien
    }
}

/// Ability that allows the owner to slap other mobs a short distance away.
/// For vatbeats, this ability is shared with the rider.
// tg: code/modules/mob/living/basic/cytology/vatbeast.dm:76
public partial class datum_action_cooldown_tentacle_slap : datum_action_cooldown
{
    // tg: code/modules/mob/living/basic/cytology/vatbeast.dm:88
    public override void update_button_name(atom_movable_screen_movable_action_button button = null, float? force = null, params object[] __extra)
    {
        if (button.our_hud?.mymob != owner)
        {
            // For buttons given to mobs which are not our owner, give it this alt name
            button.name = I18n.T("Command Tentacle Slap");
            button.desc = I18n.T("Command your steed to slap a creature with its tentacles.");
            return;
        }

        base.update_button_name(button, force, __extra);
    }

    // tg: code/modules/mob/living/basic/cytology/vatbeast.dm:97
    public override async DmTask<object> set_click_ability(mob on_who = null)
    {
        object dot = await base.set_click_ability(on_who);
        if (!Dm.truthy(dot))
            return dot;
        to_chat(on_who, span_notice(I18n.T("You prepare your []pimp-tentacle. <b>Left-click to slap a target!</b>", on_who == owner ? "" : I18n.T("steed's "))));
        return dot;
    }

    // tg: code/modules/mob/living/basic/cytology/vatbeast.dm:103
    public override async DmTask<object> unset_click_ability(mob on_who = null, float? refund_cooldown = null)
    {
        refund_cooldown ??= TRUE;
        object dot = await base.unset_click_ability(on_who, refund_cooldown);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.truthy(refund_cooldown))
            to_chat(on_who, span_notice(I18n.T("You stop preparing your []pimp-tentacle.", on_who == owner ? "" : I18n.T("steed's "))));
        return dot;
    }

    // tg: code/modules/mob/living/basic/cytology/vatbeast.dm:110
    public override async DmTask<object> InterceptClickOn(mob_living clicker = null, object @params = null, atom target = null)
    {
        // Check if we can slap
        if (!isliving(target) || target == owner)
            return FALSE;

        if (!Dm.truthy(await owner.Adjacent(target)))
        {
            owner.balloon_alert(clicker, I18n.T("too far!"));
            return FALSE;
        }

        // Do the slap
        object dot = await base.InterceptClickOn(clicker, @params, target);
        if (!Dm.truthy(dot))
            return FALSE;

        // Give feedback from the slap.
        // Additional feedback for if a rider did it
        if (clicker != owner)
            to_chat(clicker, span_notice(I18n.T("You command [] to slap [] with its tentacles.", owner, target)));

        return TRUE;
    }

    // tg: code/modules/mob/living/basic/cytology/vatbeast.dm:131
    public override async DmTask<object> Activate(atom to_slap = null)
    {
        mob_living living_to_slap = to_slap as mob_living;

        owner.visible_message(
            span_warning(I18n.T("[] slaps [] with its tentacle!", owner, to_slap)),
            span_notice(I18n.T("You slap [] with your tentacle.", to_slap))
        );
        playsound(owner, "sound/effects/emotes/assslap.ogg", 90);
        atom throw_target = get_edge_target_turf(to_slap, owner.dir) as atom;
        await living_to_slap.throw_at(throw_target, 6, 4, owner);
        await living_to_slap.apply_damage(30, BRUTE);

        StartCooldown();
        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/cytology/vatbeast.dm:32  /mob/living/basic/vatbeast's var/static/list/enjoyed_food
    internal DmList mob_living_basic_vatbeast__enjoyed_food = Dm.list(
        typeof(obj_item_food_carrotfries),
        typeof(obj_item_food_cheesyfries),
        typeof(obj_item_food_cornchips),
        typeof(obj_item_food_fries)
    );
}
