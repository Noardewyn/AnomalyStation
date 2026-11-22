// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/firebot/firebot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the foam of a burning firebot (the fluid spread, section 109) (gaps package C3).

// tg: code/modules/mob/living/basic/bots/firebot/firebot.dm:3
public partial class mob_living_basic_bot_firebot : mob_living_basic_bot
{
    private const float FOAM_INTERVAL = 5 * SECONDS;

    // tg: code/modules/mob/living/basic/bots/firebot/firebot.dm:30
    public override object generate_speak_list()
    {
        DmList idle_lines = TgState.Current.mob_living_basic_bot_firebot__idle_lines ??= Dm.list(
            Dm.kv(FIREBOT_VOICED_NO_FIRES, Dm.resource("sound/mobs/non-humanoids/firebot/nofires.ogg")),
            Dm.kv(FIREBOT_VOICED_ONLY_YOU, Dm.resource("sound/mobs/non-humanoids/firebot/onlyyou.ogg")),
            Dm.kv(FIREBOT_VOICED_TEMPERATURE_NOMINAL, Dm.resource("sound/mobs/non-humanoids/firebot/tempnominal.ogg")),
            Dm.kv(FIREBOT_VOICED_KEEP_COOL, Dm.resource("sound/mobs/non-humanoids/firebot/keepitcool.ogg")));
        DmList fire_detected_lines = TgState.Current.mob_living_basic_bot_firebot__fire_detected_lines ??= Dm.list(
            Dm.kv(FIREBOT_VOICED_FIRE_DETECTED, Dm.resource("sound/mobs/non-humanoids/firebot/detected.ogg")),
            Dm.kv(FIREBOT_VOICED_STOP_DROP, Dm.resource("sound/mobs/non-humanoids/firebot/stopdropnroll.ogg")),
            Dm.kv(FIREBOT_VOICED_EXTINGUISHING, Dm.resource("sound/mobs/non-humanoids/firebot/extinguishing.ogg")));
        DmList emagged_lines = TgState.Current.mob_living_basic_bot_firebot__emagged_lines ??= Dm.list(
            Dm.kv(FIREBOT_VOICED_CANDLE_TIP, Dm.resource("sound/mobs/non-humanoids/firebot/candle_tip.ogg")),
            Dm.kv(FIREBOT_VOICED_ELECTRIC_FIRE, Dm.resource("sound/mobs/non-humanoids/firebot/electric_fire_tip.ogg")),
            Dm.kv(FIREBOT_VOICED_FUEL_TIP, Dm.resource("sound/mobs/non-humanoids/firebot/gasoline_tip.ogg")));
        datum_ai_controller controller = (datum_ai_controller)ai_controller;
        controller.set_blackboard_key(BB_FIREBOT_EMAGGED_LINES, emagged_lines);
        controller.set_blackboard_key(BB_FIREBOT_IDLE_LINES, idle_lines);
        controller.set_blackboard_key(BB_FIREBOT_FIRE_DETECTED_LINES, fire_detected_lines);
        return Dm.add(idle_lines, fire_detected_lines);
    }

