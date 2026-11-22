// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/reverse_bear_trap.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4a, D316; the victim's red screen flash since gaps MO2 translated flash_color). The var block is
// generated (D111).

// tg: code/game/objects/items/devices/reverse_bear_trap.dm:3
public partial class obj_item_reverse_bear_trap : obj_item
{
    // tg: code/game/objects/items/devices/reverse_bear_trap.dm:1  #define REVERSE_BEAR_TRAP_COUNTDOWN (60 SECONDS)
    private const float REVERSE_BEAR_TRAP_COUNTDOWN = 60 * SECONDS;

    // tg: code/game/objects/items/devices/reverse_bear_trap.dm:31
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        soundloop = Dm.@new<datum_looping_sound_reverse_bear_trap>(this);
        soundloop2 = Dm.@new<datum_looping_sound_reverse_bear_trap_beep>(this);
        return dot;
    }

    // tg: code/game/objects/items/devices/reverse_bear_trap.dm:36
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref soundloop);
        QDEL_NULL(ref soundloop2);
        STOP_PROCESSING(SSprocessing, this);
        return base.Destroy(force);
    }

    // COOLDOWN_TIMELEFT(src, kill_countdown) (__DEFINES/cooldowns.dm:117), inline
    private float kill_countdown_timeleft() => max(0, kill_countdown - Dm.num(world.time));

    // tg: code/game/objects/items/devices/reverse_bear_trap.dm:42
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(ticking))
            return null;
        soundloop2.mid_length = max(0.5f, kill_countdown_timeleft() - 5); //beepbeepbeepbeepbeep
        if (kill_countdown <= Dm.num(world.time) || !isliving(loc)) // COOLDOWN_FINISHED
            trigger();
        return null;
    }

    // tg: code/game/objects/items/devices/reverse_bear_trap.dm:49
    public virtual void trigger()
    {
        playsound(this, Dm.resource("sound/machines/microwave/microwave-end.ogg"), 100, FALSE);
        soundloop.stop();
        soundloop2.stop();
        to_chat(loc, span_userdanger(I18n.T("*ding*")));
        _addtimer(Dm.@new<datum_callback>(this, nameof(snap)), 0.2f * SECONDS, file: "code/game/objects/items/devices/reverse_bear_trap.dm", line: 54);
        kill_countdown = 0; // COOLDOWN_RESET: reset the countdown in case it wasn't finished
    }

    // tg: code/game/objects/items/devices/reverse_bear_trap.dm:57
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (!iscarbon(user))
            return dot;
        mob_living_carbon carbon_user = user as mob_living_carbon;
        if (carbon_user.get_item_by_slot(ITEM_SLOT_HEAD) != this)
            return dot;
        if (!HAS_TRAIT_FROM(this, TRAIT_NODROP, REVERSE_BEAR_TRAP_TRAIT) || Dm.truthy(struggling))
            return dot;

        struggling = TRUE;
        string fear_string = null;
        float sw__1 = kill_countdown_timeleft();
        if (sw__1 >= 0 * SECONDS && sw__1 <= 5 * SECONDS)
            fear_string = "agonizingly";
        else if (sw__1 >= 5 * SECONDS && sw__1 <= 20 * SECONDS)
            fear_string = "desperately";
        else if (sw__1 >= 20 * SECONDS && sw__1 <= 40 * SECONDS)
            fear_string = "panickedly";
        else if (sw__1 >= 40 * SECONDS && sw__1 <= 50 * SECONDS)
            fear_string = "shakily";

        carbon_user.visible_message(span_danger(I18n.T("[] fiddles with and pulls at []...", carbon_user, this)),
            span_danger(I18n.T("You[] try to pull at []...", fear_string == null ? "" : Dm.text(" []", fear_string), this)), I18n.T("<i>You hear clicking and ticking.</i>"));
        if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: this)))
        {
            struggling = FALSE;
            return dot;
        }
        if (!prob(escape_chance))
        {
            to_chat(user, span_warning(I18n.T("It doesn't budge!")));
            escape_chance++;
        }
        else
        {
            user.visible_message(span_warning(I18n.T("The lock on []'s [] pops open!", user, name)),
                span_userdanger(I18n.T("You force open the padlock!")), I18n.T("<i>You hear a single, pronounced click!</i>"));
            REMOVE_TRAIT(this, TRAIT_NODROP, REVERSE_BEAR_TRAP_TRAIT);
        }
        struggling = FALSE;
        return dot;
    }

    // tg: code/game/objects/items/devices/reverse_bear_trap.dm:93
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        mob_living target = target_mob;
        if (Dm.truthy(target.get_item_by_slot(ITEM_SLOT_HEAD)))
        {
            to_chat(user, span_warning(I18n.T("Remove [] headgear first!", target.p_their())));
            return null;
        }
        target.visible_message(span_warning(I18n.T("[] starts forcing [] onto []'s head!", user, this, target)),
            span_userdanger(I18n.T("[] starts forcing [] onto your head!", target, this)), I18n.T("<i>You hear clanking.</i>"));
        to_chat(user, span_danger(I18n.T("You start forcing [] onto []'s head...", this, target)));

        if (!Dm.truthy(await do_after(user, 3 * SECONDS, target: target)) || Dm.truthy(target.get_item_by_slot(ITEM_SLOT_HEAD)))
            return null;
        target.visible_message(span_warning(I18n.T("[] forces and locks [] onto []'s head!", user, this, target)),
            span_userdanger(I18n.T("[] locks [] onto your head!", user, this)), I18n.T("<i>You hear a click, and then a timer ticking down.</i>"));
        to_chat(user, span_danger(I18n.T("You force [] onto []'s head and click the padlock shut.", this, target)));

        await user.dropItemToGround(this);
        await target.equip_to_slot_if_possible(this, ITEM_SLOT_HEAD);
        arm();
        notify_ghosts(
            I18n.T("[] put a reverse bear trap on []!", user.real_name, target.real_name),
            source: this,
            header: I18n.T("Reverse bear trap armed"),
            notify_flags: NOTIFY_CATEGORY_NOFLASH,
            ghost_sound: "sound/machines/beep/beep.ogg",
            notify_volume: 75);
        return null;
    }

    // tg: code/game/objects/items/devices/reverse_bear_trap.dm:119
    public virtual async DmTask snap()
    {
        reset();
        mob_living_carbon_human victim = loc as mob_living_carbon_human;
        if (victim == null || victim.get_item_by_slot(ITEM_SLOT_HEAD) != this)
        {
            visible_message(span_warning(I18n.T("[]'s jaws snap open with an ear-piercing crack!", this)));
            playsound(this, Dm.resource("sound/effects/snap.ogg"), 75, TRUE);
        }
        else
        {
            mob_living_carbon_human jill = loc as mob_living_carbon_human;
            jill.visible_message(span_boldwarning(I18n.T("[] goes off in []'s mouth, ripping [] head apart!", this, jill, jill.p_their())), span_userdanger(I18n.T("[] goes off!", this)));
            await jill.emote("scream");
            playsound(this, Dm.resource("sound/effects/snap.ogg"), 75, TRUE, frequency: 0.5f);
            playsound(this, Dm.resource("sound/effects/splat.ogg"), 50, TRUE, frequency: 0.5f);
            await jill.apply_damage(9999, BRUTE, BODY_ZONE_HEAD);
            jill.investigate_log(Dm.text("has been killed by [].", this), INVESTIGATE_DEATHS);
            await jill.death(); //just in case, for some reason, they're still alive
            flash_color(jill, flash_color: "#FF0000", flash_time: 100);
        }
    }

    // tg: code/game/objects/items/devices/reverse_bear_trap.dm:136
    public virtual void reset()
    {
        ticking = FALSE;
        update_appearance(UPDATE_OVERLAYS);
        REMOVE_TRAIT(this, TRAIT_NODROP, REVERSE_BEAR_TRAP_TRAIT);
        soundloop.stop();
        soundloop2.stop();
        STOP_PROCESSING(SSprocessing, this);
    }

    // tg: code/game/objects/items/devices/reverse_bear_trap.dm:144
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (ticking != TRUE)
            return dot;
        /// note: this timer overlay increments one frame every second (to simulate a clock ticking). If you want to instead have it do a full cycle in a minute, set the 'delay' of each frame of the icon overlay to 75 rather than 10, and the worn overlay to twice that.
        Dm.append(ref dot, "rbt_ticking");
        return dot;
    }

    // tg: code/game/objects/items/devices/reverse_bear_trap.dm:151
    public virtual void arm() //hulen
    {
        ticking = TRUE;
        update_appearance(UPDATE_OVERLAYS);
        escape_chance = Dm.initial(this, (obj_item_reverse_bear_trap p) => p.escape_chance); //we keep these vars until re-arm, for tracking purposes
        kill_countdown = Dm.num(world.time) + REVERSE_BEAR_TRAP_COUNTDOWN; // COOLDOWN_START
        ADD_TRAIT(this, TRAIT_NODROP, REVERSE_BEAR_TRAP_TRAIT);
        soundloop.start();
        soundloop2.mid_length = Dm.initial(soundloop2, (datum_looping_sound_reverse_bear_trap_beep p) => p.mid_length);
        soundloop2.start();
        START_PROCESSING(SSprocessing, this);
    }
}
