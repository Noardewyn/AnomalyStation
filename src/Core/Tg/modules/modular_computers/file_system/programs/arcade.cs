// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/arcade.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4c, D327): the Donksoft Micro Arcade and the maintenance disk's easier kind, tg's "Outbomb Cuban Pete" against a
// boss, with its sounds, the prize tickets it prints and its NtosArcade window; the gaming skill (its experience and its bonus to the
// rolls) came with gaps SL1 (D333). The var blocks are generated (D111).

// tg: code/modules/modular_computers/file_system/programs/arcade.dm:1
public partial class datum_computer_file_program_arcade : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/arcade.dm:40
    public virtual async DmTask game_check(mob_living user = null)
    {
        await sleep(0.5f * SECONDS);
        user?.mind?.adjust_experience(typeof(datum_skill_gaming), 1);
        if (boss_hp <= 0)
        {
            heads_up = Dm.text("You have crushed []! Rejoice!", boss_name);
            playsound(computer.loc, Dm.resource("sound/machines/arcade/win.ogg"), 50);
            game_active = FALSE;
            program_open_overlay = "arcade_off";
            if (computer != null)
                computer.update_appearance();
            ticket_count += 1;
            user?.mind?.adjust_experience(typeof(datum_skill_gaming), 50);
            user.won_game();
            await sleep(1 * SECONDS);
        }
        else if (player_hp <= 0 || player_mp <= 0)
        {
            heads_up = "You have been defeated... how will the station survive?";
            playsound(computer.loc, Dm.resource("sound/machines/arcade/lose.ogg"), 50);
            game_active = FALSE;
            program_open_overlay = "arcade_off";
            if (computer != null)
                computer.update_appearance();
            user?.mind?.adjust_experience(typeof(datum_skill_gaming), 10);
            user.lost_game();
            await sleep(1 * SECONDS);
        }
    }

    // tg: code/modules/modular_computers/file_system/programs/arcade.dm:65
    public virtual async DmTask enemy_check(mob_living user = null)
    {
        float boss_attackamt = 0; //Spam protection from boss attacks as well.
        float boss_mpamt = 0;
        float bossheal = 0;
        if (pause_state == TRUE)
        {
            boss_attackamt = rand(3, 6);
            boss_mpamt = rand(2, 4);
            bossheal = rand(4, 6);
        }
        if (game_active == FALSE)
            return;
        if (boss_mp <= 5)
        {
            heads_up = Dm.text("[] magic power has been stolen from you!", boss_mpamt);
            playsound(computer.loc, Dm.resource("sound/machines/arcade/steal.ogg"), 50, TRUE);
            player_mp -= boss_mpamt;
            boss_mp += boss_mpamt;
        }
        else if (boss_mp > 5 && boss_hp < 12)
        {
            heads_up = Dm.text("[] heals for [] health!", boss_name, bossheal);
            playsound(computer.loc, Dm.resource("sound/machines/arcade/heal.ogg"), 50, TRUE);
            boss_hp += bossheal;
            boss_mp -= boss_mpamt;
        }
        else
        {
            heads_up = Dm.text("[] attacks you for [] damage!", boss_name, boss_attackamt);
            playsound(computer.loc, Dm.resource("sound/machines/arcade/hit.ogg"), 50, TRUE);
            player_hp -= boss_attackamt;
        }

        pause_state = FALSE;
        await game_check(user);
    }

    // tg: code/modules/modular_computers/file_system/programs/arcade.dm:93
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/simple/arcade)): the asset transport is the client's (DV44); the client draws the bosses
        // from its own copy of tg's pictures (icons/ui/arcade/boss*.gif)
        return Dm.list();
    }

    // tg: code/modules/modular_computers/file_system/programs/arcade.dm:98
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["Hitpoints"] = boss_hp;
        data["PlayerHitpoints"] = player_hp;
        data["PlayerMP"] = player_mp;
        data["TicketCount"] = ticket_count;
        data["GameActive"] = game_active;
        data["PauseState"] = pause_state;
        data["Status"] = heads_up;
        data["BossID"] = Dm.text("boss[].gif", boss_id);
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/arcade.dm:110
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        mob_living gamer = ui.user as mob_living;
        if (gamer == null)
            return dot;
        await gamer.played_game();
        float gamerSkillLevel = 0;
        float gamerSkill = 0;
        if (gamer?.mind != null)
        {
            gamerSkillLevel = Dm.num(gamer.mind.get_skill_level(typeof(datum_skill_gaming)));
            gamerSkill = Dm.num(gamer.mind.get_skill_modifier(typeof(datum_skill_gaming), SKILL_RANDS_MODIFIER));
        }
        switch (action)
        {
            case "Attack":
            {
                float attackamt = 0; //Spam prevention.
                if (pause_state == FALSE)
                    attackamt = rand(2, 6) + rand(0, gamerSkill);
                pause_state = TRUE;
                heads_up = Dm.text("You attack for [] damage.", attackamt);
                playsound(computer.loc, Dm.resource("sound/machines/arcade/hit.ogg"), 50, TRUE);
                boss_hp -= attackamt;
                await sleep(1 * SECONDS);
                await game_check(gamer);
                await enemy_check(gamer);
                return TRUE;
            }
            case "Heal":
            {
                float healamt = 0; //More Spam Prevention.
                float healcost = 0;
                if (pause_state == FALSE)
                {
                    healamt = rand(6, 8) + rand(0, gamerSkill);
                    float maxPointCost = 3;
                    if (gamerSkillLevel >= SKILL_LEVEL_JOURNEYMAN)
                        maxPointCost = 2;
                    healcost = rand(1, maxPointCost);
                }
                pause_state = TRUE;
                heads_up = Dm.text("You heal for [] damage.", healamt);
                playsound(computer.loc, Dm.resource("sound/machines/arcade/heal.ogg"), 50, TRUE);
                player_hp += healamt;
                player_mp -= healcost;
                await sleep(1 * SECONDS);
                await game_check(gamer);
                await enemy_check(gamer);
                return TRUE;
            }
            case "Recharge_Power":
            {
                float rechargeamt = 0; //As above.
                if (pause_state == FALSE)
                    rechargeamt = rand(4, 7) + rand(0, gamerSkill);
                pause_state = TRUE;
                heads_up = Dm.text("You regain [] magic power.", rechargeamt);
                playsound(computer.loc, Dm.resource("sound/machines/arcade/mana.ogg"), 50, TRUE);
                player_mp += rechargeamt;
                await sleep(1 * SECONDS);
                await game_check(gamer);
                await enemy_check(gamer);
                return TRUE;
            }
            case "Dispense_Tickets":
                if (Dm.num(computer.stored_paper) <= 0)
                {
                    to_chat(gamer, span_notice(I18n.T("Printer is out of paper.")));
                    return dot;
                }
                else
                {
                    computer.visible_message(span_notice(I18n.T("\\The [] prints out paper.", computer)));
                    if (ticket_count >= 1)
                    {
                        Dm.@new<obj_item_stack_arcadeticket>(get_turf(computer));
                        to_chat(gamer, span_notice(I18n.T("[] dispenses a ticket!", computer)));
                        ticket_count -= 1;
                        computer.stored_paper = Dm.num(computer.stored_paper) - 1;
                    }
                    else
                        to_chat(gamer, span_notice(I18n.T("You don't have any stored tickets!")));
                    return TRUE;
                }
            case "Start_Game":
                game_active = TRUE;
                boss_hp = Dm.initial(this, (datum_computer_file_program_arcade a) => a.boss_hp);
                player_hp = Dm.initial(this, (datum_computer_file_program_arcade a) => a.player_hp);
                player_mp = Dm.initial(this, (datum_computer_file_program_arcade a) => a.player_mp);
                heads_up = Dm.text("You stand before []! Prepare for battle!", boss_name);
                program_open_overlay = "arcade";
                boss_id = rand(1, 6);
                pause_state = FALSE;
                if (computer != null)
                    computer.update_appearance();
                break;
        }
        return dot;
    }
}
