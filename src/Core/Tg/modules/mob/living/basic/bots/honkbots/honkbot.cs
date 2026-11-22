// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/honkbots/honkbot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

// tg: code/modules/mob/living/basic/bots/honkbots/honkbot.dm:1
public partial class mob_living_basic_bot_secbot_honkbot : mob_living_basic_bot_secbot
{
    // tg: code/modules/mob/living/basic/bots/honkbots/honkbot.dm:21
    private static DmList honkbot_sounds => TgState.Current.mob_living_basic_bot_secbot_honkbot__honkbot_sounds ??= Dm.list(
        Dm.kv(HONKBOT_VOICED_HONK_HAPPY, Dm.resource("sound/items/bikehorn.ogg")),
        Dm.kv(HONKBOT_VOICED_HONK_SAD, Dm.resource("sound/misc/sadtrombone.ogg")));

    // tg: code/modules/mob/living/basic/bots/honkbots/honkbot.dm:29
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        datum_ai_controller controller = (datum_ai_controller)ai_controller;
        DmList clown_friends = TgState.Current.mob_living_basic_bot_secbot_honkbot__clown_friends ??= typecacheof(Dm.list(typeof(mob_living_carbon_human), typeof(mob_living_silicon_robot))) as DmList;
        controller.set_blackboard_key(BB_CLOWNS_LIST, clown_friends);
        DmList slippery_items = TgState.Current.mob_living_basic_bot_secbot_honkbot__slippery_items ??= typecacheof(Dm.list(typeof(obj_item_grown_bananapeel), typeof(obj_item_soap))) as DmList;
        controller.set_blackboard_key(BB_SLIPPERY_ITEMS, slippery_items);

        datum_action_cooldown_mob_cooldown_bot_honk bike_honk = Dm.@new<datum_action_cooldown_mob_cooldown_bot_honk>(this);
        Waiting.NoSleep(bike_honk.Grant(this));
        bike_honk.post_honk_callback = Dm.@new<datum_callback>(this, nameof(set_attacking_state));
        controller.set_blackboard_key(BB_HONK_ABILITY, bike_honk);

        Waiting.NoSleep(AddComponent(typeof(datum_component_slippery),
            Dm.named("knockdown", 6 * SECONDS), Dm.named("paralyze", 3 * SECONDS), Dm.named("on_slip_callback", Dm.@new<datum_callback>(this, nameof(post_slip))), Dm.named("can_slip_callback", Dm.@new<datum_callback>(this, nameof(pre_slip)))));
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/honkbots/honkbot.dm:54
    public override object generate_speak_list()
    {
        return honkbot_sounds;
    }

    // tg: code/modules/mob/living/basic/bots/honkbots/honkbot.dm:57
    public virtual object pre_slip()
    {
        return Dm.num(prob(70) && Dm.truthy((ai_controller as datum_ai_controller)?.blackboard_key_exists(BB_CURRENT_TARGET)));
    }

    // tg: code/modules/mob/living/basic/bots/honkbots/honkbot.dm:60
    public virtual void post_slip()
    {
        speak(HONKBOT_VOICED_HONK_SAD);
        set_attacking_state();
    }

    // tg: code/modules/mob/living/basic/bots/honkbots/honkbot.dm:64
    public virtual void set_attacking_state()
    {
        icon_state = Dm.text("[]-c", base_icon_state);
        _addtimer(Dm.@new<datum_callback>(this, nameof(update_appearance)), 0.2f * SECONDS, file: "code/modules/mob/living/basic/bots/honkbots/honkbot.dm", line: 66);
    }

