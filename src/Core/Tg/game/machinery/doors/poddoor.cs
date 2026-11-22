// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/doors/poddoor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the pod door's M3 subset (M3 P3; D117: pod doors and shutters keep their mapped state, a preopen one stands
// open): Initialize, Destroy, the appearance, the timings and the animation effects. M4a P4b: the tool acts and the item
// interaction (the controller calibration, the crowbar that opens an unpowered door; M5 slice 10: the crafted frame's
// owner, its cables and electronics, and taking it apart with the wirecutters and the welder); tg's buttons open and close them through the blast door controller (modules/assembly/doorcontrol.cs) with door.dm's
// open and close. The procs of later milestones are generated live-partial stubs (docs/ai/specs/live-partial.txt, ## P3, ## P4).
// The sounds of the translated procs play (M4b audio slice 1). The var block is generated (D111).

// tg: code/game/machinery/doors/poddoor.dm:1
public partial class obj_machinery_door_poddoor : obj_machinery_door
{
    //"BLAST" doors are obviously stronger than regular doors when it comes to BLASTS.
    // tg: code/game/machinery/doors/poddoor.dm:217  (M5x slice 1: explosions; set waitfor = FALSE, inherited: it sleeps only
    // through ..(), waiting-model.md 5.4)
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        if (Dm.num(severity) <= EXPLODE_LIGHT)
            return FALSE;
        return base.ex_act(severity, target, __extra);
    }

    // tg: code/game/machinery/doors/poddoor.dm:30
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(show_nav_computer_icon))
            Waiting.NoSleep(AddElement(typeof(datum_element_nav_computer_icon), Dm.resource("icons/effects/nav_computer_indicators.dmi"), "airlock", TRUE));
        return dot;
    }

    // tg: code/game/machinery/doors/poddoor.dm:35
    public override object Destroy(float? force = null)
    {
        owner = null;
        return base.Destroy(force);
    }

    // tg: code/game/machinery/doors/poddoor.dm:51
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(panel_open))
        {
            if (deconstruction == BLASTDOOR_FINISHED)
            {
                Dm.append(ref dot, span_notice(I18n.T("The maintenance panel is opened and the electronics could be <b>pried</b> out.")));
                Dm.append(ref dot, span_notice(I18n.T("\\The [] could be calibrated to a blast door controller ID with a <b>blast door controller</b>.", this)));
            }
            else if (deconstruction == BLASTDOOR_NEEDS_ELECTRONICS)
                Dm.append(ref dot, span_notice(I18n.T("The <i>electronics</i> are missing and there are some <b>wires</b> sticking out.")));
            else if (deconstruction == BLASTDOOR_NEEDS_WIRES)
                Dm.append(ref dot, span_notice(I18n.T("The <i>wires</i> have been removed and it's ready to be <b>sliced apart</b>.")));
        }
        return dot;
    }

    // tg: code/game/machinery/doors/poddoor.dm:91
    public override async DmTask on_craft_completion(DmList components = null, datum_crafting_recipe current_recipe = null, atom crafter = null)
    {
        await base.on_craft_completion(components, current_recipe, crafter);
        owner = WEAKREF(crafter) as datum_weakref;
    }

    // tg: code/game/machinery/doors/poddoor.dm:95
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.eq(deconstruction, BLASTDOOR_NEEDS_WIRES) && tool is obj_item_stack_cable_coil coil)
        {
            var recipe = (datum_crafting_recipe)Dm.locate_type(recipe_type, GLOB.crafting_recipes);
            object amount_needed = recipe.reqs[typeof(obj_item_stack_cable_coil)];
            if (Dm.num(coil.get_amount()) < Dm.num(amount_needed))
            {
                balloon_alert(user, I18n.T("not enough cable!"));
                return ITEM_INTERACT_SUCCESS;
            }
            balloon_alert(user, I18n.T("adding cables..."));
            if (!Dm.truthy(await do_after(user, 5 * SECONDS, this)))
                return ITEM_INTERACT_SUCCESS;
            await coil.use(amount_needed);
            deconstruction = BLASTDOOR_NEEDS_ELECTRONICS;
            balloon_alert(user, I18n.T("cables added"));
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.eq(deconstruction, BLASTDOOR_NEEDS_ELECTRONICS) && tool is obj_item_electronics_airlock)
        {
            balloon_alert(user, I18n.T("adding electronics..."));
            if (!Dm.truthy(await do_after(user, 10 * SECONDS, this)))
                return ITEM_INTERACT_SUCCESS;
            qdel(tool);
            balloon_alert(user, I18n.T("electronics added"));
            deconstruction = BLASTDOOR_FINISHED;
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.eq(deconstruction, BLASTDOOR_FINISHED) && tool is obj_item_assembly_control_blast_door controller_item)
        {
            if (Dm.truthy(density))
            {
                balloon_alert(user, I18n.T("open the door first!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(panel_open))
            {
                balloon_alert(user, I18n.T("open the panel first!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (Dm.eq(controller_item.id, -1))
            {
                //collect existing ids
                DmList door_ids = Dm.list();
                foreach (obj_machinery_door_poddoor M in Dm.each_cast<obj_machinery_door_poddoor>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_door_poddoor))))
                {
                    if (!Dm.truthy(M.id) || Dm.@in(M.id, door_ids))
                        continue;
                    Dm.append(ref door_ids, Dm.text("[]", M.id));
                }

                //create new id
                float new_id = 0;
                while (Dm.@in(Dm.text("[]", new_id), door_ids))
                    new_id += 1;
                controller_item.id = Dm.text("[]", new_id);
            }
            id = controller_item.id;
            owner = WEAKREF(user) as datum_weakref;
            balloon_alert(user, I18n.T("id changed to []", id));
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/machinery/doors/poddoor.dm:148
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(density))
        {
            balloon_alert(user, I18n.T("open the door first!"));
            return ITEM_INTERACT_SUCCESS;
        }

        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/doors/poddoor.dm:155
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            await open(TRUE, user);
            return ITEM_INTERACT_SUCCESS;
        }
        if (Dm.truthy(density))
        {
            balloon_alert(user, I18n.T("open the door first!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("open the panel first!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.eq(deconstruction, BLASTDOOR_FINISHED))
            return ITEM_INTERACT_BLOCKING;
        balloon_alert(user, I18n.T("removing airlock electronics..."));
        if (Dm.truthy(await tool.use_tool(this, user, 10 * SECONDS, volume: 50)))
        {
            Dm.@new<obj_item_electronics_airlock>(loc);
            id = null;
            owner = null;
            deconstruction = BLASTDOOR_NEEDS_ELECTRONICS;
            balloon_alert(user, I18n.T("removed airlock electronics"));
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/doors/poddoor.dm:176
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(density))
        {
            balloon_alert(user, I18n.T("open the door first!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("open the panel first!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.eq(deconstruction, BLASTDOOR_NEEDS_ELECTRONICS))
            return ITEM_INTERACT_BLOCKING;
        balloon_alert(user, I18n.T("removing internal cables..."));
        if (Dm.truthy(await tool.use_tool(this, user, 10 * SECONDS, volume: 50)))
        {
            var recipe = (datum_crafting_recipe)Dm.locate_type(recipe_type, GLOB.crafting_recipes);
            object amount = recipe.reqs[typeof(obj_item_stack_cable_coil)];
            Dm.@new<obj_item_stack_cable_coil>(loc, amount);
            deconstruction = BLASTDOOR_NEEDS_WIRES;
            balloon_alert(user, I18n.T("removed internal cables"));
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/doors/poddoor.dm:194
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.welder_act(user, tool);
        if (Dm.truthy(density))
        {
            balloon_alert(user, I18n.T("open the door first!"));
            return ITEM_INTERACT_SUCCESS;
        }
        if (!Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("open the panel first!"));
            return ITEM_INTERACT_SUCCESS;
        }
        if (!Dm.eq(deconstruction, BLASTDOOR_NEEDS_WIRES))
            return dot;
        balloon_alert(user, I18n.T("tearing apart...")); //You're tearing me apart, Lisa!
        if (Dm.truthy(await tool.use_tool(this, user, 15 * SECONDS, volume: 50)))
        {
            var recipe = (datum_crafting_recipe)Dm.locate_type(recipe_type, GLOB.crafting_recipes);
            object amount = recipe.reqs[typeof(obj_item_stack_sheet_plasteel)];
            Dm.@new<obj_item_stack_sheet_plasteel>(loc, amount);
            user.balloon_alert(user, I18n.T("torn apart"));
            qdel(this);
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/doors/poddoor.dm:213
    public override DmTask<object> connect_to_shuttle(object mapload = null, obj_docking_port_mobile port = null, obj_docking_port_stationary dock = null)
    {
        id = Dm.text("[]_[]", port.shuttle_id, id);
        return DmTask<object>.FromResult(default);
    }

    // tg: code/game/machinery/doors/poddoor.dm:222
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_icon_state(__extra);
        object sw__1 = animation;
        if (Dm.eq(sw__1, DOOR_OPENING_ANIMATION))
            icon_state = "opening";
        else if (Dm.eq(sw__1, DOOR_CLOSING_ANIMATION))
            icon_state = "closing";
        else if (Dm.eq(sw__1, DOOR_DENY_ANIMATION))
            icon_state = "deny";
        else
            icon_state = Dm.truthy(density) ? "closed" : "open";
        return dot;
    }

    // tg: code/game/machinery/doors/poddoor.dm:234
    public override object animation_length(object animation = null)
    {
        object sw__1 = animation;
        if (Dm.eq(sw__1, DOOR_OPENING_ANIMATION))
            return 1.1f * SECONDS;
        else if (Dm.eq(sw__1, DOOR_CLOSING_ANIMATION))
            return 1.1f * SECONDS;
        return null;
    }

    // tg: code/game/machinery/doors/poddoor.dm:241
    public override object animation_segment_delay(object animation = null)
    {
        object sw__1 = animation;
        if (Dm.eq(sw__1, DOOR_OPENING_PASSABLE))
            return 0.5f * SECONDS;
        else if (Dm.eq(sw__1, DOOR_OPENING_FINISHED))
            return 1.1f * SECONDS;
        else if (Dm.eq(sw__1, DOOR_CLOSING_UNPASSABLE))
            return 0.2f * SECONDS;
        else if (Dm.eq(sw__1, DOOR_CLOSING_FINISHED))
            return 1.1f * SECONDS;
        return null;
    }

    // tg: code/game/machinery/doors/poddoor.dm:252
    [DmParams("animation")]
    public override async DmTask animation_effects(object animation = null, float? force_type = null)
    {
        object sw__1 = animation;
        if (Dm.eq(sw__1, DOOR_OPENING_ANIMATION))
            playsound(this, animation_sound, 50, TRUE);
        else if (Dm.eq(sw__1, DOOR_CLOSING_ANIMATION))
            playsound(this, animation_sound, 50, TRUE);
    }
}

// tg: code/game/machinery/doors/poddoor.dm:289
public partial class obj_machinery_door_poddoor_shuttledock : obj_machinery_door_poddoor
{
    // tg: code/game/machinery/doors/poddoor.dm:293
    public virtual void check()
    {
        turf turf = get_step(this, checkdir);
        if (!istype(turf, turftype))
            Waiting.Detach(open());
        else
            Waiting.Detach(close());
    }
}

// tg: code/game/machinery/doors/poddoor.dm:259  (interleaved types, rules 1.9; gaps X5: an alien prying a blast door)
public partial class obj_machinery_door_poddoor : obj_machinery_door
{
    // tg: code/game/machinery/doors/poddoor.dm:259
    public override async DmTask<object> attack_alien(mob_living_carbon user = null, DmList modifiers = null)
    {
        if (Dm.truthy(Dm.bitand(density, Dm.num(!Dm.truthy(Dm.bitand(resistance_flags, INDESTRUCTIBLE))))))
        {
            add_fingerprint(user);
            user.visible_message(span_warning(I18n.T("[] begins prying open [].", user, this)),
                        span_noticealien(I18n.T("You begin digging your claws into [] with all your might!", this)),
                        span_warning(I18n.T("You hear groaning metal...")));
            playsound(this, Dm.resource("sound/machines/airlock/airlock_alien_prying.ogg"), 100, TRUE);

            float time_to_open = 5 * SECONDS;
            if (Dm.truthy(hasPower()))
                time_to_open = 15 * SECONDS;

            if (Dm.truthy(await do_after(user, time_to_open, this)))
            {
                if (Dm.truthy(density) && !Dm.truthy(await open(TRUE, user))) //The airlock is still closed, but something prevented it opening. (Another player noticed and bolted/welded the airlock in time!)
                    to_chat(user, span_warning(I18n.T("Despite your efforts, [] managed to resist your attempts to open it!", this)));
            }
        }

        else
            return await base.attack_alien(user, modifiers);
        return null;
    }
}
