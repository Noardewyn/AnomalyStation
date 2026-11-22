// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/v8_engine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps OD2 (D333): the file whole. The ancient V8 engine is held in both hands, roars and shakes when used in hand, and its label
// teaches the House Edge's crafting recipe when examined closely. The House Edge counts its fire charges on wielded hits (its
// "house_edge1".."3" pictures), loses them when dropped, thrown or used in hand, and spends them on a right-click blazing slash: a
// flaming projectile, with a light blast when the second wind-up completes on three charges. The var blocks are generated (D111).

/**
 * File for the House Edge sword and the V8 Engine, obtained from the black market.
 */
// tg: code/game/objects/items/v8_engine.dm:12
public partial class obj_item_v8_engine : obj_item
{
    // tg: code/game/objects/items/v8_engine.dm:1  (file-local, #undef at :192)
    private const float ENGINE_COOLDOWN = 5 * SECONDS;

    // tg: code/game/objects/items/v8_engine.dm:24
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed), Dm.named("require_twohands", TRUE), Dm.named("force_unwielded", 5), Dm.named("force_wielded", 5)));
        return dot;
    }

    // tg: code/game/objects/items/v8_engine.dm:28
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (!(engine_sound_cooldown <= world.time)) // COOLDOWN_FINISHED
            return dot;
        playsound(this, Dm.resource("sound/items/car_engine_start.ogg"), vol: 75, vary: FALSE, extrarange: 3);
        Shake(duration: ENGINE_COOLDOWN);
        to_chat(user, span_notice(I18n.T("Darn thing... it's too old to keep on without retrofitting it! Without modifications, it works like it's junk.")));
        engine_sound_cooldown = world.time + ENGINE_COOLDOWN; // COOLDOWN_START
        return dot;
    }

    // tg: code/game/objects/items/v8_engine.dm:37
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);
        Waiting.Detach(start_learning_recipe(user)); // INVOKE_ASYNC(src, PROC_REF(start_learning_recipe), user)
        return dot;
    }

    // tg: code/game/objects/items/v8_engine.dm:41
    public virtual async DmTask start_learning_recipe(mob user = null)
    {
        if (!Dm.truthy(user.mind))
            return;
        if (Dm.truthy(user.mind.has_crafting_recipe(typeof(datum_crafting_recipe_house_edge))))
            return;
        to_chat(user, span_notice(I18n.T("You peer at the label on the side, reading about some unique modifications that could be made to the engine...")));
        if (Dm.truthy(await do_after(user, 15 * SECONDS, this)))
        {
            user.mind.teach_crafting_recipe(typeof(datum_crafting_recipe_house_edge));
            to_chat(user, span_notice(I18n.T("You learned how to make the House Edge.")));
        }
    }
}

// tg: code/game/objects/items/v8_engine.dm:51
public partial class obj_item_house_edge : obj_item
{
    // tg: code/game/objects/items/v8_engine.dm:2-6  (file-local, #undef at :193-197)
    private const float SLASH_COOLDOWN = 1.2f * SECONDS;
    private const float SLASH_WINDUP = 1 * SECONDS;
    private const int HOUSE_EDGE_ICONS_MAX = 3;
    private const int HOUSE_EDGE_ICONS_MIN = 0;

