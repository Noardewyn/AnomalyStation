// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/laserpointer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps J3, D298). The var blocks are generated.

// tg: code/game/objects/items/devices/laserpointer.dm:2
public partial class obj_item_laser_pointer : obj_item
{
    // tg: code/game/objects/items/devices/laserpointer.dm:44
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        diode = Dm.@new<obj_item_stock_parts_micro_laser>(this);
        if (!Dm.truthy(pointer_icon_state))
            pointer_icon_state = Dm.str(pick(Dm.list("red_laser", "green_laser", "blue_laser", "purple_laser")));
        return dot;
    }

    // tg: code/game/objects/items/devices/laserpointer.dm:50
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref crystal_lens);
        QDEL_NULL(ref diode);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/devices/laserpointer.dm:55
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == crystal_lens)
            crystal_lens = null;
        if (gone == diode)
            diode = null;
        return dot;
    }

    // tg: code/game/objects/items/devices/laserpointer.dm:75
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (diode != null)
        {
            tool.play_tool_sound(this);
            balloon_alert(user, I18n.T("removed diode"));
            await diode.forceMove(drop_location() as atom);
            diode = null;
            return TRUE;
        }
        return null;
    }

    // tg: code/game/objects/items/devices/laserpointer.dm:83
    public override async DmTask<object> tool_act(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (crystal_lens == null)
            return await base.tool_act(user, tool, modifiers);
        if (!Dm.eq(tool_behaviour, TOOL_WIRECUTTER) && !Dm.eq(tool_behaviour, TOOL_HEMOSTAT))
            return await base.tool_act(user, tool, modifiers);
        tool.play_tool_sound(this);
        balloon_alert(user, I18n.T("removed crystal lens"));
        await crystal_lens.forceMove(drop_location() as atom);
        crystal_lens = null;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/devices/laserpointer.dm:94
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_stock_parts_micro_laser)
        {
            if (diode != null)
            {
                balloon_alert(user, I18n.T("already has a diode!"));
                return ITEM_INTERACT_BLOCKING;
            }
            obj_item_stock_parts attack_diode = (obj_item_stock_parts)tool;
            if (crystal_lens != null && attack_diode.rating < 3) //only tier 3 and up are small enough to fit
            {
                to_chat(user, span_warning(I18n.T("You try to jam \\the [] in place, but \\the [] is in the way!", tool.name, crystal_lens.name)));
                playsound(this, Dm.resource("sound/machines/airlock/airlock_alien_prying.ogg"), 20);
                if (!Dm.truthy(await do_after(user, 2 * SECONDS, this)))
                    return ITEM_INTERACT_BLOCKING;
                atom atom_to_teleport = pick(Dm.list(user, tool)) as atom;
                if (atom_to_teleport == user)
                {
                    to_chat(user, span_warning(I18n.T("You jam \\the [] in too hard and break \\the [] inside, teleporting you away!", tool.name, crystal_lens.name)));
                    await user.drop_all_held_items();
                }
                else if (atom_to_teleport == tool)
                {
                    await tool.forceMove(drop_location() as atom);
                    to_chat(user, span_warning(I18n.T("You jam \\the [] in too hard and break \\the [] inside, teleporting \\the [] away!", tool.name, crystal_lens.name, tool.name)));
                }
                // do_teleport(atom_to_teleport, get_turf(src), crystal_lens.blink_range, asoundin = 'sound/effects/phasein.ogg', channel = TELEPORT_CHANNEL_BLUESPACE)
                Dm.Deferred("code/game/objects/items/devices/laserpointer.dm:112", "item_interaction: do_teleport, the jammed lens's blink (the teleport procs, datums/helper_datums/teleport.dm, not ported)");
                qdel(crystal_lens);
                return ITEM_INTERACT_SUCCESS;
            }
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;
            playsound(this, Dm.resource("sound/items/tools/screwdriver.ogg"), 30);
            diode = (obj_item_stock_parts_micro_laser)tool;
            balloon_alert(user, I18n.T("installed \\the []", diode.name));
            //we have a diode now, try starting a charge sequence in case the pointer was charging when we took out the diode
            recharging = TRUE;
            START_PROCESSING(SSobj, this);
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_stack_ore_bluespace_crystal)
        {
            if (crystal_lens != null)
            {
                balloon_alert(user, I18n.T("already has a lens!"));
                return ITEM_INTERACT_BLOCKING;
            }
            //the crystal stack we're trying to install a crystal from
            obj_item_stack_ore_bluespace_crystal crystal_stack = (obj_item_stack_ore_bluespace_crystal)tool;
            if (diode != null && diode.rating < 3) //only lasers of tier 3 and up can house a lens
            {
                to_chat(user, span_warning(I18n.T("You try to jam \\the [] in front of the diode, but it's a bad fit!", crystal_stack.name)));
                playsound(this, Dm.resource("sound/machines/airlock/airlock_alien_prying.ogg"), 20);
                if (!Dm.truthy(await do_after(user, 2 * SECONDS, this)))
                    return ITEM_INTERACT_BLOCKING;
                atom atom_to_teleport = pick(Dm.list(user, this)) as atom;
                if (atom_to_teleport == user)
                {
                    to_chat(user, span_warning(I18n.T("You press on \\the [] too hard and are teleported away!", crystal_stack.name)));
                    await user.drop_all_held_items();
                }
                else if (atom_to_teleport == this)
                {
                    await forceMove(drop_location() as atom);
                    to_chat(user, span_warning(I18n.T("You press on \\the [] too hard and \\the [] is teleported away!", crystal_stack.name, this)));
                }
                // do_teleport(atom_to_teleport, get_turf(src), crystal_stack.blink_range, asoundin = 'sound/effects/phasein.ogg', channel = TELEPORT_CHANNEL_BLUESPACE)
                Dm.Deferred("code/game/objects/items/devices/laserpointer.dm:143", "item_interaction: do_teleport, the jammed crystal's blink (the teleport procs, datums/helper_datums/teleport.dm, not ported)");
                await crystal_stack.use_tool(this, user, amount: 1); //use only one if we were installing from a stack of crystals
                return ITEM_INTERACT_SUCCESS;
            }
            //the single crystal that we actually install
            obj_item_stack_ore_bluespace_crystal single_crystal = await crystal_stack.split_stack(1) as obj_item_stack_ore_bluespace_crystal;
            if (single_crystal == null)
                return ITEM_INTERACT_BLOCKING;
            await single_crystal.forceMove(this);
            crystal_lens = single_crystal;
            playsound(this, Dm.resource("sound/items/tools/screwdriver2.ogg"), 30);
            balloon_alert(user, I18n.T("installed \\the []", crystal_lens.name));
            to_chat(user, span_notice(I18n.T("You install a [] in []. It can now be used to shine through obstacles at the cost of double the energy drain.", crystal_lens.name, this)));
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/objects/items/devices/laserpointer.dm:160
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (in_range(user, this) || isobserver(user))
        {
            if (diode == null)
                dot.Add(span_notice(I18n.T("The diode is missing.")));
            else
                dot.Add(span_notice(I18n.T("A class <b>[]</b> laser diode is installed. It is <i>screwed</i> in place.", diode.rating)));
            dot.Add(span_notice(I18n.T("A small display reads out that[] there is <b>[]%</b> total charge remaining.", Dm.truthy(recharge_locked) ? I18n.T(" it is currently recharging to full, and") : "", energy * 10)));
            if (crystal_lens != null)
                dot.Add(span_notice(I18n.T("There is a <b>[]</b> fit neatly before the focus lens. It can be <i>plucked out</i> with some <i>wirecutters</i>.", crystal_lens.name)));
            else if (diode != null) //hint at the ability to modify the pointer with a crystal only if we have a diode
                dot.Add(span_notice(I18n.T("<i>You could examine it more thoroughly...</i>")));
        }
        return dot;
    }

    // tg: code/game/objects/items/devices/laserpointer.dm:173
    public override async DmTask<object> examine_more(mob user = null)
    {
        DmList dot = await base.examine_more(user) as DmList;
        if (crystal_lens != null || diode == null)
            return dot;
        float rating = diode.rating.GetValueOrDefault();
        if (rating == 1)
            dot.Add(I18n.T("<i>\\The [] is fit neatly into the casing.</i>", diode.name));
        else if (rating == 2)
            dot.Add(I18n.T("<i>\\The [] is secured in place, with a little bit of room left between it and the focus lens.</i>", diode.name));
        else if (rating >= 3 && rating <= 4)
            dot.Add(I18n.T("<i>\\The []'s size is much smaller compared to the previous generation lasers, and the wide margin between it and the focus lens could probably house <b>a crystal</b> of some sort.</i>", diode.name));
        return dot;
    }

    // tg: code/game/objects/items/devices/laserpointer.dm:186
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        await laser_act(interacting_with, user, modifiers);
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/game/objects/items/devices/laserpointer.dm:190
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (HAS_TRAIT(interacting_with, TRAIT_COMBAT_MODE_SKIP_INTERACTION))
            return NONE;
        return await ranged_interact_with_atom(interacting_with, user, modifiers);
    }

    ///Handles shining the clicked atom,
    // tg: code/game/objects/items/devices/laserpointer.dm:196
    public virtual async DmTask laser_act(atom target = null, mob_living user = null, DmList modifiers = null)
    {
        if (diode == null)
        {
            to_chat(user, span_notice(I18n.T("You point [] at [], but nothing happens!", this, target)));
            return;
        }
        if (!ISADVANCEDTOOLUSER(user))
        {
            to_chat(user, span_warning(I18n.T("You don't have the dexterity to do this!")));
            return;
        }
        if (HAS_TRAIT(user, TRAIT_CHUNKYFINGERS))
        {
            to_chat(user, span_warning(I18n.T("Your fingers can't press the button!")));
            return;
        }

        if (max_range != INFINITY)
        {
            if (!IN_GIVEN_RANGE(target, user, max_range.GetValueOrDefault()))
            {
                to_chat(user, span_warning(I18n.T("\\The [] is too far away!", target)));
                return;
            }
            if (!Dm.@in(user, view(max_range, target))) //check if we are visible from the target's PoV
            {
                if (crystal_lens == null)
                {
                    to_chat(user, span_warning(I18n.T("You can't point with [] through walls!", this)));
                    return;
                }
                if (!(Dm.truthy(Dm.bitand(user.sight, SEE_OBJS)) || Dm.truthy(Dm.bitand(user.sight, SEE_MOBS)))) //only let it work if we have xray or thermals. mesons don't count because they are easier to get.
                {
                    to_chat(user, span_notice(I18n.T("You can't quite make out your target and you fail to shine at it.")));
                    return;
                }
            }
        }

        add_fingerprint(user);

        //nothing happens if the battery has been drained and has not fully recharged yet
        if (Dm.truthy(recharge_locked))
        {
            to_chat(user, span_notice(I18n.T("You point [] at [], but it's still charging.", this, target)));
            return;
        }

        //The message we send to the user upon using the pointer
        object outmsg = null;
        //The turf of the target we clicked on
        turf targloc = get_turf(target);
        float rating = diode.rating.GetValueOrDefault();

        //human/alien mobs: if we aim for the eyes, chance to flash the target
        if (iscarbon(target))
        {
            mob_living_carbon target_humanoid = (mob_living_carbon)target;
            if (target_humanoid.stat == DEAD)
                outmsg = span_notice(I18n.T("You point [] at [], but [] appear[] to be dead!", this, target_humanoid, target_humanoid.p_they(), target_humanoid.p_s()));
            else if (Dm.eq(user.zone_selected, BODY_ZONE_PRECISE_EYES))
            {
                //Intensity of the laser dot to pass to flash_act
                float severity = Dm.num(pick(Dm.list(0, 1, 2)));
                bool always_fail = false;
                if (istype(target_humanoid.get_item_by_slot(ITEM_SLOT_EYES), typeof(obj_item_clothing_glasses_eyepatch)) && prob(50))
                    always_fail = true;

                //chance to actually hit the eyes depends on internal component
                if (prob(effectchance * rating) && !always_fail && Dm.truthy(await target_humanoid.flash_act(severity)))
                {
                    outmsg = span_notice(I18n.T("You blind [] by shining [] in [] eyes.", target_humanoid, this, target_humanoid.p_their()));
                    log_combat(user, target_humanoid, "blinded with a laser pointer", this);
                }
                else
                {
                    outmsg = span_warning(I18n.T("You fail to blind [] by shining [] at [] eyes!", target_humanoid, this, target_humanoid.p_their()));
                    log_combat(user, target_humanoid, "attempted to blind with a laser pointer", this);
                }
            }
        }
        //borgs: chance to flash and paralyse the target
        else if (iscyborg(target))
        {
            mob_living_silicon target_sillycone = (mob_living_silicon)target;
            //chance to actually hit the eyes depends on internal component
            if (target_sillycone.stat == DEAD)
                outmsg = span_notice(I18n.T("You point [] at [], but [] appear[] to be non-functioning.", this, target_sillycone, target_sillycone.p_they(), target_sillycone.p_s()));
            if (prob(effectchance * rating) && Dm.truthy(await target_sillycone.flash_act(affect_silicon: TRUE)))
            {
                await target_sillycone.set_temp_blindness_if_lower(5 * SECONDS);
                to_chat(target_sillycone, span_danger(I18n.T("Your sensors were overloaded by a laser!")));
                outmsg = span_notice(I18n.T("You overload [] by shining [] at [] sensors.", target_sillycone, this, target_sillycone.p_their()));
                log_combat(user, target_sillycone, "shone in the sensors", this);
            }
            else
            {
                outmsg = span_warning(I18n.T("You fail to overload [] by shining [] at [] sensors!", target_sillycone, this, target_sillycone.p_their()));
                log_combat(user, target_sillycone, "attempted to shine in the sensors", this);
            }
        }
        //cameras: chance to EMP the camera
        else if (target is obj_machinery_camera)
        {
            obj_machinery_camera target_camera = (obj_machinery_camera)target;
            if (!Dm.truthy(target_camera.camera_enabled) && !Dm.truthy(target_camera.emped))
                outmsg = span_notice(I18n.T("You point [] at [], but it seems to be disabled.", this, target_camera));
            else if (prob(effectchance * rating))
            {
                await target_camera.emp_act(EMP_HEAVY);
                outmsg = span_notice(I18n.T("You hit the lens of [] with [], temporarily disabling the camera!", target_camera, this));
                log_combat(user, target_camera, "EMPed", this);
            }
            else
                outmsg = span_warning(I18n.T("You miss the lens of [] with []!", target_camera, this));
        }

        //Make any mob with catlike instincts near the target face the target, with a chance of pouncing (moving) toward it.
        foreach (mob_living target_cat in Dm.each<mob_living>(view(1, targloc)))
        {
            if (!HAS_TRAIT(target_cat, TRAIT_CATLIKE_INSTINCT) || Dm.truthy(target_cat.is_blind()) || Dm.truthy(target_cat.incapacitated))
                continue;
            await target_cat.setDir(get_dir(target_cat, targloc)); // kitty always looks at the light
            if (target_cat.body_position == STANDING_UP)
            {
                if (prob(effectchance * rating))
                {
                    target_cat.visible_message(span_warning(I18n.T("[] makes a grab for the light!", target_cat)), span_userdanger(I18n.T("LIGHT!")));
                    await target_cat.Move(targloc, get_dir(target_cat, targloc));
                    log_combat(user, target_cat, "moved with a laser pointer", this);
                }
                else
                    target_cat.visible_message(span_notice(I18n.T("[] looks briefly distracted by the light.", target_cat)), span_warning(I18n.T("You're briefly tempted by the shiny light...")));
            }
            else
                target_cat.visible_message(span_notice(I18n.T("[] stares at the light.", target_cat)), span_warning(I18n.T("You stare at the light...")));
        }
        //The pointer is shining, change its sprite to show
        icon_state = Dm.text("pointer_[]", pointer_icon_state);

        //setup pointer blip
        mutable_appearance laser = (mutable_appearance)mutable_appearance(Dm.resource("icons/obj/weapons/guns/projectiles.dmi"), Dm.str(pointer_icon_state));
        if (modifiers != null)
        {
            if (Dm.truthy(modifiers[ICON_X]))
                laser.pixel_w = Dm.num(text2num(modifiers[ICON_X])) - 16;
            if (Dm.truthy(modifiers[ICON_Y]))
                laser.pixel_z = Dm.num(text2num(modifiers[ICON_Y])) - 16;
        }
        else
        {
            laser.pixel_w = target.pixel_w + rand(-5, 5);
            laser.pixel_z = target.pixel_z + rand(-5, 5);
        }

        if (Dm.truthy(outmsg))
            to_chat(user, outmsg);
        else
            to_chat(user, span_info(I18n.T("You point [] at [].", this, target)));

        //we have successfully shone our pointer, reduce our battery depending on whether we have an extra lens or not
        energy -= crystal_lens != null ? 2 : 1;
        if (energy <= max_energy) //normal recharge, does not stop us from using the pointer
        {
            if (!Dm.truthy(recharging))
            {
                recharging = TRUE;
                START_PROCESSING(SSobj, this);
            }
            if (energy <= 0) //battery is completely dry, recharge the pointer to full then let us use it again
            {
                to_chat(user, span_warning(I18n.T("[]'s battery is overused, it needs time to recharge!", this)));
                recharge_locked = TRUE;
            }
        }

        //flash a pointer blip at the target
        target.flick_overlay_view(laser, 1 * SECONDS);
        //reset pointer sprite
        icon_state = "pointer";
    }

    // tg: code/game/objects/items/devices/laserpointer.dm:326
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (diode == null)
        {
            recharging = FALSE;
            return PROCESS_KILL;
        }
        if (SPT_PROB(10 + diode.rating.GetValueOrDefault() * 10, seconds_per_tick)) //+10% chance per diode tier to recharge one use per process
        {
            energy += 1;
            if (energy >= max_energy)
            {
                energy = max_energy;
                recharging = FALSE;
                recharge_locked = FALSE;
                return base.process(seconds_per_tick, __extra);
            }
        }
        return null;
    }
}

// tg: code/game/objects/items/devices/laserpointer.dm:62
public partial class obj_item_laser_pointer_upgraded : obj_item_laser_pointer
{
    // tg: code/game/objects/items/devices/laserpointer.dm:62
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        diode = Dm.@new<obj_item_stock_parts_micro_laser_ultra>();
        return dot;
    }
}

// tg: code/game/objects/items/devices/laserpointer.dm:67
public partial class obj_item_laser_pointer_infinite_range : obj_item_laser_pointer
{
    // tg: code/game/objects/items/devices/laserpointer.dm:71
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        diode = Dm.@new<obj_item_stock_parts_micro_laser_quadultra>();
        return dot;
    }
}