    // tg: code/modules/mob/living/basic/bots/firebot/firebot.dm:52
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_SPACEWALK, INNATE_TRAIT);
        update_appearance(UPDATE_ICON);
        DmList things_to_extinguish = TgState.Current.mob_living_basic_bot_firebot__things_to_extinguish ??= typecacheof(Dm.list(typeof(mob_living_carbon))) as DmList;
        ((datum_ai_controller)ai_controller).set_blackboard_key(BB_FIREBOT_CAN_EXTINGUISH, things_to_extinguish);
        Waiting.NoSleep(create_extinguisher());
        Waiting.NoSleep(AddElement(typeof(datum_element_atmos_sensitive), mapload));
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/firebot/firebot.dm:61
    public override object Destroy(float? force = null)
    {
        qdel(internal_ext);
        internal_ext = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/bots/firebot/firebot.dm:65
    public override async DmTask bot_reset(float? bypass_ai_reset = null)
    {
        await base.bot_reset(bypass_ai_reset);
        await create_extinguisher();
    }

    // tg: code/modules/mob/living/basic/bots/firebot/firebot.dm:69
    public virtual async DmTask create_extinguisher()
    {
        internal_ext = await Dm.new_async<obj_item_extinguisher>(this);
        internal_ext.safety = FALSE;
        internal_ext.precision = TRUE;
        internal_ext.max_water = INFINITY;
        await internal_ext.refill();
    }

    // tg: code/modules/mob/living/basic/bots/firebot/firebot.dm:76
    public override async DmTask<object> melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        await use_extinguisher(target, modifiers);
        return null;
    }

    // tg: code/modules/mob/living/basic/bots/firebot/firebot.dm:79
    public override async DmTask<object> RangedAttack(atom A = null, object modifiers = null)
    {
        await use_extinguisher(A, modifiers as DmList);
        return null;
    }

    // tg: code/modules/mob/living/basic/bots/firebot/firebot.dm:82
    public virtual async DmTask use_extinguisher(atom attacked_atom = null, DmList modifiers = null)
    {
        if (!Dm.truthy(Dm.bitand(bot_mode_flags, BOT_MODE_ON)))
            return;
        await spray_water(attacked_atom, modifiers);
    }

    // tg: code/modules/mob/living/basic/bots/firebot/firebot.dm:87
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        await base.emag_act(user, emag_card, __extra);
        if (!Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_EMAGGED)))
            return null;

        to_chat(user, span_warning("You enable the very ironically named \"fighting with fire\" mode, and disable the targeting safeties.")); // heheehe. funny

        audible_message(span_danger(Dm.text("[] buzzes oddly!", this)));
        playsound(this, SFX_SPARKS, 75, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);

        internal_ext.chem = typeof(datum_reagent_clf3); //Refill the internal extinguisher with liquid fire
        internal_ext.power = 3;
        internal_ext.safety = FALSE;
        internal_ext.precision = FALSE;
        internal_ext.max_water = INFINITY;
        await internal_ext.refill();
        return TRUE;
    }

    // Variables sent to TGUI
    // tg: code/modules/mob/living/basic/bots/firebot/firebot.dm:106
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = await base.ui_data(user) as DmList;
        if (!Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_LOCKED)) || HAS_SILICON_ACCESS(user))
        {
            DmList custom = data["custom_controls"] as DmList;
            custom["extinguish_fires"] = Dm.bitand(firebot_mode_flags, FIREBOT_EXTINGUISH_FLAMES);
            custom["extinguish_people"] = Dm.bitand(firebot_mode_flags, FIREBOT_EXTINGUISH_PEOPLE);
            custom["stationary_mode"] = Dm.bitand(firebot_mode_flags, FIREBOT_STATIONARY_MODE);
        }
        return data;
    }

    // Actions received from TGUI
    // tg: code/modules/mob/living/basic/bots/firebot/firebot.dm:115
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        mob user = ui.user;
        if (Dm.truthy(dot) || (Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_LOCKED)) && !HAS_SILICON_ACCESS(user)))
            return dot;

        switch (action as string)
        {
            case "extinguish_fires":
                firebot_mode_flags = Dm.bitxor(firebot_mode_flags, FIREBOT_EXTINGUISH_FLAMES);
                break;
            case "extinguish_people":
                firebot_mode_flags = Dm.bitxor(firebot_mode_flags, FIREBOT_EXTINGUISH_PEOPLE);
                break;
            case "stationary_mode":
                firebot_mode_flags = Dm.bitxor(firebot_mode_flags, FIREBOT_STATIONARY_MODE);
                update_appearance();
                break;
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/firebot/firebot.dm:130
    public override object should_atmos_process(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        return Dm.num(Dm.num(exposed_temperature) > T0C + 200 || Dm.num(exposed_temperature) < BODYTEMP_COLD_DAMAGE_LIMIT);
    }

    // tg: code/modules/mob/living/basic/bots/firebot/firebot.dm:133
    public override async DmTask<object> atmos_expose(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        if (!(foam_cooldown <= world.time)) // COOLDOWN_FINISHED, inline
            return null;
        await do_foam(3, this, loc as turf, foam_type: typeof(datum_effect_system_fluid_spread_foam_firefighting)); // gaps F1
        foam_cooldown = world.time + FOAM_INTERVAL; // COOLDOWN_START, inline
        return null;
    }

    // tg: code/modules/mob/living/basic/bots/firebot/firebot.dm:139
    public virtual async DmTask spray_water(atom attacked_atom = null, DmList modifiers = null)
    {
        if (Dm.truthy(Dm.bitand(firebot_mode_flags, FIREBOT_STATIONARY_MODE)))
            flick("firebots_use", this);
        else
            flick("firebot1_use", this);
        if (internal_ext != null)
            await internal_ext.interact_with_atom(attacked_atom, this, modifiers);
    }

    // tg: code/modules/mob/living/basic/bots/firebot/firebot.dm:146
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (!Dm.truthy(Dm.bitand(bot_mode_flags, BOT_MODE_ON)))
        {
            icon_state = "firebot0";
            return dot;
        }
        if (Dm.truthy(IsStun()) || Dm.truthy(IsParalyzed()) || Dm.truthy(Dm.bitand(firebot_mode_flags, FIREBOT_STATIONARY_MODE))) //Bot has yellow light to indicate stationary mode.
        {
            icon_state = "firebots1";
            return dot;
        }
        icon_state = "firebot1";
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/firebot/firebot.dm:156
    public override async DmTask explode()
    {
        atom my_turf = drop_location() as atom;

        Dm.@new<obj_item_assembly_prox_sensor>(my_turf);
        Dm.@new<obj_item_clothing_head_utility_hardhat_red>(my_turf);

        if (my_turf is turf_open open_turf)
            await open_turf.MakeSlippery(TURF_WET_WATER, min_wet_time: 10 * SECONDS, wet_time_to_add: 5 * SECONDS);

        await base.explode();
    }
}

// rules 10.6: the static vars above, made on first use
public sealed partial class TgState
{
    internal DmList mob_living_basic_bot_firebot__idle_lines;
    internal DmList mob_living_basic_bot_firebot__fire_detected_lines;
    internal DmList mob_living_basic_bot_firebot__emagged_lines;
    internal DmList mob_living_basic_bot_firebot__things_to_extinguish;
}
