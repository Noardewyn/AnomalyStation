// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/lightreplacer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 power helpers slice (D164): the whole file: the light replacer, its high capacity subtype and the bluespace subtype with its
// scan (dview, the firefly marks). The var block is generated (D111). The glass sheet and the shard hand themselves to it in
// glass.cs (glass.dm:53, :393). Left in live-partial.txt: vv_edit_var (admin tooling, M10); deferred (not-translated.md): the
// firefly's emissive glow (DV12; the bluespace beam is drawn since gaps OD3); not translated: the emagged replacer's plasma.
//
// Light replacer (LR)
//
// ABOUT THE DEVICE
//
// This is a device supposedly to be used by Janitors and Janitor Cyborgs which will
// allow them to easily replace lights. This was mostly designed for Janitor Cyborgs since
// they don't have hands or a way to replace lightbulbs.
//
// HOW IT WORKS
//
// You attack a light fixture with it, if the light fixture is broken it will replace the
// light fixture with a working light; the broken light is then placed on the floor for the
// user to then pickup with a trash bag. If it's empty then it will just place a light in the fixture.
//
// HOW TO REFILL THE DEVICE
//
// It will need to be manually refilled with lights.
// If it's part of a robot module, it will charge when the Robot is inside a Recharge Station.
//
// EMAGGED FEATURES
//
// When emagged it will rig every light it replaces with plasma, which will slowly heat up and ignite while the light is on.
// This is VERY noticable, even the device's name changes when you emag it so if anyone
// examines you when you're holding it in your hand, you will be discovered.

// tg: code/game/objects/items/devices/lightreplacer.dm:30  #define GLASS_SHEET_USES 5 (a file-local define, rules 11.4)
// tg: code/game/objects/items/devices/lightreplacer.dm:31  #define LIGHTBULB_COST 1
// tg: code/game/objects/items/devices/lightreplacer.dm:32  #define BULB_SHARDS_REQUIRED 4

// tg: code/game/objects/items/devices/lightreplacer.dm:34
public partial class obj_item_lightreplacer : obj_item
{
    private const float GLASS_SHEET_USES = 5;
    private const float LIGHTBULB_COST = 1;
    private const float BULB_SHARDS_REQUIRED = 4;