    // tg: code/game/objects/items/v8_engine.dm:78
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed), Dm.named("force_unwielded", 12), Dm.named("force_wielded", 22), Dm.named("attacksound", active_hitsound)));
        RegisterSignals(this, Dm.list(COMSIG_ITEM_DROPPED, COMSIG_MOVABLE_PRE_THROW, COMSIG_ITEM_ATTACK_SELF), nameof(reset_charges));
        return dot;
    }

    // tg: code/game/objects/items/v8_engine.dm:83
    public override DmTask afterattack(atom target = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (!ismob(target))
            return DmTask.CompletedTask;
        mob mob_target = target as mob;
        if (HAS_TRAIT(this, TRAIT_WIELDED) && !Dm.eq(mob_target.stat, DEAD))
        {
            //Add a fire charge to a max of 3, updates icon_state.
            fire_charges = clamp((fire_charges + 1), HOUSE_EDGE_ICONS_MIN, HOUSE_EDGE_ICONS_MAX);
            fire_charge_cooldown = 0; // COOLDOWN_RESET
        }
        update_appearance(UPDATE_ICON_STATE);
        return DmTask.CompletedTask;
    }

    // tg: code/game/objects/items/v8_engine.dm:93
    public override async DmTask<object> ranged_interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!(fire_charge_cooldown <= world.time)) // COOLDOWN_FINISHED
            return ITEM_INTERACT_BLOCKING;
        if (fire_charges <= 0)
        {
            balloon_alert(user, I18n.T("no fire charges!"));
            return ITEM_INTERACT_BLOCKING;
        }
        to_chat(user, span_boldnotice(I18n.T("You take aim at []...", interacting_with)));
        user.add_shared_particles(typeof(particles_bonfire));

        if (!Dm.truthy(await do_after(user, SLASH_WINDUP, target: this)))
        {
            // Special attack fizzles, no slash and charges lost.
            await reset_charges();
            drop_particles(user);
            return ITEM_INTERACT_BLOCKING;
        }

        user.add_shared_particles(typeof(particles_bonfire));
        Dm.@new<obj_effect_temp_visual_focus_ring>(get_turf(this));
        playsound(this, Dm.resource("sound/items/car_engine_start.ogg"), vol: 35, vary: FALSE, extrarange: 3);

        if (!Dm.truthy(await do_after(user, SLASH_WINDUP * 2, target: this)) || fire_charges < 3)
        {
            await flaming_slash(interacting_with, user, upgraded: FALSE);
            return ITEM_INTERACT_SUCCESS;
        }

        await flaming_slash(interacting_with, user, upgraded: TRUE); //Upgraded slash.
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/v8_engine.dm:119
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        inhand_icon_state = HAS_TRAIT(this, TRAIT_WIELDED) ? "house_edge1" : "house_edge";
        icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(fire_charges) ? fire_charges : "");
        return await base.update_icon_state(__extra);
    }

    /**
     * Proc that handles the house edge's fire_charge mechanic when resetting charges back to zero.
     * Updates icon, sets count, and updates icon.
     */
    // tg: code/game/objects/items/v8_engine.dm:128
    // PORT: on_slash is object, not the slot's float? (rules 3.5, a "-" cell; Appendix A): as the handler of the drop, throw and
    // use-in-hand signals it is handed the signal's source, the sword itself, which DM reads as a true on_slash.
    public virtual async DmTask reset_charges(object on_slash = null)
    {
        on_slash ??= FALSE;
        if (!(fire_charge_cooldown <= world.time) && !Dm.truthy(on_slash)) // COOLDOWN_FINISHED
            return;
        if (Dm.truthy(fire_charges))
            balloon_alert_to_viewers(I18n.T("charges lost!"));
        fire_charges = 0;
        await update_icon(Dm.bitor(UPDATE_OVERLAYS, UPDATE_ICON_STATE));
    }

    /// Kills any of the relevant particles off the wielder, as added during special attacks.
    // tg: code/game/objects/items/v8_engine.dm:137
    public virtual void drop_particles(mob_living user = null)
    {
        user.remove_shared_particles(typeof(particles_bonfire));
    }

    /**
     * Proc that creates and fires the flaming_slash projectile from the house edge special attack.
     * Extends off the ranged_interact_with_atom_secondary() behavior.
     * * upgraded: when juiced up from the second do_after, applies a light explosion radius to the slash.
     */
    // tg: code/game/objects/items/v8_engine.dm:145
    public virtual async DmTask flaming_slash(atom interacting_with = null, mob_living user = null, float? upgraded = null)
    {
        upgraded ??= FALSE;
        // Do the cool slash
        obj_projectile_flaming_slash projectile = Dm.@new<obj_projectile_flaming_slash>(get_turf(this));

        await projectile.aim_projectile(interacting_with, this);
        projectile.firer = user;
        projectile.damage = Dm.num(projectile.damage) + (10 * fire_charges);
        if (Dm.truthy(upgraded))
        {
            projectile.explosion_power = 1;
            projectile.damage = Dm.num(projectile.damage) / 2; // The damage is pretty solid normally, but with the explosion and all the RNG that comes with, it's nearly a 1-shot. This evens a playing field a bit.
        }
        await projectile.fire(null, interacting_with);

        user.visible_message(span_danger(I18n.T("[] makes a[] blazing slash at []!", user, Dm.truthy(upgraded) ? I18n.T(" devastating") : "", interacting_with)),
            span_notice(I18n.T("You take a blazing swipe at []!", interacting_with)));
        playsound(this, Dm.resource("sound/items/modsuit/flamethrower.ogg"), vol: 75, vary: FALSE, extrarange: 3);
        playsound(this, Dm.resource("sound/items/weapons/slash.ogg"), vol: 50, vary: FALSE, extrarange: 3);

        drop_particles(user);
        fire_charge_cooldown = world.time + SLASH_COOLDOWN; // COOLDOWN_START
        await reset_charges(on_slash: TRUE);
    }
}

/// Flaming slash for the special attack at max charges.
// tg: code/game/objects/items/v8_engine.dm:167
public partial class obj_projectile_flaming_slash : obj_projectile
{
    // tg: code/game/objects/items/v8_engine.dm:183
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        object dot = await base.on_hit(target, blocked, pierce_hit);
        await explosion(loc as atom,
            light_impact_range: explosion_power,
            flame_range: 3,
            explosion_cause: Dm.text("[] using \\the []", firer, fired_from),
            explosion_arc: 120
        );
        return dot;
    }
}
