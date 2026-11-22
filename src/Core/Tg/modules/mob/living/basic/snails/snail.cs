// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/snails/snail.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

// tg: code/modules/mob/living/basic/snails/snail.dm:2
public partial class mob_living_basic_snail : mob_living_basic
{
    // tg: code/modules/mob/living/basic/snails/snail.dm:30
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), TgState.Current.mob_living_basic_snail__loc_connections));

        DmList eatable_food = TgState.Current.mob_living_basic_snail__eatable_food ??= Dm.list(typeof(obj_item_food_grown), typeof(obj_item_food_appleslice));
        DmList innate_actions = TgState.Current.mob_living_basic_snail__innate_actions ??= Dm.list(Dm.kv(typeof(datum_action_cooldown_mob_cooldown_shell_retreat), BB_SNAIL_RETREAT_ABILITY));
        grant_actions_by_list(innate_actions);

        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_BASIC_FOODS, typecacheof(eatable_food));
        Waiting.NoSleep(AddElement(typeof(datum_element_basic_eating), null, null, null, null, null, null, eatable_food));
        create_reagents(100, REAGENT_HOLDER_ALIVE);
        RegisterSignal(reagents as datum, COMSIG_REAGENTS_HOLDER_UPDATED, nameof(on_reagents_update));
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_SNAIL, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 5f));
        if (minion_path != null)
            Waiting.NoSleep(AddElement(typeof(datum_element_regal_rat_minion), minion_path, "gurgle", GLOB.regal_rat_minion_commands));
        if (Dm.truthy(should_be_holdable))
            Waiting.NoSleep(AddElement(typeof(datum_element_can_be_held)));
        return dot;
    }

    // tg: code/modules/mob/living/basic/snails/snail.dm:58
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable potential_salt = null)
    {
        if (potential_salt is obj_effect_decal_cleanable_food_salt)
            on_salt_exposure(); //immediately perish
    }

    // tg: code/modules/mob/living/basic/snails/snail.dm:63
    [SignalHandler]
    public virtual void on_reagents_update(datum source = null)
    {
        if (Dm.truthy((reagents as datum_reagents).has_reagent(typeof(datum_reagent_consumable_salt))))
            on_salt_exposure();
    }

    // tg: code/modules/mob/living/basic/snails/snail.dm:68
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (!isturf(loc))
            return dot;
        if (Dm.locate_type(typeof(obj_effect_decal_cleanable_food_salt), loc.contents) != null)
            on_salt_exposure();
        return dot;
    }

    // tg: code/modules/mob/living/basic/snails/snail.dm:75
    public virtual void on_salt_exposure()
    {
        if (stat == DEAD)
            return;
        visible_message(
            span_danger(Dm.text("[] shows a strong reaction after tasting some salt!", this)),
            span_userdanger("You show a strong reaction after tasting some salt.")
        );
        Waiting.NoSleep(apply_damage(500)); //ouch
    }

    // tg: code/modules/mob/living/basic/snails/snail.dm:84
    public override async DmTask mob_pickup(mob_living user = null)
    {
        obj_item_mob_holder_snail holder = Dm.@new<obj_item_mob_holder_snail>(get_turf(this), this, held_state, head_icon, held_lh, held_rh, worn_slot_flags);
        if (Dm.truthy(this._listen_lookup?[COMSIG_LIVING_SCOOPED_UP]))
            this._SendSignal(COMSIG_LIVING_SCOOPED_UP, Dm.list(this, user, holder));
        object display_message = Dm.text("[] []!", user, HAS_TRAIT(this, TRAIT_MOVE_FLOATING) ? Dm.text("scoops up []", this) : Dm.text("peels [] off the ground", this));
        user.visible_message(span_warning(Dm.text("[]", display_message)));
        await user.put_in_hands(holder);
    }

    // tg: code/modules/mob/living/basic/snails/snail.dm:93
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (stat != DEAD)
            icon_state = HAS_TRAIT(this, TRAIT_SHELL_RETREATED) ? "snail_shell" : Dm.text("[]", base_icon_state);
        return await base.update_icon_state(__extra);
    }
}

// tg: code/modules/mob/living/basic/snails/snail.dm:114
public partial class obj_item_mob_holder_snail : obj_item_mob_holder
{
    // tg: code/modules/mob/living/basic/snails/snail.dm:119
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (interacting_with is not obj_machinery_hydroponics)
            return NONE;

        object dot = ITEM_INTERACT_BLOCKING;
        if (held_mob.stat == DEAD)
        {
            user.balloon_alert(user, "it's dead!");
            return dot;
        }

        if (Dm.locate_type(GetType(), interacting_with) != null)
        {
            user.balloon_alert(user, "already has snail!");
            return dot;
        }

        if (!Dm.truthy(await do_after(user, 2 * SECONDS, interacting_with)))
            return dot;

        await forceMove(interacting_with);
        return ITEM_INTERACT_SUCCESS;
    }
}

public sealed partial class TgState
{
    internal DmList mob_living_basic_snail__eatable_food;
    internal DmList mob_living_basic_snail__innate_actions;
    // tg: code/modules/mob/living/basic/snails/snail.dm:32
    internal DmList mob_living_basic_snail__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(mob_living_basic_snail.on_entered)));
}
