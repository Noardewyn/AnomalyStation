// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/robot/robot_defense.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI2, D326). The damage particles (add_shared_particles: smoke and sparks, a client visual) are
// assigned and kept as in tg; the client does not draw BYOND particles yet.

// tg: code/modules/mob/living/silicon/robot/robot_defense.dm:1
public partial class datum_controller_global_vars
{
    // tg: code/modules/mob/living/silicon/robot/robot_defense.dm:1  GLOBAL_LIST_INIT(blacklisted_borg_hats, typecacheof(list(...)))
    public DmList blacklisted_borg_hats;

    // tg: code/modules/mob/living/silicon/robot/robot_defense.dm:1  (GLOBAL_MANAGED(blacklisted_borg_hats, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalblacklisted_borg_hats()
    {
        blacklisted_borg_hats = typecacheof(Dm.list( //Hats that don't really work on borgos
            typeof(obj_item_clothing_head_helmet_space),
            typeof(obj_item_clothing_head_utility_welding),
            typeof(obj_item_clothing_head_chameleon_broken)
            )) as DmList;
        Dm.append(ref gvars_datum_init_order, "blacklisted_borg_hats");
    }
}

// tg: code/modules/mob/living/silicon/robot/robot_defense.dm:7
public partial class mob_living_silicon_robot : mob_living_silicon
{
    // tg: code/modules/mob/living/silicon/robot/robot_defense.dm:7
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(is_wire_tool(tool, check_secured: TRUE)))
        {
            if (Dm.truthy(wiresexposed))
            {
                await wires.interact(user);
                return ITEM_INTERACT_SUCCESS;
            }
            if (Dm.truthy(user.combat_mode))
                return ITEM_INTERACT_SKIP_TO_ATTACK;

            balloon_alert(user, I18n.T("expose the wires first!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (tool is obj_item_stack_cable_coil coil)
        {
            if (!Dm.truthy(wiresexposed))
            {
                balloon_alert(user, I18n.T("expose the wires first!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (Dm.num(get_fire_loss()) <= 0)
            {
                balloon_alert(user, I18n.T("wires are fine!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (this == user)
            {
                balloon_alert(user, I18n.T("repairing self..."));
                if (!Dm.truthy(await do_after(user, 5 * SECONDS, target: this)))
                    return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await coil.use(1)))
            {
                balloon_alert(user, I18n.T("not enough cable!"));
                return ITEM_INTERACT_BLOCKING;
            }
            await adjust_fire_loss(-30);
            playsound(this, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
            balloon_alert(user, I18n.T("wires repaired"));
            user.visible_message(
                span_notice(I18n.T("[] fixes some of the burnt wires on [].", user, this)),
                span_notice(I18n.T("You fix some of the burnt wires on [].", this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE
            );
            user.changeNext_move(CLICK_CD_MELEE);
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_stock_parts_power_store_cell && Dm.truthy(opened)) // trying to put a cell inside
        {
            if (Dm.truthy(wiresexposed))
            {
                balloon_alert(user, I18n.T("unexpose the wires first!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (cell != null)
            {
                balloon_alert(user, I18n.T("already has a cell!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;
            cell = tool;
            balloon_alert(user, I18n.T("cell inserted"));
            await update_icons();
            diag_hud_set_borgcell();
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(Dm.bitand(tool.slot_flags, ITEM_SLOT_HEAD))
            && !Dm.eq(hat_offset, INFINITY)
            && !Dm.truthy(user.combat_mode)
            && !Dm.truthy(is_type_in_typecache(tool, GLOB.blacklisted_borg_hats)))
        {
            if (user == this)
            {
                balloon_alert(user, I18n.T("can't place on self!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (hat != null && HAS_TRAIT(hat, TRAIT_NODROP))
            {
                balloon_alert(user, I18n.T("can't remove existing headwear!"));
                return ITEM_INTERACT_BLOCKING;
            }
            balloon_alert(user, I18n.T("placing on head..."));
            user.visible_message(
                span_notice(I18n.T("[] begins to place [] on []'s head...", user, tool, this)),
                span_notice(I18n.T("You begin to place [] on []'s head...", tool, this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE
            );
            if (!Dm.truthy(await do_after(user, 3 * SECONDS, target: this)))
                return ITEM_INTERACT_BLOCKING;
            if (hat != null && HAS_TRAIT(hat, TRAIT_NODROP))
            {
                balloon_alert(user, I18n.T("can't remove existing headwear!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
                return ITEM_INTERACT_BLOCKING;
            balloon_alert(user, I18n.T("headwear placed"));
            await place_on_head(tool);
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_defibrillator defib && !Dm.truthy(user.combat_mode))
        {
            if (!Dm.truthy(opened))
            {
                balloon_alert(user, I18n.T("chassis cover is closed!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (model is not obj_item_robot_model_medical)
            {
                balloon_alert(user, I18n.T("wrong cyborg model!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (stat == DEAD)
            {
                balloon_alert(user, I18n.T("it's dead!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(Dm.bitand(defib.slot_flags, ITEM_SLOT_BACK))) //belt defibs need not apply
            {
                balloon_alert(user, I18n.T("doesn't fit!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (Dm.truthy(defib.get_cell()))
            {
                balloon_alert(user, I18n.T("remove []'s cell first!", tool));
                return ITEM_INTERACT_BLOCKING;
            }
            if (Dm.locate<obj_item_borg_upgrade_defib>(this) != null)
            {
                balloon_alert(user, I18n.T("already has a defibrillator!"));
                return ITEM_INTERACT_BLOCKING;
            }
            obj_item_borg_upgrade_defib_backpack defib_upgrade = Dm.@new<obj_item_borg_upgrade_defib_backpack>(null, defib);
            if (Dm.truthy(await apply_upgrade(defib_upgrade, user)))
            {
                balloon_alert(user, I18n.T("defibrillator installed"));
                return ITEM_INTERACT_SUCCESS;
            }
            return ITEM_INTERACT_BLOCKING;
        }

        if (tool is obj_item_storage_part_replacer replacer)
        {
            if (!Dm.truthy(opened))
            {
                balloon_alert(user, I18n.T("chassis cover is closed!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (model is not obj_item_robot_model_engineering)
            {
                balloon_alert(user, I18n.T("wrong cyborg model!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (Dm.locate<obj_item_borg_upgrade_rped>(this) != null)
            {
                balloon_alert(user, I18n.T("already has a RPED!"));
                return ITEM_INTERACT_BLOCKING;
            }
            qdel(tool);
            obj_item_borg_upgrade_smallrped lilrped = Dm.@new<obj_item_borg_upgrade_smallrped>();
            if (Dm.truthy(await apply_upgrade(lilrped, user)))
            {
                balloon_alert(user, I18n.T("[] installed", replacer));
                return ITEM_INTERACT_SUCCESS;
            }
            return ITEM_INTERACT_BLOCKING;
        }

        if (tool is obj_item_encryptionkey && Dm.truthy(opened))
        {
            if (radio is obj_item_radio radio__typed)
                return await radio__typed.item_interaction(user, tool);

            balloon_alert(user, I18n.T("no radio found!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (tool is obj_item_borg_upgrade upgrade)
        {
            if (!Dm.truthy(opened))
            {
                balloon_alert(user, I18n.T("chassis cover is closed!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (model == null && Dm.truthy(upgrade.require_model))
            {
                balloon_alert(user, I18n.T("choose a model first!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (Dm.truthy(upgrade.locked))
            {
                balloon_alert(user, I18n.T("upgrade locked!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (Dm.truthy(await apply_upgrade(upgrade, user)))
            {
                balloon_alert(user, I18n.T("upgrade installed"));
                return ITEM_INTERACT_SUCCESS;
            }
            return ITEM_INTERACT_BLOCKING;
        }

        if (tool is obj_item_toner)
        {
            if (toner >= tonermax)
            {
                balloon_alert(user, I18n.T("toner full!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;
            toner = tonermax;
            qdel(tool);
            balloon_alert(user, I18n.T("toner filled"));
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_flashlight && tool is not obj_item_flashlight_emp) //subtypes my behated. OOP was a dumb idea
        {
            if (Dm.truthy(user.combat_mode))
                return NONE;
            if (!Dm.truthy(opened))
            {
                balloon_alert(user, I18n.T("open the chassis cover first!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (Dm.truthy(lamp_functional))
            {
                balloon_alert(user, I18n.T("headlamp already functional!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;
            lamp_functional = TRUE;
            qdel(tool);
            balloon_alert(user, I18n.T("headlamp repaired"));
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_disk_computer disk)
        {
            if (modularInterface == null)
            {
                stack_trace(Dm.text("Cyborg [] ( [] ) was somehow missing their integrated tablet. Please make a bug report.", this, GetType()), "code/modules/mob/living/silicon/robot/robot_defense.dm", 182);
                create_modularInterface();
            }
            return await modularInterface.computer_disk_act(user, disk);
        }

        return NONE;
    }

    //Checks blockchance of any items in any module slots
    // tg: code/modules/mob/living/silicon/robot/robot_defense.dm:189
    public override async DmTask<object> check_block(atom hit_by = null, object damage = null, string attack_text = null, float? attack_type = null, float? armour_penetration = null, string damage_type = null, params object[] __extra)
    {
        attack_text ??= "the attack";
        attack_type ??= MELEE_ATTACK;
        armour_penetration ??= 0;
        damage_type ??= BRUTE;
        object dot = await base.check_block(hit_by, damage, attack_text, attack_type, armour_penetration, damage_type, __extra);
        if (Dm.eq(dot, SUCCESSFUL_BLOCK))
            return SUCCESSFUL_BLOCK;

        float block_chance_modifier = round(Dm.num(damage) / -3);
        foreach (obj_item module in Dm.each<obj_item>(held_items))
        {
            float final_block_chance = Dm.num(module.block_chance) - clamp((Dm.num(armour_penetration) - Dm.num(module.armour_penetration)) / 2, 0, 100) + block_chance_modifier;
            if (Dm.truthy(await module.hit_reaction(this as object as mob_living_carbon_human, hit_by as atom_movable, attack_text, final_block_chance, Dm.num(damage), attack_type, damage_type)))
                return SUCCESSFUL_BLOCK;
        }
        return dot;
    }

    // This has to go at the very end of interaction so we don't block every interaction with ID-like items
    // tg: code/modules/mob/living/silicon/robot/robot_defense.dm:201
    public override async DmTask<object> base_item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        object dot = await base.base_item_interaction(user, tool, modifiers);
        if (Dm.truthy(dot) || !Dm.truthy(tool.GetID()))
            return dot;
        if (Dm.truthy(opened))
        {
            balloon_alert(user, I18n.T("close the chassis cover first!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await allowed__proc(user)))
        {
            balloon_alert(user, I18n.T("access denied!"));
            return ITEM_INTERACT_BLOCKING;
        }
        await toggle_cover(user);
        return ITEM_INTERACT_SUCCESS;
    }

    private const float LOW_DAMAGE_UPPER_BOUND = 1f / 3;
    private const float MODERATE_DAMAGE_UPPER_BOUND = 2f / 3;

    // tg: code/modules/mob/living/silicon/robot/robot_defense.dm:217
    public virtual void update_damage_particles()
    {
        float brute_percent = Dm.num(bruteloss) / Dm.num(maxHealth);
        float burn_percent = Dm.num(fireloss) / Dm.num(maxHealth);

        Type old_smoke = smoke_particles;
        if (brute_percent > MODERATE_DAMAGE_UPPER_BOUND)
            smoke_particles = typeof(particles_smoke_cyborg_heavy_damage);
        else if (brute_percent > LOW_DAMAGE_UPPER_BOUND)
            smoke_particles = typeof(particles_smoke_cyborg);
        else
            smoke_particles = null;

        if (old_smoke != smoke_particles)
        {
            if (old_smoke != null)
                remove_shared_particles(old_smoke);
            if (smoke_particles != null)
                add_shared_particles(smoke_particles);
        }

        Type old_sparks = spark_particles;
        if (burn_percent > MODERATE_DAMAGE_UPPER_BOUND)
            spark_particles = typeof(particles_embers_spark_severe);
        else if (burn_percent > LOW_DAMAGE_UPPER_BOUND)
            spark_particles = typeof(particles_embers_spark);
        else
            spark_particles = null;

        if (old_sparks != spark_particles)
        {
            if (old_sparks != null)
                remove_shared_particles(old_sparks);
            if (spark_particles != null)
                add_shared_particles(spark_particles);
        }
    }

    // tg: code/modules/mob/living/silicon/robot/robot_defense.dm:253
    public override async DmTask<object> attack_alien(mob_living_carbon user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
            return await base.attack_alien(user, modifiers);
        if (!Dm.eq(body_position, STANDING_UP))
            return null;
        await user.do_attack_animation(this, ATTACK_EFFECT_DISARM);
        obj_item I = get_active_held_item() as obj_item;
        if (I != null)
        {
            await uneq_active();
            visible_message(span_danger(I18n.T("[] disarmed []!", user, this)),
                span_userdanger(I18n.T("[] has disabled []'s active module!", user, this)), null, COMBAT_MESSAGE_RANGE);
            log_combat(user, this, "disarmed", Dm.text("[]", I != null ? Dm.text(" removing \\the []", I) : ""));
        }
        else
        {
            await Stun(40);
            await step(this, get_dir(user, this));
            visible_message(span_danger(I18n.T("[] forces back []!", user, this)),
                span_userdanger(I18n.T("[] forces you back!", user)), null, COMBAT_MESSAGE_RANGE);
            log_combat(user, this, "pushed");
        }
        playsound(loc, Dm.resource("sound/items/weapons/pierce.ogg"), 50, TRUE, -1);
        return null;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_defense.dm:273
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        add_fingerprint(user);
        if (!Dm.truthy(opened))
            return await base.attack_hand(user, modifiers, __extra);
        if (!Dm.truthy(wiresexposed) && !issilicon(user))
        {
            if (cell == null)
                return null;
            cell__obj.add_fingerprint(user);
            balloon_alert(user, I18n.T("cell removed"));
            await user.put_in_active_hand(cell__obj);
            await update_icons();
            diag_hud_set_borgcell();
        }
        return null;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_defense.dm:286
    public override async DmTask<object> attack_hulk(mob_living_carbon_human user = null, params object[] __extra)
    {
        object dot = await base.attack_hulk(user, __extra);
        if (!Dm.truthy(dot))
            return dot;
        await spark_system.start();
        await step_away(this, user, 15);
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, nameof(_step_away), this, get_turf(user), 15), 0.3f * SECONDS, file: "code/modules/mob/living/silicon/robot/robot_defense.dm", line: 292);
        return dot;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_defense.dm:294
    public override object get_shove_flags(mob_living shover = null, obj_item weapon = null)
    {
        object dot = base.get_shove_flags(shover, weapon);
        if (weapon == null || IS_UNCONSCIOUS_OR_CRIT(this))
            dot = Dm.bitand(dot, Dm.bitnot(Dm.bitor(SHOVE_CAN_MOVE, SHOVE_CAN_HIT_SOMETHING)));
        return dot;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_defense.dm:299
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.combat_mode) && user != this)
            return NONE;

        user.changeNext_move(CLICK_CD_MELEE);
        if (!Dm.truthy(get_brute_loss()))
        {
            balloon_alert(user, I18n.T("no dents to fix!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(tool.tool_start_check(user, amount: 1, heat_required: HIGH_TEMPERATURE_REQUIRED))) //The welder has 1u of fuel consumed by its afterattack, so we don't need to worry about taking any away.
            return ITEM_INTERACT_BLOCKING;
        if (this == user)
        {
            balloon_alert(user, I18n.T("repairing self..."));
            if (!Dm.truthy(await tool.use_tool(this, user, delay: 5 * SECONDS, amount: 1, volume: 50)))
                return ITEM_INTERACT_BLOCKING;
        }
        else
        {
            if (!Dm.truthy(await tool.use_tool(this, user, delay: 0.5f * SECONDS, amount: 1, volume: 50)))
                return ITEM_INTERACT_BLOCKING;
        }

        await adjust_brute_loss(-30);
        add_fingerprint(user);
        balloon_alert(user, I18n.T("dents fixed"));
        user.visible_message(
            span_notice(I18n.T("[] fixes some of the dents on [].", user, this)),
            span_notice(I18n.T("You fix some of the dents on [].", this)),
            visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE
        );
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_defense.dm:327
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(opened))
        {
            balloon_alert(user, I18n.T("chassis cover closed"));
            opened = FALSE;
            await update_icons();
        }
        else
        {
            if (Dm.truthy(locked))
                balloon_alert(user, I18n.T("chassis cover locked!"));
            else
            {
                balloon_alert(user, I18n.T("chassis cover opened"));
                opened = TRUE;
                await update_icons();
            }
        }

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_defense.dm:342
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(opened))
            return NONE;
        if (cell == null) // haxing
        {
            wiresexposed = Dm.not(wiresexposed);
            balloon_alert(user, Dm.truthy(wiresexposed) ? I18n.T("wires exposed") : I18n.T("wires unexposed"));
        }
        else // radio
        {
            if (Dm.truthy(shell))
                balloon_alert(user, I18n.T("can't access radio!")); // Prevent AI radio key theft
            else if (radio is obj_item_radio radio__typed)
                await radio__typed.screwdriver_act(user, tool); // Push it to the radio to let it handle everything
            else
            {
                to_chat(user, span_warning(I18n.T("Unable to locate a radio!")));
                balloon_alert(user, I18n.T("no radio found!"));
            }
        }
        await update_icons();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_defense.dm:359
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (!(Dm.truthy(opened) && cell == null)) // Deconstruction. The flashes break from the fall, to prevent this from being a ghetto reset module.
            return NONE;
        if (!Dm.truthy(lockcharge))
        {
            to_chat(user, span_warning(I18n.T("[]'s bolts spark! Maybe you should lock them down first!", this)));
            await spark_system.start();
            return ITEM_INTERACT_BLOCKING;
        }
        balloon_alert(user, I18n.T("deconstructing..."));
        if (!Dm.truthy(await tool.use_tool(this, user, 5 * SECONDS, volume: 50)) && cell == null)
            return ITEM_INTERACT_BLOCKING;
        loc.balloon_alert(user, I18n.T("deconstructed"));
        user.visible_message(
            span_notice(I18n.T("[] deconstructs []!", user, this)),
            span_notice(I18n.T("You unfasten the securing bolts, and [] falls to pieces!", this)),
            visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE
        );
        await cyborg_deconstruct();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_defense.dm:378
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        if (!Dm.truthy(on_fire)) //Silicons don't gain stacks from hotspots, but hotspots can ignite them
            ignite_mob();
        return null;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_defense.dm:382
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        switch (Dm.num(severity))
        {
            case EMP_HEAVY:
                await Unconscious(16 * SECONDS);
                break;
            case EMP_LIGHT:
                await Unconscious(6 * SECONDS);
                break;
        }
        return dot;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_defense.dm:392
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (user == this) //To prevent syndieborgs from emagging themselves
            return FALSE;
        if (!Dm.truthy(opened)) //Cover is closed
        {
            if (Dm.truthy(locked))
            {
                balloon_alert(user, I18n.T("cover lock destroyed"));
                locked = FALSE;
                if (Dm.truthy(shell)) //A warning to Traitors who may not know that emagging AI shells does not slave them.
                {
                    balloon_alert(user, I18n.T("shells cannot be subverted!"));
                    to_chat(user, span_boldwarning(I18n.T("[] seems to be controlled remotely! Emagging the interface may not work as expected.", this)));
                }
                return TRUE;
            }
            else
            {
                balloon_alert(user, I18n.T("cover already unlocked!"));
                return FALSE;
            }
        }
        if (world.time < emag_cooldown)
            return FALSE;
        if (Dm.truthy(wiresexposed))
        {
            balloon_alert(user, I18n.T("expose the fires first!"));
            return FALSE;
        }

        balloon_alert(user, I18n.T("interface hacked"));
        emag_cooldown = world.time + 100;

        if (connected_ai != null && connected_ai.mind != null && Dm.truthy(connected_ai.mind.has_antag_datum(typeof(datum_antagonist_malf_ai))))
        {
            to_chat(this, span_danger(I18n.T("ALERT: Foreign software execution prevented.")));
            await logevent("ALERT: Foreign software execution prevented.");
            to_chat(connected_ai, span_danger(I18n.T("ALERT: Cyborg unit \\[[]\\] successfully defended against subversion.", this)));
            log_silicon(Dm.text("EMAG: [] attempted to emag cyborg [], but they were slaved to traitor AI [].", key_name(user), key_name(this), connected_ai));
            return TRUE; // emag succeeded, it was just counteracted
        }

        if (Dm.truthy(shell)) //AI shells cannot be emagged, so we try to make it look like a standard reset. Smart players may see through this, however.
        {
            to_chat(user, span_danger(I18n.T("[] is remotely controlled! Your emag attempt has triggered a system reset instead!", this)));
            log_silicon(Dm.text("EMAG: [] attempted to emag an AI shell belonging to []. The shell has been reset as a result.", key_name(user), Dm.truthy(key_name(this)) ? key_name(this) : connected_ai));
            await ResetModel();
            return TRUE;
        }

        await SetEmagged(TRUE);
        await Paralyze(10 * SECONDS); //Make cyborgs actually unconscious. Without this they can scream for help over the radio mid-emag, which doesn't make much sense
        await SetStun(10 * SECONDS); //Borgs were getting into trouble because they would attack the emagger before the new laws were shown
        lawupdate = FALSE;
        await set_connected_ai(null);
        message_admins(Dm.text("[] emagged cyborg [].  Laws overridden.", ADMIN_LOOKUPFLW(user), ADMIN_LOOKUPFLW(this)));
        log_silicon(Dm.text("EMAG: [] emagged cyborg []. Laws overridden.", key_name(user), key_name(this)));
        log_law_change(user as mob_living, Dm.text("emagged []", key_name(this)));

        await model.rebuild_modules();

        Waiting.Detach(borg_emag_end(user)); // INVOKE_ASYNC
        return TRUE;
    }

    /// A async proc called from [emag_act] that gives the borg a lot of flavortext, and applies the syndicate lawset after a delay.
    // tg: code/modules/mob/living/silicon/robot/robot_defense.dm:443
    public virtual async DmTask borg_emag_end(mob user = null)
    {
        to_chat(this, span_danger(I18n.T("ALERT: Foreign software detected.")));
        await logevent("ALERT: Foreign software detected.");
        await sleep(0.5f * SECONDS);
        to_chat(this, span_danger(I18n.T("Initiating diagnostics...")));
        await sleep(2 * SECONDS);
        to_chat(this, span_danger(I18n.T("SynBorg v1.7 loaded.")));
        await logevent(Dm.text("WARN: root privleges granted to PID [][].", num2hex(rand(1, 65535), -1), num2hex(rand(1, 65535), -1))); //random eight digit hex value. Two are used because rand(1,4294967295) throws an error
        await sleep(0.5f * SECONDS);
        to_chat(this, span_danger(I18n.T("LAW SYNCHRONISATION ERROR")));
        await sleep(0.5f * SECONDS);
        if (user != null)
            await logevent(Dm.text("LOG: New user \\[[]\\], groups \\[root\\]", replacetext(user.real_name, " ", "")));
        to_chat(this, span_danger(I18n.T("Would you like to send a report to NanoTraSoft? Y/N")));
        await sleep(1 * SECONDS);
        to_chat(this, span_danger(I18n.T("> N")));
        await sleep(2 * SECONDS);
        to_chat(this, span_danger(I18n.T("ERRORERRORERROR")));
        await replace_law_set(typeof(datum_ai_laws_syndicate_override));
        if (user != null)
        {
            to_chat(this, span_danger(I18n.T("ALERT: [] is your new master. Obey your new laws and [] commands.", user.real_name, user.p_their())));
            laws.set_zeroth_law(I18n.T("Only [] and people [] designate[] as being such are Syndicate Agents.", user.real_name, user.p_they(), user.p_s()), force: TRUE);
            laws.protected_zeroth = TRUE;
        }
        await update_icons();
    }

    // tg: code/modules/mob/living/silicon/robot/robot_defense.dm:468
    public override async DmTask<object> blob_act(obj_structure_blob B = null)
    {
        if (stat != DEAD)
            await adjust_brute_loss(30);
        else
        {
            investigate_log("has been gibbed by a blob.", INVESTIGATE_DEATHS);
            await gib(DROP_ALL_REMAINS);
        }
        return TRUE;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_defense.dm:476
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        switch (Dm.num(severity))
        {
            case EXPLODE_DEVASTATE:
                investigate_log("has been gibbed by an explosion.", INVESTIGATE_DEATHS);
                Waiting.NoSleep(gib(DROP_ALL_REMAINS));
                return TRUE;
            case EXPLODE_HEAVY:
                if (stat != DEAD)
                {
                    Waiting.NoSleep(adjust_brute_loss(60));
                    Waiting.NoSleep(adjust_fire_loss(60));
                }
                break;
            case EXPLODE_LIGHT:
                if (stat != DEAD)
                    Waiting.NoSleep(adjust_brute_loss(30));
                break;
        }

        return TRUE;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_defense.dm:492
    public override async DmTask<object> bullet_act(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, float? blocked = null)
    {
        piercing_hit ??= FALSE;
        if (Dm.truthy(await check_block(hitting_projectile, hitting_projectile.damage, Dm.text("\\the []", hitting_projectile), PROJECTILE_ATTACK, Dm.num(hitting_projectile.armour_penetration), hitting_projectile.damage_type)))
            return await base.bullet_act(hitting_projectile, def_zone, piercing_hit, 100);
        object dot = await base.bullet_act(hitting_projectile, def_zone, piercing_hit, blocked);
        if (prob(25) || !Dm.eq(dot, BULLET_ACT_HIT))
            return dot;
        if (hitting_projectile.damage_type != BRUTE && hitting_projectile.damage_type != BURN)
            return dot;
        if (!Dm.truthy(hitting_projectile.is_hostile_projectile()) || Dm.num(hitting_projectile.damage) <= 0)
            return dot;
        await spark_system.start();
        return dot;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_defense.dm:504
    public override async DmTask<object> attack_effects(object damage_done = null, object hit_zone = null, object armor_block = null, obj_item attacking_item = null, mob_living attacker = null)
    {
        object dot = null;
        if (Dm.num(damage_done) > 0 && attacking_item.damtype != STAMINA && stat != DEAD)
        {
            await spark_system.start();
            dot = TRUE;
        }
        object parent = await base.attack_effects(damage_done, hit_zone, armor_block, attacking_item, attacker);
        return Dm.truthy(parent) ? parent : dot;
    }
}

// tg's robot_defense.dm:510 declares /mob/living/silicon/robot/apply_damage/apply_damage(...): a subtype named apply_damage with
// the override, not an override on the cyborg itself, so the shield module's absorption never reaches a real cyborg (kept as in tg).
// tg: code/modules/mob/living/silicon/robot/robot_defense.dm:510
public partial class mob_living_silicon_robot_apply_damage : mob_living_silicon_robot
{
    // tg: code/modules/mob/living/silicon/robot/robot_defense.dm:510
    public override async DmTask<object> apply_damage(float? damage = null, string damagetype = null, object def_zone = null, float? blocked = null, float? forced = null, float? spread_damage = null, float? wound_bonus = null, float? exposed_wound_bonus = null, float? sharpness = null, object attack_direction = null, object attacking_item = null, float? wound_clothing = null)
    {
        damage ??= 0;
        damagetype ??= BRUTE;
        blocked ??= 0;
        forced ??= FALSE;
        spread_damage ??= FALSE;
        wound_bonus ??= 0;
        exposed_wound_bonus ??= 0;
        sharpness ??= NONE;
        wound_clothing ??= TRUE;
        mob_living_silicon_robot borg = this;
        obj_item_shield_module shield = Dm.locate<obj_item_shield_module>(borg);
        if (shield == null)
            return await base.apply_damage(damage, damagetype, def_zone, blocked, forced, spread_damage, wound_bonus, exposed_wound_bonus, sharpness, attack_direction, attacking_item, wound_clothing);
        obj_item_stock_parts_power_store borg_cell = borg.cell as obj_item_stock_parts_power_store;
        if (Dm.num(borg_cell.charge) <= 0.4f * STANDARD_CELL_CHARGE)
        {
            balloon_alert(borg, I18n.T("not enough energy!"));
            if (Dm.truthy(shield.active))
            {
                shield.active = FALSE;
                playsound(this, Dm.resource("sound/vehicles/mecha/mech_shield_drop.ogg"), 50, FALSE);
                borg.cut_overlay(shield.shield_overlay);
                return null;
            }
        }
        if (shield != null && Dm.truthy(shield.active))
        {
            if (!Dm.truthy(lavaland_equipment_pressure_check(get_turf(borg))))
            {
                balloon_alert(borg, I18n.T("the shield didn't absorb the damage!"));
                return await base.apply_damage(damage, damagetype, def_zone, blocked, forced, spread_damage, wound_bonus, exposed_wound_bonus, sharpness, attack_direction, attacking_item, wound_clothing);
            }
            playsound(this, Dm.resource("sound/vehicles/mecha/mech_shield_deflect.ogg"), 100, TRUE);
            balloon_alert(borg, I18n.T("absorbed!"));
            await borg_cell.use(damage * (STANDARD_CELL_CHARGE / 15), Dm.named("force", TRUE));
            damage *= 0.5f;
        }
        return await base.apply_damage(damage, damagetype, def_zone, blocked, forced, spread_damage, wound_bonus, exposed_wound_bonus, sharpness, attack_direction, attacking_item, wound_clothing);
    }
}
