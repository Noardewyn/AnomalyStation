// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/hygienebot/hygienebot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the foam of its explosion (the fluid spread, section 109) (gaps package C3).

// tg: code/modules/mob/living/basic/bots/hygienebot/hygienebot.dm:3
public partial class mob_living_basic_bot_hygienebot : mob_living_basic_bot
{
    private const float WASH_PERIOD = 3 * SECONDS;

    // tg: code/modules/mob/living/basic/bots/hygienebot/hygienebot.dm:30
    private static mutable_appearance water_overlay => TgState.Current.mob_living_basic_bot_hygienebot__water_overlay ??= mutable_appearance(Dm.resource("icons/mob/silicon/aibots.dmi"), "hygienebot-water") as mutable_appearance;
    // tg: code/modules/mob/living/basic/bots/hygienebot/hygienebot.dm:32
    private static mutable_appearance fire_overlay => TgState.Current.mob_living_basic_bot_hygienebot__fire_overlay ??= mutable_appearance(Dm.resource("icons/mob/silicon/aibots.dmi"), "hygienebot-fire") as mutable_appearance;
    // tg: code/modules/mob/living/basic/bots/hygienebot/hygienebot.dm:34
    private static DmList found_announcements => TgState.Current.mob_living_basic_bot_hygienebot__found_announcements ??= Dm.list(Dm.kv(HYGIENEBOT_VOICED_UNHYGIENIC, Dm.resource("sound/mobs/non-humanoids/hygienebot/unhygienicclient.ogg")));
    // tg: code/modules/mob/living/basic/bots/hygienebot/hygienebot.dm:38
    private static DmList threat_announcements => TgState.Current.mob_living_basic_bot_hygienebot__threat_announcements ??= Dm.list(
        Dm.kv(HYGIENEBOT_VOICED_THREAT_AIRLOCK, Dm.resource("sound/mobs/non-humanoids/hygienebot/dragyouout.ogg")),
        Dm.kv(HYGIENEBOT_VOICED_FOUL_SMELL, Dm.resource("sound/mobs/non-humanoids/hygienebot/foulsmelling.ogg")),
        Dm.kv(HYGIENEBOT_VOICED_TROGLODYTE, Dm.resource("sound/mobs/non-humanoids/hygienebot/troglodyte.ogg")),
        Dm.kv(HYGIENEBOT_VOICED_GREEN_CLOUD, Dm.resource("sound/mobs/non-humanoids/hygienebot/greencloud.ogg")),
        Dm.kv(HYGIENEBOT_VOICED_ARSEHOLE, Dm.resource("sound/mobs/non-humanoids/hygienebot/letmeclean.ogg")),
        Dm.kv(HYGIENEBOT_VOICED_THREAT_ARTERIES, Dm.resource("sound/mobs/non-humanoids/hygienebot/cutarteries.ogg")),
        Dm.kv(HYGIENEBOT_VOICED_STOP_RUNNING, Dm.resource("sound/mobs/non-humanoids/hygienebot/stoprunning.ogg")));
    // tg: code/modules/mob/living/basic/bots/hygienebot/hygienebot.dm:48
    private static DmList cleaned_announcements => TgState.Current.mob_living_basic_bot_hygienebot__cleaned_announcements ??= Dm.list(
        Dm.kv(HYGIENEBOT_VOICED_FUCKING_FINALLY, Dm.resource("sound/mobs/non-humanoids/hygienebot/finally.ogg")),
        Dm.kv(HYGIENEBOT_VOICED_THANK_GOD, Dm.resource("sound/mobs/non-humanoids/hygienebot/thankgod.ogg")),
        Dm.kv(HYGIENEBOT_VOICED_DEGENERATE, Dm.resource("sound/mobs/non-humanoids/hygienebot/degenerate.ogg")));

    // tg: code/modules/mob/living/basic/bots/hygienebot/hygienebot.dm:54
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance(UPDATE_ICON);

