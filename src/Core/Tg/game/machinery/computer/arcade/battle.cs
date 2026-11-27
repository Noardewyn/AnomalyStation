// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/arcade/battle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the battle arcade, its worlds, shop, inn and fights, Cuban Pete's emagged hard mode, with its BattleArcade window.
// The gear (battle_gear.dm) is var-only. The var blocks are generated (D111).

// tg: code/game/machinery/computer/arcade/battle.dm:40
public partial class obj_machinery_computer_arcade_battle : obj_machinery_computer_arcade
{
    ///How many enemies needs to be defeated until the 'Boss' of the stage appears.
    // tg: code/game/machinery/computer/arcade/battle.dm:2
    private const float WORLD_ENEMY_BOSS = 2;
    ///The default amount of EXP you gain from killing an enemy, modifiers stacked on top of this.
    private const float DEFAULT_EXP_GAIN = 50;
    ///The default cost to purchase an item. Sleeping at the Inn is half of this.
    private const float DEFAULT_ITEM_PRICE = 30;
    ///The max HP the player can have at any time.
    private const float PLAYER_MAX_HP = 100;
    ///The max MP the player can have at any time.
    private const float PLAYER_MAX_MP = 50;
    ///The default cost of a spell, in MP. Defending will instead restore this amount.
    private const float SPELL_MP_COST = 10;
    private const string UI_PANEL_SHOP = "Shop";
    private const string UI_PANEL_WORLD_MAP = "World Map";
    private const string UI_PANEL_BATTLE = "Battle";
    private const string UI_PANEL_BETWEEN_FIGHTS = "Between Battle";
    private const string UI_PANEL_GAMEOVER = "Game Over";
    ///The player is set to counterattack the enemy's next move.
    private const float BATTLE_ATTACK_FLAG_COUNTERATTACK = 1 << 0;
    ///The player is set to defend against the enemy's next move.
    private const float BATTLE_ATTACK_FLAG_DEFEND = 1 << 1;
    private const string BATTLE_ARCADE_PLAYER_ATTACK = "Attack";
    private const string BATTLE_ARCADE_PLAYER_HEAVY_ATTACK = "Heavy Attack";
    private const string BATTLE_ARCADE_PLAYER_COUNTERATTACK = "Counterattack";
    private const string BATTLE_ARCADE_PLAYER_DEFEND = "Defend";

    ///List of all battle arcade gear that is available in the shop in game.
    // tg: code/game/machinery/computer/arcade/battle.dm:48
    public static ref DmList battle_arcade_gear_list => ref TgState.Current.obj_machinery_computer_arcade_battle__battle_arcade_gear_list;
    ///List of all worlds in the game.
    // tg: code/game/machinery/computer/arcade/battle.dm:50
    public static ref DmList all_worlds => ref TgState.Current.obj_machinery_computer_arcade_battle__all_worlds;
    // tg: code/game/machinery/computer/arcade/battle.dm:61
    public static ref DmList all_attack_types => ref TgState.Current.obj_machinery_computer_arcade_battle__all_attack_types;

    // tg: code/game/machinery/computer/arcade/battle.dm:111
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (battle_arcade_gear_list == null)
        {
            DmList all_gear = Dm.list();
            foreach (Type template in Dm.each<Type>(subtypesof(typeof(datum_battle_arcade_gear))))
            {
                if (!Dm.truthy(Dm.initial(template, (datum_battle_arcade_gear p) => p.slot))) //needs to fit in something.
                    continue;
                all_gear[Dm.initial(template, (datum_battle_arcade_gear p) => p.name)] = Dm.@new(template);
            }
            battle_arcade_gear_list = all_gear;
        }

