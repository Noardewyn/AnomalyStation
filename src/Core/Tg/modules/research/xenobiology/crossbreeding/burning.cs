// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/crossbreeding/burning.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the burning extracts, filled with 10u of plasma and squeezed in hand for an offensive effect of their colour.
// Kept as tg has it: the red one's loop over the slimes' friends types its loop var as a list, so it skips every friend and no
// slime forgets one. The var blocks are generated (D111).

/*
Burning extracts:
	Have a unique, primarily offensive effect when
	filled with 10u plasma and activated in-hand.
*/
// tg: code/modules/research/xenobiology/crossbreeding/burning.dm:7
public partial class obj_item_slimecross_burning : obj_item_slimecross
{
    // tg: code/modules/research/xenobiology/crossbreeding/burning.dm:12
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(10, Dm.bitor(INJECTABLE, DRAWABLE));
        return dot;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/burning.dm:16
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        datum_reagents holder = (datum_reagents)reagents;
        if (!Dm.truthy(holder.has_reagent(typeof(datum_reagent_toxin_plasma), 10)))
        {
            to_chat(user, span_warning(I18n.T("This extract needs to be full of plasma to activate!")));
            return null;
        }
        await holder.remove_reagent(typeof(datum_reagent_toxin_plasma), 10);
        to_chat(user, span_notice(I18n.T("You squeeze the extract, and it absorbs the plasma!")));
        playsound(this, "sound/effects/bubbles/bubbles.ogg", 50, TRUE);
        playsound(this, "sound/effects/magic/fireball.ogg", 50, TRUE);
        await do_effect(user);
        return null;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/burning.dm:26
    public virtual async DmTask do_effect(mob user = null) //If, for whatever reason, you don't want to delete the extract, don't do ..()
    {
        await DmTask.CompletedTask;
        qdel(this);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/burning.dm:31
public partial class obj_item_slimecross_burning_grey : obj_item_slimecross_burning
{
    // tg: code/modules/research/xenobiology/crossbreeding/burning.dm:34
    public override async DmTask do_effect(mob user = null)
    {
        mob_living_basic_slime new_slime = Dm.@new<mob_living_basic_slime>(get_turf(user), typeof(datum_slime_type_grey));
        new_slime.visible_message(span_danger(I18n.T("A baby slime emerges from [], and it nuzzles [] before burbling hungrily!", this, user)));
        await new_slime.befriend((mob_living)user); //Gas, gas, gas
        new_slime.bodytemperature = T0C + 400; //We gonna step on the gas.
        await new_slime.set_nutrition(SLIME_HUNGER_NUTRITION); //Tonight, we fight!
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/burning.dm:43
public partial class obj_item_slimecross_burning_orange : obj_item_slimecross_burning
{
    // tg: code/modules/research/xenobiology/crossbreeding/burning.dm:46
    public override async DmTask do_effect(mob user = null)
    {
        user.visible_message(span_danger(I18n.T("[] boils over with a caustic gas!", this)));
        await do_chem_smoke(7, user, get_turf(user), typeof(datum_reagent_consumable_condensedcapsaicin), 100, log: TRUE);
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/burning.dm:52
public partial class obj_item_slimecross_burning_purple : obj_item_slimecross_burning
{
    // tg: code/modules/research/xenobiology/crossbreeding/burning.dm:55
    public override async DmTask do_effect(mob user = null)
    {
        user.visible_message(span_notice(I18n.T("[] fills with a bubbling liquid!", this)));
        Dm.@new<obj_item_slimecrossbeaker_autoinjector_slimestimulant>(get_turf(user));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/burning.dm:61
public partial class obj_item_slimecross_burning_blue : obj_item_slimecross_burning
{
    // tg: code/modules/research/xenobiology/crossbreeding/burning.dm:64
    public override async DmTask do_effect(mob user = null)
    {
        user.visible_message(span_danger(I18n.T("[] flash-freezes the area!", this)));
        foreach (turf_open T in Dm.each<turf_open>(range(3, get_turf(user))))
            await T.MakeSlippery(TURF_WET_PERMAFROST, min_wet_time: 10, wet_time_to_add: 5);
        foreach (mob_living_carbon M in Dm.each<mob_living_carbon>(range(5, get_turf(user))))
        {
            if (M != user)
            {
                M.bodytemperature = BODYTEMP_COLD_DAMAGE_LIMIT + 10; //Not quite cold enough to hurt.
                to_chat(M, span_danger(I18n.T("You feel a chill run down your spine, and the floor feels a bit slippery with frost...")));
            }
        }
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/burning.dm:75
public partial class obj_item_slimecross_burning_metal : obj_item_slimecross_burning
{
    // tg: code/modules/research/xenobiology/crossbreeding/burning.dm:78
    public override async DmTask do_effect(mob user = null)
    {
        foreach (turf_closed_wall W in Dm.each<turf_closed_wall>(range(1, get_turf(user))))
        {
            await W.dismantle_wall(1);
            playsound(W, "sound/effects/break_stone.ogg", 50, TRUE);
        }
        user.visible_message(span_danger(I18n.T("[] pulses violently, and shatters the walls around it!", this)));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/burning.dm:86
public partial class obj_item_slimecross_burning_yellow : obj_item_slimecross_burning
{
    // tg: code/modules/research/xenobiology/crossbreeding/burning.dm:89
    public override async DmTask do_effect(mob user = null)
    {
        user.visible_message(span_danger(I18n.T("[] explodes into an electrical field!", this)));
        playsound(get_turf(this), "sound/items/weapons/zapbang.ogg", 50, TRUE);
        foreach (mob_living M in Dm.each<mob_living>(range(4, get_turf(user))))
        {
            if (M != user)
            {
                mob_living_carbon C = M as mob_living_carbon;
                if (C is mob_living_carbon)
                    await C.electrocute_act(25, this);
                else
                    await M.adjust_fire_loss(25);
                to_chat(M, span_danger(I18n.T("You feel a sharp electrical pulse!")));
            }
        }
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/burning.dm:103
public partial class obj_item_slimecross_burning_darkpurple : obj_item_slimecross_burning
{
    // tg: code/modules/research/xenobiology/crossbreeding/burning.dm:106
    public override async DmTask do_effect(mob user = null)
    {
        user.visible_message(span_danger(I18n.T("[] sublimates into a cloud of plasma!", this)));
        turf T = get_turf(user);
        T.atmos_spawn_air(Dm.text("[]=60", GAS_PLASMA));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/burning.dm:113
public partial class obj_item_slimecross_burning_darkblue : obj_item_slimecross_burning
{
    // tg: code/modules/research/xenobiology/crossbreeding/burning.dm:116
    public override async DmTask do_effect(mob user = null)
    {
        user.visible_message(span_danger(I18n.T("[] releases a burst of chilling smoke!", this)));
        await ((datum_reagents)user.reagents).add_reagent(typeof(datum_reagent_medicine_regen_jelly), 10);
        await do_chem_smoke(7, user, get_turf(user), typeof(datum_reagent_consumable_frostoil), 40, log: TRUE);
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/burning.dm:123
public partial class obj_item_slimecross_burning_silver : obj_item_slimecross_burning
{
    // tg: code/modules/research/xenobiology/crossbreeding/burning.dm:126
    public override async DmTask do_effect(mob user = null)
    {
        float amount = rand(3, 6);
        DmList turfs = Dm.list();
        foreach (turf_open T in Dm.each<turf_open>(range(1, get_turf(user))))
            Dm.append(ref turfs, T);
        for (int i = 1; i <= amount; i++)
        {
            object path = get_random_food();
            obj_item_food food = Dm.@new((System.Type)path, pick(turfs)) as obj_item_food;
            await ((datum_reagents)food.reagents).add_reagent(typeof(datum_reagent_toxin_slimejelly), 5); //Oh god it burns
            ADD_TRAIT(food, TRAIT_FOOD_SILVER, INNATE_TRAIT);
            if (prob(50))
                food.desc += I18n.T(" It smells strange...");
        }
        user.visible_message(span_danger(I18n.T("[] produces a few pieces of food!", this)));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/burning.dm:142
public partial class obj_item_slimecross_burning_bluespace : obj_item_slimecross_burning
{
    // tg: code/modules/research/xenobiology/crossbreeding/burning.dm:145
    public override async DmTask do_effect(mob user = null)
    {
        user.visible_message(span_danger(I18n.T("[] sparks, and lets off a shockwave of bluespace energy!", this)));
        foreach (mob_living L in Dm.each<mob_living>(range(1, get_turf(user))))
        {
            if (L != user)
            {
                await do_teleport(L, get_turf(L), 6, asoundin: "sound/effects/phasein.ogg", channel: TELEPORT_CHANNEL_BLUESPACE); //Somewhere between the effectiveness of fake and real BS crystal
                Dm.@new<obj_effect_particle_effect_sparks>(get_turf(L));
                playsound(get_turf(L), SFX_SPARKS, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
            }
        }
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/burning.dm:155
public partial class obj_item_slimecross_burning_sepia : obj_item_slimecross_burning
{
    // tg: code/modules/research/xenobiology/crossbreeding/burning.dm:158
    public override async DmTask do_effect(mob user = null)
    {
        user.visible_message(span_notice(I18n.T("[] shapes itself into a camera!", this)));
        Dm.@new<obj_item_camera_rewind>(get_turf(user));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/burning.dm:164
public partial class obj_item_slimecross_burning_cerulean : obj_item_slimecross_burning
{
    // tg: code/modules/research/xenobiology/crossbreeding/burning.dm:167
    public override async DmTask do_effect(mob user = null)
    {
        user.visible_message(span_notice(I18n.T("[] produces a potion!", this)));
        Dm.@new<obj_item_slimepotion_extract_cloner>(get_turf(user));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/burning.dm:173
public partial class obj_item_slimecross_burning_pyrite : obj_item_slimecross_burning
{
    // tg: code/modules/research/xenobiology/crossbreeding/burning.dm:176
    public override async DmTask do_effect(mob user = null)
    {
        area user_area = get_area(user) as area;
        if (user_area.apc == null)
        {
            user.visible_message(span_danger(I18n.T("[] releases a colorful wave of energy, but nothing seems to happen.", this)));
            return;
        }

        await user_area.apc.break_lights();
        user.visible_message(span_danger(I18n.T("[] releases a colorful wave of energy, which shatters the lights!", this)));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/burning.dm:187
public partial class obj_item_slimecross_burning_red : obj_item_slimecross_burning
{
    // tg: code/modules/research/xenobiology/crossbreeding/burning.dm:190
    public override async DmTask do_effect(mob user = null)
    {
        user.visible_message(span_danger(I18n.T("[] pulses a hazy red aura for a moment, which wraps around []!", this, user)));
        foreach (mob_living_basic_slime slime_in_view in Dm.each<mob_living_basic_slime>(view(7, get_turf(user))))
        {
            // DV647: a slime that has no friends has no friends list; tg subtracts the user from that null, a runtime that stops the
            // extract on the first such slime. The port reads the missing list as an empty one.
            object friends_list = (slime_in_view.ai_controller as datum_ai_controller)?.blackboard[BB_FRIENDS_LIST];
            DmList friends = Dm.sub(friends_list ?? Dm.list(), user) as DmList;
            // tg types the loop var as a list, so the loop skips every friend (kept)
            foreach (DmList ex_friend in Dm.each<DmList>(friends))
                slime_in_view.unfriend((object)ex_friend as mob_living);
            slime_in_view.set_enraged_behaviour();
            slime_in_view.visible_message(span_danger(I18n.T("The [] is driven into a dangerous frenzy!", slime_in_view)));
        }
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/burning.dm:201
public partial class obj_item_slimecross_burning_green : obj_item_slimecross_burning
{
    // tg: code/modules/research/xenobiology/crossbreeding/burning.dm:204
    public override async DmTask do_effect(mob user = null)
    {
        mob_living L = user as mob_living;
        if (!(user is mob_living))
            return;
        obj_item held = L.get_active_held_item() as obj_item; //This should be itself, but just in case...
        await L.dropItemToGround(held);
        obj_item_melee_arm_blade_slime blade = Dm.@new<obj_item_melee_arm_blade_slime>(user);
        if (!Dm.truthy(await L.put_in_hands(blade)))
        {
            qdel(blade);
            user.visible_message(span_warning(I18n.T("[] melts onto []'s arm, boiling the flesh horribly!", this, user)));
        }
        else
            user.visible_message(span_danger(I18n.T("[] sublimates the flesh around []'s arm, transforming the bone into a gruesome blade!", this, user)));
        await user.emote("scream");
        await L.apply_damage(30, BURN, L.get_active_hand());
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/burning.dm:221
public partial class obj_item_slimecross_burning_pink : obj_item_slimecross_burning
{
    // tg: code/modules/research/xenobiology/crossbreeding/burning.dm:224
    public override async DmTask do_effect(mob user = null)
    {
        user.visible_message(span_notice(I18n.T("[] shrinks into a small, gel-filled pellet!", this)));
        Dm.@new<obj_item_slimecrossbeaker_pax>(get_turf(user));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/burning.dm:230
public partial class obj_item_slimecross_burning_gold : obj_item_slimecross_burning
{
    // tg: code/modules/research/xenobiology/crossbreeding/burning.dm:233
    public override async DmTask do_effect(mob user = null)
    {
        user.visible_message(span_danger(I18n.T("[] shudders violently, and summons an army for []!", this, user)));
        for (int i = 1; i <= 3; i++) //Less than gold normally does, since it's safer and faster.
        {
            mob_living spawned_mob = create_random_mob(get_turf(user), HOSTILE_SPAWN) as mob_living;
            spawned_mob.add_ally(user);
            if (prob(50))
            {
                float steps = rand(1, 3);
                for (int j = 1; j <= steps; j++)
                    await step(spawned_mob, Dm.num(pick(NORTH, SOUTH, EAST, WEST)));
            }
        }
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/burning.dm:244
public partial class obj_item_slimecross_burning_oil : obj_item_slimecross_burning
{
    // tg: code/modules/research/xenobiology/crossbreeding/burning.dm:247
    public override async DmTask do_effect(mob user = null)
    {
        await DmTask.CompletedTask;
        user.visible_message(span_warning(I18n.T("[] activates []. It's going to explode!", user, this)), span_danger(I18n.T("You activate []. It crackles in anticipation", this)));
        _addtimer(Dm.@new<datum_callback>(this, nameof(boom)), 5 * SECONDS, file: "code/modules/research/xenobiology/crossbreeding/burning.dm", line: 248);
    }

    /// Inflicts a blastwave upon every mob within a small radius.
    // tg: code/modules/research/xenobiology/crossbreeding/burning.dm:252
    public virtual void boom()
    {
        turf T = get_turf(this);
        playsound(T, "sound/effects/explosion/explosion2.ogg", 200, TRUE);
        foreach (mob_living target in Dm.each<mob_living>(range(2, T)))
        {
            Dm.@new<obj_effect_temp_visual_explosion>(get_turf(target));
            Dm.append(ref SSexplosions.med_mov_atom, target);
        }
        qdel(this);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/burning.dm:261
public partial class obj_item_slimecross_burning_black : obj_item_slimecross_burning
{
    // tg: code/modules/research/xenobiology/crossbreeding/burning.dm:264
    public override async DmTask do_effect(mob user = null)
    {
        if (!isliving(user))
            return;
        user.visible_message(span_danger(I18n.T("[] absorbs [], transforming [] into a slime!", this, user, user.p_them())));
        datum_action_cooldown_spell_shapeshift_slime_form transform = Dm.@new<datum_action_cooldown_spell_shapeshift_slime_form>((object)user.mind ?? user);
        transform.remove_on_restore = TRUE;
        await transform.Grant(user);
        await transform.Activate(user);
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/burning.dm:275
public partial class obj_item_slimecross_burning_lightpink : obj_item_slimecross_burning
{
    // tg: code/modules/research/xenobiology/crossbreeding/burning.dm:278
    public override async DmTask do_effect(mob user = null)
    {
        user.visible_message(span_danger(I18n.T("[] lets off a hypnotizing pink glow!", this)));
        foreach (mob_living_carbon C in Dm.each<mob_living_carbon>(view(7, get_turf(user))))
            await ((datum_reagents)C.reagents).add_reagent(typeof(datum_reagent_pax), 5);
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/burning.dm:285
public partial class obj_item_slimecross_burning_adamantine : obj_item_slimecross_burning
{
    // tg: code/modules/research/xenobiology/crossbreeding/burning.dm:288
    public override async DmTask do_effect(mob user = null)
    {
        user.visible_message(span_notice(I18n.T("[] crystallizes into a large shield!", this)));
        Dm.@new<obj_item_shield_adamantineshield>(get_turf(user));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/burning.dm:294
public partial class obj_item_slimecross_burning_rainbow : obj_item_slimecross_burning
{
    // tg: code/modules/research/xenobiology/crossbreeding/burning.dm:297
    public override async DmTask do_effect(mob user = null)
    {
        user.visible_message(span_notice(I18n.T("[] flattens into a glowing rainbow blade.", this)));
        Dm.@new<obj_item_knife_rainbowknife>(get_turf(user));
        await base.do_effect(user);
    }
}