        generate_ai_speech();

        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), TgState.Current.mob_living_basic_bot_hygienebot__loc_connections ??= Dm.list(Dm.kv(COMSIG_ATOM_ENTERED, nameof(on_entered)))));
        Waiting.NoSleep(AddElement(typeof(datum_element_hat_wearer), TgState.Current.mob_living_basic_bot_hygienebot__hat_offsets ??= Dm.list(1, 1)));

        ADD_TRAIT(this, TRAIT_SPRAY_PAINTABLE, INNATE_TRAIT);
        RegisterSignal(this, COMSIG_HOSTILE_PRE_ATTACKINGTARGET, nameof(on_attack));
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/hygienebot/hygienebot.dm:70
    public override async DmTask explode()
    {
        await do_foam(2, this, loc as turf); // gaps F1
        await base.explode();
    }

    // tg: code/modules/mob/living/basic/bots/hygienebot/hygienebot.dm:74
    public override object generate_speak_list()
    {
        return TgState.Current.mob_living_basic_bot_hygienebot__finalized_speak_list ??= Dm.add(Dm.add(found_announcements, threat_announcements), cleaned_announcements) as DmList;
    }

    // tg: code/modules/mob/living/basic/bots/hygienebot/hygienebot.dm:78
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(Dm.bitand(bot_mode_flags, BOT_MODE_ON)) ? "-on" : "");
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/hygienebot/hygienebot.dm:83
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (Dm.truthy(Dm.bitand(bot_mode_flags, BOT_MODE_ON)))
            Dm.append(ref dot, mutable_appearance(icon, "hygienebot-flame"));

        if (!Dm.truthy(washing))
            return dot;

        Dm.append(ref dot, Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_EMAGGED)) ? fire_overlay : water_overlay);
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/hygienebot/hygienebot.dm:93
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable movable = null)
    {
        if (!Dm.truthy(washing))
            return;
        Waiting.NoSleep(commence_wash(movable));
    }

    // tg: code/modules/mob/living/basic/bots/hygienebot/hygienebot.dm:99
    [SignalHandler]
    public virtual object on_attack(datum source = null, atom target = null)
    {
        if (Dm.truthy(washing))
            return COMPONENT_HOSTILE_NO_ATTACK;
        set_washing_mode(new_mode: TRUE);
        foreach (atom to_wash in Dm.each<atom>(loc))
            Waiting.NoSleep(commence_wash(to_wash));
        _addtimer(Dm.@new<datum_callback>(this, nameof(set_washing_mode), FALSE), WASH_PERIOD, file: "code/modules/mob/living/basic/bots/hygienebot/hygienebot.dm", line: 107);
        return COMPONENT_HOSTILE_NO_ATTACK;
    }

    // tg: code/modules/mob/living/basic/bots/hygienebot/hygienebot.dm:109
    public virtual void set_washing_mode(object new_mode = null)
    {
        washing = Dm.num(new_mode);
        update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/modules/mob/living/basic/bots/hygienebot/hygienebot.dm:113
    public virtual async DmTask commence_wash(atom target = null)
    {
        if (Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_EMAGGED)))
        {
            await target.fire_act();
            return;
        }
        await target.wash(CLEAN_WASH);
        await target.extinguish();
    }

    // tg: code/modules/mob/living/basic/bots/hygienebot/hygienebot.dm:120
    public override async DmTask on_bot_movement(atom_movable source = null, atom oldloc = null, object dir = null, object forced = null)
    {
        if (!Dm.truthy(washing) || !isturf(loc))
            return;

        foreach (mob_living_carbon human in Dm.each<mob_living_carbon>(loc))
            await commence_wash(human);
    }

    // tg: code/modules/mob/living/basic/bots/hygienebot/hygienebot.dm:128
    public virtual void generate_ai_speech()
    {
        datum_ai_controller controller = (datum_ai_controller)ai_controller;
        controller.set_blackboard_key(BB_WASH_FOUND, found_announcements);
        controller.set_blackboard_key(BB_WASH_THREATS, threat_announcements);
        controller.set_blackboard_key(BB_WASH_DONE, cleaned_announcements);
    }
}

// rules 10.6: the static vars above, made on first use
public sealed partial class TgState
{
    internal mutable_appearance mob_living_basic_bot_hygienebot__water_overlay;
    internal mutable_appearance mob_living_basic_bot_hygienebot__fire_overlay;
    internal DmList mob_living_basic_bot_hygienebot__found_announcements;
    internal DmList mob_living_basic_bot_hygienebot__threat_announcements;
    internal DmList mob_living_basic_bot_hygienebot__cleaned_announcements;
    internal DmList mob_living_basic_bot_hygienebot__loc_connections;
    internal DmList mob_living_basic_bot_hygienebot__hat_offsets;
    internal DmList mob_living_basic_bot_hygienebot__finalized_speak_list;
}