        name = Dm.str(make_boss_name_with_verb());
        return dot;
    }

    // tg: code/game/machinery/computer/arcade/battle.dm:123
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        balloon_alert(user, I18n.T("hard mode enabled"));
        to_chat(user, span_warning(I18n.T("A mesmerizing Rhumba beat starts playing from the arcade machine's speakers!")));
        await setup_new_opponent(user as mob_living);
        feedback_message = I18n.T("If you die in the game, you die for real!");
        SStgui.update_uis(this);
        return TRUE;
    }

    // tg: code/game/machinery/computer/arcade/battle.dm:134
    public override void reset_cabinet(mob_living user = null)
    {
        enemy_name = null;
        player_turn = Dm.initial(this, (obj_machinery_computer_arcade_battle p) => p.player_turn);
        feedback_message = Dm.initial(this, (obj_machinery_computer_arcade_battle p) => p.feedback_message);
        player_current_world = Dm.initial(this, (obj_machinery_computer_arcade_battle p) => p.player_current_world);
        latest_unlocked_world = Dm.initial(this, (obj_machinery_computer_arcade_battle p) => p.latest_unlocked_world);
        enemies_defeated = Dm.initial(this, (obj_machinery_computer_arcade_battle p) => p.enemies_defeated);
        player_gold = Dm.initial(this, (obj_machinery_computer_arcade_battle p) => p.player_gold);
        player_current_hp = Dm.initial(this, (obj_machinery_computer_arcade_battle p) => p.player_current_hp);
        player_current_mp = Dm.initial(this, (obj_machinery_computer_arcade_battle p) => p.player_current_mp);
        ui_panel = Dm.initial(this, (obj_machinery_computer_arcade_battle p) => p.ui_panel);
        bomb_cooldown = Dm.initial(this, (obj_machinery_computer_arcade_battle p) => p.bomb_cooldown);
        equipped_gear = Dm.list(Dm.kv(WEAPON_SLOT, null), Dm.kv(ARMOR_SLOT, null));
        base.reset_cabinet(user);
    }

    // tg: code/game/machinery/computer/arcade/battle.dm:150
    public virtual object make_boss_name_with_verb(object boss_verb = null)
    {
        if (Dm.truthy(check_holidays(HALLOWEEN)))
            boss_verb = Dm.truthy(boss_verb) ? boss_verb : pick_list(ARCADE_FILE, "rpg_action_halloween");
        else if (Dm.truthy(check_holidays(CHRISTMAS)))
            boss_verb = Dm.truthy(boss_verb) ? boss_verb : pick_list(ARCADE_FILE, "rpg_action_xmas");
        else if (Dm.truthy(check_holidays(VALENTINES)))
            boss_verb = Dm.truthy(boss_verb) ? boss_verb : pick_list(ARCADE_FILE, "rpg_action_valentines");
        else
            boss_verb = Dm.truthy(boss_verb) ? boss_verb : pick_list(ARCADE_FILE, "rpg_action");

        return I18n.T("[] []", boss_verb, make_boss_name());
    }

    // tg: code/game/machinery/computer/arcade/battle.dm:162
    public virtual object make_boss_name(object boss_name = null, object boss_adjective = null)
    {
        if (Dm.truthy(check_holidays(HALLOWEEN)))
        {
            boss_adjective = Dm.truthy(boss_adjective) ? boss_adjective : pick_list(ARCADE_FILE, "rpg_adjective_halloween");
            boss_name = Dm.truthy(boss_name) ? boss_name : pick_list(ARCADE_FILE, "rpg_enemy_halloween");
        }
        else if (Dm.truthy(check_holidays(CHRISTMAS)))
        {
            boss_adjective = Dm.truthy(boss_adjective) ? boss_adjective : pick_list(ARCADE_FILE, "rpg_adjective_xmas");
            boss_name = Dm.truthy(boss_name) ? boss_name : pick_list(ARCADE_FILE, "rpg_enemy_xmas");
        }
        else if (Dm.truthy(check_holidays(VALENTINES)))
        {
            boss_adjective = Dm.truthy(boss_adjective) ? boss_adjective : pick_list(ARCADE_FILE, "rpg_adjective_valentines");
            boss_name = Dm.truthy(boss_name) ? boss_name : pick_list(ARCADE_FILE, "rpg_enemy_valentines");
        }
        else
        {
            boss_adjective = Dm.truthy(boss_adjective) ? boss_adjective : pick_list(ARCADE_FILE, "rpg_adjective");
            boss_name = Dm.truthy(boss_name) ? boss_name : pick_list(ARCADE_FILE, "rpg_enemy");
        }

        return I18n.T("The [] []", boss_adjective, boss_name);
    }

    ///Sets up a new opponent depending on what stage they are at.
    // tg: code/game/machinery/computer/arcade/battle.dm:180
    public virtual async DmTask setup_new_opponent(mob_living user = null, float? enemy_gets_first_move = null)
    {
        enemy_gets_first_move ??= FALSE;
        enemy_hp = round(rand(90, 125) * Dm.num(all_worlds[player_current_world]), 1);
        enemy_mp = round(rand(20, 30) * Dm.num(all_worlds[player_current_world]), 1);
        enemy_gold_reward = rand((DEFAULT_ITEM_PRICE / 2), DEFAULT_ITEM_PRICE);

        // there's only one boss in each stage (except the last)
        bool boss = (player_current_world == latest_unlocked_world) && Dm.eq(enemies_defeated, WORLD_ENEMY_BOSS);
        if (boss)
        {
            enemy_mp = Dm.num(enemy_mp) * 1.25f;
            enemy_hp = Dm.num(enemy_hp) * 1.25f;
            enemy_gold_reward = Dm.num(enemy_gold_reward) * 1.5f;
        }

        enemy_icon_id = rand(1, 6);
        enemy_name = make_boss_name(boss_adjective: boss ? I18n.T("Big Boss") : null);
        feedback_message = I18n.T("New game started against []", enemy_name);

        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            enemy_name = I18n.T("Cuban Pete");
            enemy_hp = Dm.num(enemy_hp) + 100; //extra HP just to make cuban pete even more bullshit
        }

        //set max HP to reference later
        enemy_max_hp = enemy_hp;
        //set the player to fight now.
        ui_panel = UI_PANEL_BATTLE;

        if (Dm.truthy(enemy_gets_first_move))
            await perform_enemy_turn(user);
    }

    /**
     * on_battle_win
     *
     * Called when the player wins a level, this handles giving EXP, loot, tickets, etc.
     * It also handles clearing the enemy out for the next one, and unlocking new worlds.
     * We stop at BATTLE_WORLD_NINE because it is the last stage, and has infinite bosses.
     */
    // tg: code/game/machinery/computer/arcade/battle.dm:217
    public virtual async DmTask on_battle_win(mob_living user = null)
    {
        enemy_name = null;
        feedback_message = null;
        player_turn = TRUE;
        if (player_current_world == latest_unlocked_world)
        {
            if (Dm.eq(enemies_defeated, WORLD_ENEMY_BOSS))
            {
                enemies_defeated = 0;
                //the last stage doesn't have a next one to move onto.
                if (latest_unlocked_world != BATTLE_WORLD_NINE)
                {
                    float current_world = all_worlds.Find(latest_unlocked_world);
                    latest_unlocked_world = Dm.str(all_worlds[current_world + 1]);
                    ui_panel = UI_PANEL_WORLD_MAP;
                    await say(I18n.T("New world unlocked, []!", latest_unlocked_world));
                }
            }
            enemies_defeated = Dm.num(enemies_defeated) + 1;
        }
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            obj_flags = Dm.bitand(obj_flags, ~EMAGGED);
            bomb_cooldown = Dm.initial(this, (obj_machinery_computer_arcade_battle p) => p.bomb_cooldown);
            Dm.@new<obj_effect_spawner_newbomb_plasma>(loc, typeof(obj_item_assembly_timer));
            Dm.@new<obj_item_clothing_head_collectable_petehat>(loc);
            message_admins(Dm.text("[] has outbombed Cuban Pete and been awarded a bomb.", ADMIN_LOOKUPFLW(user)));
            user.log_message("outbombed Cuban Pete and has been awarded a bomb.", LOG_GAME);
        }
        else
            victory_tickets(2, FALSE);
        player_gold += Dm.num(enemy_gold_reward);
        if (user != null)
        {
            float exp_gained = DEFAULT_EXP_GAIN * Dm.num(all_worlds[player_current_world]);
            user.mind?.adjust_experience(typeof(datum_skill_gaming), exp_gained);
            user.won_game();
        }
        SSblackbox.record_feedback("nested tally", "arcade_results", 1, Dm.list("win", Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) ? "emagged" : "normal"));
        playsound(loc as atom, Dm.resource("sound/machines/arcade/win.ogg"), 40);
        if (ui_panel != UI_PANEL_WORLD_MAP) //we havent been booted to world map, we're still going.
            ui_panel = UI_PANEL_BETWEEN_FIGHTS;
    }

    ///Called when a mob loses at the battle arcade.
    // tg: code/game/machinery/computer/arcade/battle.dm:252
    public virtual async DmTask lose_game(mob_living user = null)
    {
        SSblackbox.record_feedback("nested tally", "arcade_results", 1, Dm.list("loss", "hp", Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) ? "emagged" : "normal"));
        SStgui.update_uis(this);
        if (user == null)
            return;
        user.lost_game();
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            user.investigate_log("has been gibbed by an emagged Orion Trail game.", INVESTIGATE_DEATHS);
            await user.gib(DROP_ALL_REMAINS);
        }
    }

    ///Called when the enemy attacks you.
    // tg: code/game/machinery/computer/arcade/battle.dm:263
    public virtual async DmTask user_take_damage(mob user = null, object base_damage_taken = null)
    {
        var armor = equipped_gear[ARMOR_SLOT] as datum_battle_arcade_gear;
        float damage_taken = (Dm.num(base_damage_taken) * Dm.num(all_worlds[player_current_world])) / (armor != null ? Dm.num(armor.bonus_modifier) : 1);
        player_current_hp -= round(max(0, damage_taken), 1);
        if (player_current_hp <= 0)
        {
            ui_panel = UI_PANEL_GAMEOVER;
            feedback_message = I18n.T("GAME OVER.");
            await say(I18n.T("You have been crushed! GAME OVER."));
            playsound(loc as atom, Dm.resource("sound/machines/arcade/lose.ogg"), 40, TRUE);
            await lose_game(user as mob_living);
            return;
        }

        feedback_message = I18n.T("User took [] damage!", damage_taken);
        playsound(loc as atom, Dm.resource("sound/machines/arcade/hit.ogg"), 40, TRUE, extrarange: -3);
        SStgui.update_uis(this);
    }

    ///Called when you attack the enemy.
    // tg: code/game/machinery/computer/arcade/battle.dm:280
    public virtual async DmTask process_player_attack(mob user = null, object attack_type = null)
    {
        float damage_dealt = 0;
        switch (attack_type)
        {
            case BATTLE_ARCADE_PLAYER_ATTACK:
            {
                var weapon = equipped_gear[WEAPON_SLOT] as datum_battle_arcade_gear;
                damage_dealt = (rand(5, 15) * (weapon != null ? Dm.num(weapon.bonus_modifier) : 1));
                break;
            }
            case BATTLE_ARCADE_PLAYER_HEAVY_ATTACK:
            {
                var weapon = equipped_gear[WEAPON_SLOT] as datum_battle_arcade_gear;
                damage_dealt = (rand(15, 25) * (weapon != null ? Dm.num(weapon.bonus_modifier) : 1));
                break;
            }
            case BATTLE_ARCADE_PLAYER_COUNTERATTACK:
                feedback_message = I18n.T("User prepares to counterattack!");
                await process_enemy_turn(user, defending_flags: BATTLE_ATTACK_FLAG_COUNTERATTACK);
                playsound(loc as atom, Dm.resource("sound/machines/arcade/mana.ogg"), 40, TRUE, extrarange: -3);
                break;
            case BATTLE_ARCADE_PLAYER_DEFEND:
                feedback_message = I18n.T("User pulls up their shield!");
                await process_enemy_turn(user, defending_flags: BATTLE_ATTACK_FLAG_DEFEND);
                playsound(loc as atom, Dm.resource("sound/machines/arcade/mana.ogg"), 40, TRUE, extrarange: -3);
                break;
        }

        if (!Dm.truthy(damage_dealt))
            return;
        enemy_hp = Dm.num(enemy_hp) - round(max(0, damage_dealt), 1);
        feedback_message = I18n.T("[] took [] damage!", enemy_name, damage_dealt);
        playsound(loc as atom, Dm.resource("sound/machines/arcade/hit.ogg"), 40, TRUE, extrarange: -3);
        await process_enemy_turn(user);
    }

    ///Called when you successfully counterattack the enemy.
    // tg: code/game/machinery/computer/arcade/battle.dm:306
    public virtual async DmTask successful_counterattack(mob user = null)
    {
        var weapon = equipped_gear[WEAPON_SLOT] as datum_battle_arcade_gear;
        float damage_dealt = (rand(20, 30) * (weapon != null ? Dm.num(weapon.bonus_modifier) : 1));
        enemy_hp = Dm.num(enemy_hp) - round(max(0, damage_dealt), 1);
        feedback_message = I18n.T("User counterattacked for [] damage!", damage_dealt);
        playsound(loc as atom, Dm.resource("sound/machines/arcade/boom.ogg"), 40, TRUE, extrarange: -3);
        if (Dm.num(enemy_hp) <= 0)
            await on_battle_win(user as mob_living);
        SStgui.update_uis(this);
    }

    ///Handles the delay between the user's and enemy's turns to process what's going on.
    // tg: code/game/machinery/computer/arcade/battle.dm:318
    public virtual async DmTask<object> process_enemy_turn(mob user = null, float? defending_flags = null)
    {
        defending_flags ??= NONE;
        if (Dm.num(enemy_hp) <= 0)
        {
            await on_battle_win(user as mob_living);
            return null;
        }
        //if emagged, cuban pete will set up a bomb acting up as a timer. when it reaches 0 the player fucking dies

        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            bomb_cooldown--;
            switch (bomb_cooldown)
            {
                case 18:
                    feedback_message = I18n.T("[] takes two valve tank and links them together, what's he planning?", enemy_name);
                    break;
                case 15:
                    feedback_message = I18n.T("[] adds a remote control to the tan- ho god is that a bomb?", enemy_name);
                    break;
                case 12:
                    feedback_message = I18n.T("[] throws the bomb next to you, you'r too scared to pick it up.", enemy_name);
                    break;
                case 6:
                    feedback_message = I18n.T("[]'s hand brushes the remote linked to the bomb, your heart skipped a beat.", enemy_name);
                    break;
                case 2:
                    feedback_message = I18n.T("[] is going to press the button! It's now or never!", enemy_name);
                    break;
                case 0:
                    player_current_hp = 0; //instant death
                    break;
            }
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(perform_enemy_turn), user, defending_flags), 1 * SECONDS, file: "code/game/machinery/computer/arcade/battle.dm", line: 339);
        return null;
    }

    /**
     * perform_enemy_turn
     *
     * Actually performs the enemy's turn.
     * We first roll to see if the enemy should use magic. As their HP goes lower, the chances of self healing goes higher, but
     * if they lack the MP, then it's rolling to steal MP from the player.
     * After, we will roll to see if the player counterattacks the enemy (if set), otherwise we will attack normally.
     */
    // tg: code/game/machinery/computer/arcade/battle.dm:349
    public virtual async DmTask<object> perform_enemy_turn(mob_living user = null, float? defending_flags = null)
    {
        defending_flags ??= NONE;
        player_turn = TRUE;
        float chance_to_magic = round(max((-(Dm.num(enemy_hp) - Dm.num(enemy_max_hp)) / 2), 75), 1);
        if (!Dm.eq(enemy_hp, enemy_max_hp) && prob(chance_to_magic))
        {
            if (Dm.num(enemy_mp) >= 10)
            {
                float healed_amount = rand(10, 20);
                enemy_hp = round(min(Dm.num(enemy_max_hp), Dm.num(enemy_hp) + healed_amount), 1);
                enemy_mp = Dm.num(enemy_mp) - round(max(0, 10), 1);
                feedback_message = I18n.T("[] healed for [] health points!", enemy_name, healed_amount);
                playsound(loc as atom, Dm.resource("sound/machines/arcade/heal.ogg"), 40, TRUE, extrarange: -3);
                SStgui.update_uis(this);
                return null;
            }
            if (player_current_mp >= 5) //minimum to steal
            {
                float healed_amount = rand(5, 10);
                player_current_mp -= round(max(0, healed_amount), 1);
                enemy_mp = Dm.num(enemy_mp) + healed_amount;
                feedback_message = I18n.T("[] stole [] MP from you!", enemy_name, healed_amount);
                playsound(loc as atom, Dm.resource("sound/machines/arcade/steal.ogg"), 40, TRUE);
                SStgui.update_uis(this);
                return null;
            }
            //we couldn't heal ourselves or steal MP, we'll just attack instead.
        }
        object or__1 = user?.mind?.get_skill_level(typeof(datum_skill_gaming));
        float skill_level = Dm.truthy(or__1) ? Dm.num(or__1) : 1;
        float chance_at_counterattack = 40 + (skill_level * 5); //at level 1 this is 45, at legendary this is 75
        float damage_dealt = Dm.truthy(Dm.bitand(defending_flags, BATTLE_ATTACK_FLAG_DEFEND)) ? rand(5, 10) : rand(15, 20);
        if (Dm.truthy(Dm.bitand(defending_flags, BATTLE_ATTACK_FLAG_COUNTERATTACK)) && prob(chance_at_counterattack))
        {
            await successful_counterattack(user);
            return null;
        }
        await user_take_damage(user, damage_dealt);
        return null;
    }

    // tg: code/game/machinery/computer/arcade/battle.dm:376
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = await base.ui_data(user) as DmList;

        data["feedback_message"] = feedback_message;
        data["shop_items"] = Dm.list();
        foreach (object gear_name in Dm.each(battle_arcade_gear_list))
        {
            var gear = battle_arcade_gear_list[gear_name] as datum_battle_arcade_gear;
            if (!Dm.eq(latest_unlocked_world, gear.world_available))
                continue;
            Dm.append_at(data, "shop_items", Dm.list(gear_name));
        }
        data["ui_panel"] = ui_panel;
        data["player_current_world"] = player_current_world;
        data["unlocked_world_modifier"] = all_worlds[latest_unlocked_world];
        data["latest_unlocked_world_position"] = all_worlds.Find(latest_unlocked_world);
        data["player_gold"] = player_gold;
        data["player_current_hp"] = player_current_hp;
        data["player_current_mp"] = player_current_mp;
        data["enemy_icon_id"] = Dm.text("boss[].gif", enemy_icon_id);
        data["enemy_name"] = enemy_name;
        data["enemy_max_hp"] = enemy_max_hp;
        data["enemy_hp"] = enemy_hp;
        data["enemy_mp"] = enemy_mp;

        data["equipped_gear"] = Dm.list();
        foreach (object gear_slot in Dm.each(equipped_gear))
        {
            var user_gear = equipped_gear[gear_slot] as datum_battle_arcade_gear;
            if (!istype(user_gear, typeof(datum_battle_arcade_gear)))
                continue;
            Dm.append_at(data, "equipped_gear", Dm.list(Dm.list(
                Dm.kv("name", user_gear.name),
                Dm.kv("slot", gear_slot))));
        }

        return data;
    }

    // tg: code/game/machinery/computer/arcade/battle.dm:409
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = await base.ui_static_data(user) as DmList;

        DmList worlds = Dm.list();
        data["all_worlds"] = worlds;
        foreach (object individual_world in Dm.each(all_worlds))
            UNTYPED_LIST_ADD(ref worlds, individual_world);
        DmList attack_types = Dm.list();
        data["attack_types"] = attack_types;
        foreach (object individual_attack_type in Dm.each(all_attack_types))
            UNTYPED_LIST_ADD(ref attack_types, Dm.list(Dm.kv("name", individual_attack_type), Dm.kv("tooltip", all_attack_types[individual_attack_type])));
        data["cost_of_items"] = DEFAULT_ITEM_PRICE;
        data["max_hp"] = PLAYER_MAX_HP;
        data["max_mp"] = PLAYER_MAX_MP;

        return data;
    }

    // tg: code/game/machinery/computer/arcade/battle.dm:423
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/simple/arcade)): the asset transport is the client's (DV44); the client draws the bosses
        // from its own copy of tg's pictures (icons/ui/arcade/boss*.gif)
        return Dm.list();
    }

    // tg: code/game/machinery/computer/arcade/battle.dm:428
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "BattleArcade", "Battle Arcade");
            ui.set_autoupdate(FALSE);
            await ui.open();
        }
        return dot;
    }

    // tg: code/game/machinery/computer/arcade/battle.dm:436
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        var gamer = ui.user as mob_living;
        if (!istype(gamer, typeof(mob_living)))
            return dot;

        await gamer.played_game();
        switch (ui_panel)
        {
            case UI_PANEL_GAMEOVER:
                switch (action)
                {
                    case "restart":
                        reset_cabinet();
                        return TRUE;
                }
                break;
            case UI_PANEL_SHOP:
                switch (action)
                {
                    case "sleep":
                        if (player_gold < DEFAULT_ITEM_PRICE / 2)
                        {
                            await say(I18n.T("You don't have enough gold to rest!"));
                            return TRUE;
                        }
                        player_gold -= DEFAULT_ITEM_PRICE / 2;
                        playsound(loc as atom, Dm.resource("sound/vehicles/mecha/skyfall_power_up.ogg"), 40);
                        player_current_hp = PLAYER_MAX_HP;
                        player_current_mp = PLAYER_MAX_MP;
                        return TRUE;
                    case "buy_item":
                    {
                        var gear = battle_arcade_gear_list[@params["purchasing_item"]] as datum_battle_arcade_gear;
                        if (!Dm.eq(latest_unlocked_world, gear.world_available) || equipped_gear[gear.slot] == gear)
                        {
                            await say(I18n.T("That item is not in stock."));
                            return TRUE;
                        }
                        if (player_gold < (DEFAULT_ITEM_PRICE * Dm.num(all_worlds[latest_unlocked_world])))
                        {
                            await say(I18n.T("You don't have enough gold to buy that!"));
                            return TRUE;
                        }
                        player_gold -= DEFAULT_ITEM_PRICE * Dm.num(all_worlds[latest_unlocked_world]);
                        equipped_gear[gear.slot] = gear;
                        return TRUE;
                    }
                    case "leave":
                        ui_panel = UI_PANEL_WORLD_MAP;
                        return TRUE;
                }
                break;
            case UI_PANEL_WORLD_MAP:
                switch (action)
                {
                    case "start_fight":
                    {
                        float world_travelling = all_worlds.Find(@params["selected_arena"]);
                        float max_unlocked_worlds = all_worlds.Find(latest_unlocked_world);
                        if (world_travelling > max_unlocked_worlds)
                        {
                            await say(I18n.T("That world is not unlocked yet!"));
                            return TRUE;
                        }
                        player_current_world = Dm.str(all_worlds[world_travelling]);
                        await setup_new_opponent(gamer);
                        return TRUE;
                    }
                    case "enter_inn":
                        ui_panel = UI_PANEL_SHOP;
                        return TRUE;
                }
                break;
            case UI_PANEL_BETWEEN_FIGHTS:
                switch (action)
                {
                    case "continue_without_rest":
                        await setup_new_opponent(gamer);
                        return TRUE;
                    case "continue_with_rest":
                        if (prob(60))
                        {
                            playsound(loc as atom, Dm.resource("sound/vehicles/mecha/skyfall_power_up.ogg"), 40);
                            player_current_hp = PLAYER_MAX_HP;
                            player_current_mp = PLAYER_MAX_MP;
                        }
                        else
                        {
                            playsound(loc as atom, Dm.resource("sound/machines/defib/defib_zap.ogg"), 40);
                            if (prob(40))
                            {
                                //You got robbed, and now have to go to your next fight.
                                player_gold /= 2;
                            }
                            else
                            {
                                //You got ambushed, the enemy gets the first hit.
                                await setup_new_opponent(gamer, enemy_gets_first_move: TRUE);
                                return TRUE;
                            }
                        }
                        await setup_new_opponent(gamer);
                        return TRUE;
                    case "abandon_quest":
                        if (player_current_world == latest_unlocked_world)
                            enemies_defeated = 0;
                        ui_panel = UI_PANEL_WORLD_MAP;
                        return TRUE;
                }
                break;
            case UI_PANEL_BATTLE:
                if (!Dm.truthy(player_turn))
                    return TRUE;
                player_turn = FALSE;
                switch (action)
                {
                    case BATTLE_ARCADE_PLAYER_ATTACK:
                        await process_player_attack(gamer, BATTLE_ARCADE_PLAYER_ATTACK);
                        return TRUE;
                    case BATTLE_ARCADE_PLAYER_HEAVY_ATTACK:
                        if (player_current_mp < SPELL_MP_COST)
                        {
                            await say(I18n.T("You don't have enough MP to heavy attack!"));
                            player_turn = TRUE;
                            return TRUE;
                        }
                        player_current_mp -= SPELL_MP_COST;
                        await process_player_attack(gamer, BATTLE_ARCADE_PLAYER_HEAVY_ATTACK);
                        return TRUE;
                    case BATTLE_ARCADE_PLAYER_COUNTERATTACK:
                        if (player_current_mp < SPELL_MP_COST)
                        {
                            await say(I18n.T("You don't have enough MP to counterattack!"));
                            player_turn = TRUE;
                            return TRUE;
                        }
                        player_current_mp -= SPELL_MP_COST;
                        await process_player_attack(gamer, BATTLE_ARCADE_PLAYER_COUNTERATTACK);
                        return TRUE;
                    case BATTLE_ARCADE_PLAYER_DEFEND:
                        player_current_hp = round(min(player_current_hp + (SPELL_MP_COST / 2), PLAYER_MAX_HP), 1);
                        player_current_mp = round(min(player_current_mp + SPELL_MP_COST, PLAYER_MAX_MP), 1);
                        await process_player_attack(gamer, BATTLE_ARCADE_PLAYER_DEFEND);
                        return TRUE;
                    case "flee":
                        //you can't outrun the cuban pete
                        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
                        {
                            await lose_game(gamer);
                            return dot;
                        }
                        player_turn = TRUE;
                        ui_panel = UI_PANEL_WORLD_MAP;
                        if (Dm.truthy(player_gold))
                        {
                            player_gold /= 2;
                            player_gold = max(round(player_gold, 1), 0);
                        }
                        return TRUE;
                }
                //they pressed something but it wasn't in the menu, we'll be nice and give them back their turn anyway.
                player_turn = TRUE;
                break;
        }
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/machinery/computer/arcade/battle.dm:48
    internal DmList obj_machinery_computer_arcade_battle__battle_arcade_gear_list;

    // tg: code/game/machinery/computer/arcade/battle.dm:50
    internal DmList obj_machinery_computer_arcade_battle__all_worlds = Dm.list(
        Dm.kv(BATTLE_WORLD_ONE, 1),
        Dm.kv(BATTLE_WORLD_TWO, 1.25f),
        Dm.kv(BATTLE_WORLD_THREE, 1.5f),
        Dm.kv(BATTLE_WORLD_FOUR, 1.75f),
        Dm.kv(BATTLE_WORLD_FIVE, 2),
        Dm.kv(BATTLE_WORLD_SIX, 2.25f),
        Dm.kv(BATTLE_WORLD_SEVEN, 2.5f),
        Dm.kv(BATTLE_WORLD_EIGHT, 2.75f),
        Dm.kv(BATTLE_WORLD_NINE, 3));

    // tg: code/game/machinery/computer/arcade/battle.dm:61
    internal DmList obj_machinery_computer_arcade_battle__all_attack_types = Dm.list(
        Dm.kv("Attack", "Attack the enemy in a default attack at no MP cost."),
        Dm.kv("Heavy Attack", "Attack the enemy with the power of Magic, costing MP for additional damage."),
        Dm.kv("Counterattack", "Use magic to prepare a counterattack of your enemy, allowing you to deal extra damage if you succeed."),
        Dm.kv("Defend", "Defend from the next incoming attack, lowing the amount of damage you take while restoring some HP and MP."));
}
