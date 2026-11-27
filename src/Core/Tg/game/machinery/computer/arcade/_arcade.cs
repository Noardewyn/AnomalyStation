// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/arcade/_arcade.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the arcade cabinet's tickets and prizes, its reset by the display case key, its EMP prizes and the victory tickets.
// The var block is generated (D111).

// tg: code/game/machinery/computer/arcade/_arcade.dm:1
public partial class obj_machinery_computer_arcade : obj_machinery_computer
{
    // tg: code/game/machinery/computer/arcade/_arcade.dm:27  the proc-level static radial_menu_options
    private static ref DmList item_interaction__radial_menu_options => ref TgState.Current.obj_machinery_computer_arcade__item_interaction__radial_menu_options;

    // tg: code/game/machinery/computer/arcade/_arcade.dm:16
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_stack_arcadeticket)))
        {
            var tickets = (obj_item_stack_arcadeticket)tool;
            if (!Dm.truthy(await tickets.use(2)))
            {
                balloon_alert(user, I18n.T("need 2 tickets!"));
                return ITEM_INTERACT_BLOCKING;
            }

            await prizevend(user);
            balloon_alert(user, I18n.T("prize claimed"));
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_key_displaycase)) || istype(tool, typeof(obj_item_access_key)))
        {
            if (!Dm.truthy(item_interaction__radial_menu_options))
            {
                item_interaction__radial_menu_options = Dm.list(
                    Dm.kv("Reset Cabinet", image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_reset")),
                    Dm.kv("Cancel", image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_close")));
            }
            object radial_reset_menu = await show_radial_menu(user, this, item_interaction__radial_menu_options, require_near: TRUE);
            if (!Dm.eq(radial_reset_menu, "Reset Cabinet"))
                return ITEM_INTERACT_BLOCKING;
            playsound(loc as atom, Dm.resource("sound/items/rattling_keys.ogg"), 25, TRUE);
            if (!Dm.truthy(await do_after(user, 10 * SECONDS, this)))
                return ITEM_INTERACT_BLOCKING;
            balloon_alert(user, I18n.T("cabinet reset"));
            reset_cabinet(user);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/machinery/computer/arcade/_arcade.dm:46
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        //you can't stop playing when you start.
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return ITEM_INTERACT_BLOCKING;
        return await base.screwdriver_act(user, tool);
    }

    ///Performs a factory reset of the cabinet and wipes all its stats.
    // tg: code/game/machinery/computer/arcade/_arcade.dm:53
    public virtual void reset_cabinet(mob_living user = null)
    {
        obj_flags = Dm.bitand(obj_flags, ~EMAGGED);
        SStgui.update_uis(this);
    }

    // tg: code/game/machinery/computer/arcade/_arcade.dm:58
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))) || Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;

        object empprize = null;
        float num_of_prizes = 0;
        switch (Dm.num(severity))
        {
            case 1:
                num_of_prizes = rand(1, 4);
                break;
            case 2:
                num_of_prizes = rand(0, 2);
                break;
        }
        for (float i = num_of_prizes; i > 0; i--)
        {
            if (Dm.truthy(prize_override))
                empprize = pick_weight(prize_override);
            else
                empprize = pick_weight(GLOB.arcade_prize_pool);
            Dm.@new((Type)empprize, loc);
        }
        await explosion(this, devastation_range: -1, light_impact_range: 1 + num_of_prizes, flame_range: 1 + num_of_prizes);
        return dot;
    }

    ///Dispenses the proper prizes and gives them a positive mood event. If valid, has a small chance to give a pulse rifle.
    // tg: code/game/machinery/computer/arcade/_arcade.dm:79
    public virtual async DmTask prizevend(mob_living user = null, float? prizes = null)
    {
        prizes ??= 1;
        if (Dm.num(user.mind?.get_skill_level(typeof(datum_skill_gaming))) >= SKILL_LEVEL_LEGENDARY && HAS_TRAIT(user, TRAIT_GAMERGOD))
        {
            visible_message(span_notice(I18n.T("[] inputs an intense cheat code!", user)),
                span_notice(I18n.T("You hear a flurry of buttons being pressed.")));
            await say(I18n.T("CODE ACTIVATED: EXTRA PRIZES."));
            prizes *= 2;
        }
        for (float i = 1; i <= prizes; i++)
        {
            await user.add_mood_event("arcade", typeof(datum_mood_event_arcade));
            if (prob(0.0001f)) //1 in a million
            {
                Dm.@new<obj_item_gun_energy_pulse_prize>(get_turf(this));
                visible_message(span_notice(I18n.T("[] dispenses.. woah, a gun! Way past cool.", this)), span_notice(I18n.T("You hear a chime and a shot.")));
                Dm.Deferred("code/game/machinery/computer/arcade/_arcade.dm:90", "prizevend: user.client.give_award(/datum/award/achievement/misc/pulse, user) (the achievements)");
                continue;
            }

            object prizeselect;
            if (Dm.truthy(prize_override))
                prizeselect = pick_weight(prize_override);
            else
                prizeselect = pick_weight(GLOB.arcade_prize_pool);
            var the_prize = Dm.@new((Type)prizeselect, get_turf(this)) as atom_movable;
            playsound(this, Dm.resource("sound/machines/machine_vend.ogg"), 50, TRUE, extrarange: -3);
            visible_message(span_notice(I18n.T("[] dispenses []!", this, the_prize)), span_notice(I18n.T("You hear a chime and a clunk.")));
        }
    }

    // tg: code/game/machinery/computer/arcade/_arcade.dm:102
    public virtual void victory_tickets(object tickets = null, float? sound = null)
    {
        sound ??= TRUE;
        SEND_SIGNAL(COMSIG_ARCADE_VICTORY, Dm.list(this));
        visible_message(span_notice(I18n.T("[] dispenses [] ticket\\s!", this, tickets)));
        Dm.@new<obj_item_stack_arcadeticket>(get_turf(this), tickets);
        if (Dm.truthy(sound))
            playsound(loc as atom, Dm.resource("sound/machines/arcade/win.ogg"), 40);
    }
}

public sealed partial class TgState
{
    // tg: code/game/machinery/computer/arcade/_arcade.dm:27
    internal DmList obj_machinery_computer_arcade__item_interaction__radial_menu_options;
}