    // tg: code/modules/mob/living/basic/bots/honkbots/honkbot.dm:68
    public override void post_arrest(mob_living_carbon current_target = null)
    {
        bool emagged = Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_EMAGGED));
        playsound(this, emagged ? SFX_HONKBOT_E : Dm.resource("sound/items/bikehorn.ogg"), 50, FALSE);
        icon_state = emagged ? Dm.text("[]-e", base_icon_state) : Dm.text("[]-c", base_icon_state);
        _addtimer(Dm.@new<datum_callback>(this, nameof(update_appearance)), 3 * SECONDS, Dm.bitor(TIMER_OVERRIDE, TIMER_UNIQUE), file: "code/modules/mob/living/basic/bots/honkbots/honkbot.dm", line: 71);

        audible_message(span_danger(Dm.text("[] gives out an evil laugh!", this)));
        playsound(this, Dm.resource("sound/mobs/non-humanoids/honkbot/honkbot_evil_laugh.ogg"), 75, TRUE, -1); // evil laughter
    }

    // tg: code/modules/mob/living/basic/bots/honkbots/honkbot.dm:76
    public override void retrieve_emag_message()
    {
        audible_message(span_danger(Dm.text("[] gives out an evil laugh!", this)));
        playsound(this, Dm.resource("sound/mobs/non-humanoids/honkbot/honkbot_evil_laugh.ogg"), 75, TRUE, -1); // evil laughter
    }

    // tg: code/modules/mob/living/basic/bots/honkbots/honkbot.dm:80
    public override async DmTask post_stun(mob_living_carbon current_target = null, float? harm = null)
    {
        if (current_target == null)
            return;

        await current_target.set_stutter(40 * SECONDS);
        await current_target.set_jitter_if_lower(100 * SECONDS);
        set_attacking_state();
        if (HAS_TRAIT(current_target, TRAIT_DEAF))
            return;

        await sound_damage(deafen: 10 * SECONDS);
    }

    // tg: code/modules/mob/living/basic/bots/honkbots/honkbot.dm:92
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = await base.ui_data(user) as DmList;
        if (!Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_LOCKED)) || HAS_SILICON_ACCESS(user))
        {
            DmList custom = data["custom_controls"] as DmList;
            custom["slip_people"] = Dm.bitand(security_mode_flags, HONKBOT_MODE_SLIP);
            custom["fake_cuff"] = Dm.bitand(security_mode_flags, SECBOT_HANDCUFF_TARGET);
            custom["check_ids"] = Dm.bitand(security_mode_flags, SECBOT_CHECK_IDS);
            custom["check_records"] = Dm.bitand(security_mode_flags, SECBOT_CHECK_RECORDS);
        }
        return data;
    }

    // tg: code/modules/mob/living/basic/bots/honkbots/honkbot.dm:101
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        mob user = ui.user;
        if (Dm.truthy(dot) || !isliving(user) || (Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_LOCKED)) && !HAS_SILICON_ACCESS(user)))
            return dot;
        switch (action as string)
        {
            case "slip_people":
                security_mode_flags = Dm.bitxor(security_mode_flags, HONKBOT_MODE_SLIP);
                break;
            case "fake_cuff":
                security_mode_flags = Dm.bitxor(security_mode_flags, SECBOT_HANDCUFF_TARGET);
                break;
            case "check_ids":
                security_mode_flags = Dm.bitxor(security_mode_flags, SECBOT_CHECK_IDS);
                break;
            case "check_records":
                security_mode_flags = Dm.bitxor(security_mode_flags, SECBOT_CHECK_RECORDS);
                break;
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/honkbots/honkbot.dm:116
    public override async DmTask retrieve_secbot_drops(atom drop_location = null)
    {
        obj_item_bot_assembly_honkbot honkbot_assembly = Dm.@new<obj_item_bot_assembly_honkbot>(drop_location);
        honkbot_assembly.build_step = ASSEMBLY_FIRST_STEP;
        honkbot_assembly.created_name = name;
        Dm.@new<obj_item_assembly_prox_sensor>(drop_location);
        await drop_part(baton_type, drop_location);
    }
}

// rules 10.6: the static vars above, made on first use
public sealed partial class TgState
{
    internal DmList mob_living_basic_bot_secbot_honkbot__honkbot_sounds;
    internal DmList mob_living_basic_bot_secbot_honkbot__clown_friends;
    internal DmList mob_living_basic_bot_secbot_honkbot__slippery_items;
}