    // tg: code/game/objects/items/devices/lightreplacer.dm:62
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, status_string());
        return dot;
    }

    // tg: code/game/objects/items/devices/lightreplacer.dm:66
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        return Dm.truthy(await do_action(interacting_with, user)) ? ITEM_INTERACT_SUCCESS : NONE;
    }

    // tg: code/game/objects/items/devices/lightreplacer.dm:69
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        // has no bluespace capabilities
        if (!Dm.truthy(bluespace_toggle))
            return NONE;
        // target not in range
        if (!Dm.eq(interacting_with.z, user.z))
            return NONE;

        //replace lights & stuff
        return Dm.truthy(await do_action(interacting_with, user)) ? ITEM_INTERACT_SUCCESS : NONE;
    }

    // tg: code/game/objects/items/devices/lightreplacer.dm:80
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (uses >= max_uses)
        {
            user.balloon_alert(user, I18n.T("already full!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (istype(tool, typeof(obj_item_stack_sheet_glass)))
        {
            obj_item_stack_sheet_glass glass_to_insert = tool as obj_item_stack_sheet_glass;
            if (!Dm.truthy(await glass_to_insert.use(LIGHTBULB_COST)))
            {
                user.balloon_alert(user, I18n.T("need [] glass sheets!", LIGHTBULB_COST));
                return ITEM_INTERACT_BLOCKING;
            }
            add_uses(GLASS_SHEET_USES);
            user.balloon_alert(user, I18n.T("glass inserted"));
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_shard)))
            return await attempt_insert_shard(user, tool as obj_item_shard);

        if (istype(tool, typeof(obj_item_light)))
        {
            obj_item_light light_to_insert = tool as obj_item_light;
            //remove from player's hand
            if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(light_to_insert)))
            {
                user.balloon_alert(user, I18n.T("stuck in your hand!"));
                return ITEM_INTERACT_BLOCKING;
            }

            //insert light. display message only if adding a shard did not create a new bulb else the messages will conflict
            float display_msg = TRUE;
            if (Dm.eq(light_to_insert.status, LIGHT_OK))
                add_uses(1);
            else if (Dm.truthy(await add_shard(user)))
                display_msg = FALSE;
            if (Dm.truthy(display_msg))
                user.balloon_alert(user, I18n.T("light inserted"));
            qdel(light_to_insert);

            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_storage)))
        {
            float replaced_something = FALSE;
            float loaded = FALSE;

            foreach (obj_item item_to_check in Dm.each<obj_item>(tool.contents))
            {
                //reached max capacity during insertion
                if (uses >= max_uses)
                    break;

                //consume the item only if it's a light tube, bulb or shard
                loaded = FALSE;
                if (istype(item_to_check, typeof(obj_item_light)))
                {
                    obj_item_light found_light = item_to_check as obj_item_light;
                    if (Dm.eq(found_light.status, LIGHT_OK))
                        add_uses(1);
                    else
                        await add_shard(user);
                    loaded = TRUE;
                }
                else if (istype(item_to_check, typeof(obj_item_stack_sheet_glass)))
                {
                    obj_item_stack_sheet_glass glass_to_insert = item_to_check as obj_item_stack_sheet_glass;
                    if (Dm.truthy(await glass_to_insert.use(LIGHTBULB_COST)))
                    {
                        add_uses(GLASS_SHEET_USES);
                        loaded = TRUE;
                    }
                }
                else if (item_to_check.type == typeof(obj_item_shard))
                {
                    await add_shard(user);
                    loaded = TRUE;
                }

                //if item was loaded delete it
                if (Dm.truthy(loaded))
                {
                    qdel(item_to_check);
                    replaced_something = TRUE;
                }
            }

            if (!Dm.truthy(replaced_something))
            {
                user.balloon_alert(user, I18n.T("nothing usable in []!", tool));
                return ITEM_INTERACT_BLOCKING;
            }

            user.balloon_alert(user, I18n.T("lights inserted"));
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/objects/items/devices/lightreplacer.dm:157
    public virtual async DmTask<object> attempt_insert_shard(mob_living user = null, obj_item_shard tool = null)
    {
        if (tool.type != typeof(obj_item_shard)) //we don't want to insert plasma, titanium or other types of shards
        {
            user.balloon_alert(user, I18n.T("too impure!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
        {
            user.balloon_alert(user, I18n.T("stuck in your hand!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await add_shard(user))) //add_shard will display a message if it created a bulb from the shard so only display message when that does not happen
            user.balloon_alert(user, I18n.T("shard inserted"));
        qdel(tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/devices/lightreplacer.dm:171
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        Dm.combine(ref obj_flags, EMAGGED);
        playsound(loc, SFX_SPARKS, 100, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        update_appearance();
        to_chat(user, span_warning(I18n.T("[]'s lights are now filled with plasma! Be careful to only install them in disabled light fixtures, lest they explode!", this)));
        return FALSE;
    }

    // tg: code/game/objects/items/devices/lightreplacer.dm:180
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);
        name = Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) ? Dm.text("shortcircuited []", Dm.initial(this, (atom a) => a.name)) : (string)Dm.initial(this, (atom a) => a.name);
        return dot;
    }

    // tg: code/game/objects/items/devices/lightreplacer.dm:184
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", Dm.initial(this, (atom a) => a.icon_state), Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) ? "-emagged" : "");
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/devices/lightreplacer.dm:208
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        float on_a_light = FALSE; //For if we are on the same tile as a light when we do this
        foreach (obj_machinery_light target in Dm.each<obj_machinery_light>(user.loc))
        {
            await replace_light(target, user as mob_living);
            on_a_light = TRUE;
        }
        if (!Dm.truthy(on_a_light)) //So we don't give a balloon alert when we just used replace_light
            user.balloon_alert(user, I18n.T("[] lights, []/[] fragments", uses, bulb_shards, BULB_SHARDS_REQUIRED));
        return null;
    }

    /**
     * attempts to fix lights, flood lights & lights on a turf
     * Arguments
     * * target - the target we are trying to fix
     * * user - the mob performing this action
     * returns TRUE if the target was valid[light, floodlight or turf] regardless if any light's were fixed or not
     */
    // tg: code/game/objects/items/devices/lightreplacer.dm:223
    public virtual async DmTask<object> do_action(atom target = null, mob user = null)
    {
        // if we are attacking an light fixture then replace it directly
        if (istype(target, typeof(obj_machinery_light)))
        {
            if (Dm.truthy(await replace_light(target, user as mob_living)) && Dm.truthy(bluespace_toggle))
            {
                beam_to(user, target);
                playsound(this, Dm.resource("sound/items/pshoom/pshoom.ogg"), 40, 1);
            }
            return TRUE;
        }

        // if we are attacking a floodlight frame finish it
        if (istype(target, typeof(obj_structure_floodlight_frame)))
        {
            obj_structure_floodlight_frame frame = target as obj_structure_floodlight_frame;
            if (Dm.eq(frame.state, FLOODLIGHT_NEEDS_LIGHTS) && Dm.truthy(await Use(user)))
            {
                Dm.@new<obj_machinery_power_floodlight>(frame.loc);
                if (Dm.truthy(bluespace_toggle))
                {
                    beam_to(user, target);
                    playsound(this, Dm.resource("sound/items/pshoom/pshoom.ogg"), 40, 1);
                }
                to_chat(user, span_notice(I18n.T("You finish \\the [] with a light tube.", frame)));
                qdel(frame);
            }
            return TRUE;
        }

        //attempt to replace all light sources on the turf
        if (isturf(target))
        {
            float light_replaced = FALSE;
            foreach (atom target_atom in Dm.each<atom>(target))
            {
                if (Dm.truthy(await replace_light(target_atom, user as mob_living)))
                    light_replaced = TRUE;
            }
            if (Dm.truthy(light_replaced) && Dm.truthy(bluespace_toggle))
            {
                beam_to(user, target);
                playsound(this, Dm.resource("sound/items/pshoom/pshoom.ogg"), 40, 1);
            }
            return TRUE;
        }

        return FALSE;
    }

    // tg: code/game/objects/items/devices/lightreplacer.dm:227  user.Beam(target, icon_state = "rped_upgrade", time = 0.5 SECONDS)
    // (three sites: a fixture, a floodlight frame, a turf; one helper here; the beam is live since datums/beam.dm was translated,
    // swept with gaps OD3)
    private static void beam_to(mob user, atom target)
    {
        user.Beam(target, icon_state: "rped_upgrade", time: 0.5f * SECONDS);
    }

    // tg: code/game/objects/items/devices/lightreplacer.dm:256
    public virtual object status_string()
    {
        return I18n.T("It has [] light\\s remaining (plus []/[] fragment\\s).", uses, bulb_shards, BULB_SHARDS_REQUIRED);
    }

    // tg: code/game/objects/items/devices/lightreplacer.dm:259
    public virtual async DmTask<object> Use(mob user = null)
    {
        if (uses <= 0)
            return FALSE;

        playsound(loc, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        add_fingerprint(user);
        add_uses(-1);

        return TRUE;
    }

    // Negative numbers will subtract
    // tg: code/game/objects/items/devices/lightreplacer.dm:270
    public virtual void add_uses(float? amount = null)
    {
        amount ??= 1;
        uses = clamp(uses + amount.Value, 0, max_uses);
    }

    // tg: code/game/objects/items/devices/lightreplacer.dm:273
    public virtual async DmTask<object> add_shard(object user = null)
    {
        bulb_shards += 1;
        if (bulb_shards >= BULB_SHARDS_REQUIRED)
        {
            bulb_shards = 0;
            add_uses(1);
            to_chat(user, span_notice(I18n.T("\\The [] fabricates a new bulb from the broken glass it has stored. []", this, status_string())));
            playsound(loc, Dm.resource("sound/machines/ding.ogg"), 50, TRUE);
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/objects/items/devices/lightreplacer.dm:283  #define LIGHT_CHARGE_COEFF 30000 (a file-local define, rules 11.4)
    private const float LIGHT_CHARGE_COEFF = 30000;

    // tg: code/game/objects/items/devices/lightreplacer.dm:284
    public virtual void Charge(mob user = null, float? coeff = null)
    {
        charge += coeff ?? 0;
        if (charge > LIGHT_CHARGE_COEFF)
        {
            add_uses(floor(charge / LIGHT_CHARGE_COEFF));
            charge = Dm.mod(charge, LIGHT_CHARGE_COEFF);
        }
    }

    // tg: code/game/objects/items/devices/lightreplacer.dm:292
    public virtual async DmTask<object> replace_light(atom target = null, mob_living user = null)
    {
        //Confirm that it's a light we're testing, because afterattack runs this for everything on a given turf and will runtime
        if (!istype(target, typeof(obj_machinery_light)))
            return FALSE;
        obj_machinery_light light = target as obj_machinery_light;
        //If the light source is ok then what are we doing here
        if (Dm.eq(light.status, LIGHT_OK))
        {
            user.balloon_alert(user, I18n.T("light already installed!"));
            return FALSE;
        }
        //Were all out
        if (!Dm.truthy(await Use(user)))
        {
            //This balloon alert text is a little redundant, but I want to avoid a new player "yeah i know the light is empty" moment
            user.balloon_alert(user, I18n.T("light replacer empty!"));
            return FALSE;
        }

        //remove any broken light on the fixture & add it as a shard
        if (!Dm.eq(light.status, LIGHT_EMPTY))
        {
            await add_shard(user);
            light.status = LIGHT_EMPTY;
            await light.update();
        }
        //create a copy of the light type & copy it's params onto the target
        obj_item_light old_light = Dm.@new(light.light_type) as obj_item_light;
        light.status = old_light.status;
        light.switchcount = old_light.switchcount;
        light.brightness = old_light.brightness;
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            // target.create_reagents(LIGHT_REAGENT_CAPACITY, SEALED_CONTAINER | TRANSPARENT); target.reagents.add_reagent(/datum/reagent/toxin/plasma, 10):
            // the rigged bulb. The fixture's own handling of a bulb with reagents (heating them, spark_act) is chemistry (M8, the
            // NotTranslated sites of light.cs), and the plasma reagent is not translated either.
            Dm.NotTranslated("code/game/objects/items/devices/lightreplacer.dm:317", "replace_light: the emagged replacer's plasma in the fitted bulb (rigged bulbs: the reagent holder's plasma and its burning, chemistry, M8)");
        }
        light.on = Dm.num(light.has_power());
        await light.update();
        //clean up
        qdel(old_light);

        return TRUE;
    }
}

// tg: code/game/objects/items/devices/lightreplacer.dm:326  (/obj/item/lightreplacer/advanced: var-only, generated)

// tg: code/game/objects/items/devices/lightreplacer.dm:332  #define BLIGHTREPLACER_SPOT_COOLDOWN (BLIGHTREPLACER_SPOT_LIFE + 1 SECONDS) (a file-local define, rules 11.4)
// tg: code/game/objects/items/devices/lightreplacer.dm:333  #define BLIGHTREPLACER_SPOT_RANGE 7
// tg: code/game/objects/items/devices/lightreplacer.dm:334  #define BLIGHTREPLACER_SPOT_LIFE (5 SECONDS)

// tg: code/game/objects/items/devices/lightreplacer.dm:336
public partial class obj_item_lightreplacer_blue : obj_item_lightreplacer
{
    private const float BLIGHTREPLACER_SPOT_LIFE = 5 * SECONDS;
    private const float BLIGHTREPLACER_SPOT_COOLDOWN = BLIGHTREPLACER_SPOT_LIFE + 1 * SECONDS;
    private const float BLIGHTREPLACER_SPOT_RANGE = 7;

    // tg: code/game/objects/items/devices/lightreplacer.dm:346
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        return FALSE; // balancing against longrange explosions
    }

    // tg: code/game/objects/items/devices/lightreplacer.dm:349
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        if (!(lightreplacer_spot_cooldown <= world.time)) // !COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        {
            balloon_alert(user, I18n.T("on cooldown!"));
            return null;
        }
        lightreplacer_spot_cooldown = world.time + BLIGHTREPLACER_SPOT_COOLDOWN; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline
        lightreplacer_scan();
        return null;
    }

    /// Scans the area in search of fixtures with broken bulbs in the BLIGHTREPLACER_SPOT_RANGE range, and also marks them with the blue_firefly effect.
    // tg: code/game/objects/items/devices/lightreplacer.dm:357
    public virtual void lightreplacer_scan()
    {
        turf source_turf = get_turf(this);
        foreach (obj_machinery_light broken_light in Dm.each<obj_machinery_light>(dview(BLIGHTREPLACER_SPOT_RANGE, source_turf)))
        {
            if (Dm.eq(broken_light.status, LIGHT_OK))
                continue;
            obj_effect_temp_visual_blue_firefly firefly = Dm.@new<obj_effect_temp_visual_blue_firefly>(get_turf(broken_light));
            animate(firefly, Dm.named("alpha", 0), Dm.named("time", BLIGHTREPLACER_SPOT_LIFE), Dm.named("easing", Dm.bitor(CIRCULAR_EASING, EASE_IN)));
        }
    }
}

// tg: code/game/objects/items/devices/lightreplacer.dm:365  (/datum/action/item_action/lightreplacer_scan: var-only, generated)

// tg: code/game/objects/items/devices/lightreplacer.dm:369
public partial class obj_effect_temp_visual_blue_firefly : obj_effect_temp_visual
{
    // tg: code/game/objects/items/devices/lightreplacer.dm:378
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/game/objects/items/devices/lightreplacer.dm:382
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        // . += emissive_appearance(icon, icon_state, src, alpha = src.alpha): the glow on the emissive plane (DEVIATION DV12)
        Dm.Deferred("code/game/objects/items/devices/lightreplacer.dm:384", "update_overlays: emissive_appearance of the bluespace firefly (DEVIATION DV12)");
        return dot;
    }
}
