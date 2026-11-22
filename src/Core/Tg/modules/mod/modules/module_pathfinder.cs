// Translated from tgstation (snapshot 1d99bd7f): code/modules/mod/modules/module_pathfinder.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO2 (D333): the file whole. The pathfinder module: its implant goes into a human (by hand, or into the wearer from the suit),
// and the implant's "Recall MOD" button flies the suit to its owner: it shakes, breaks out of a closet or a suit storage unit, and
// rides a transport supply pod (with its jet overlay) to the implanted owner, then puts itself on them and deploys when they stand
// next to it. The var blocks are generated (D111).

///Pathfinder - Can fly the suit from a long distance to an implant installed in someone.
// tg: code/modules/mod/modules/module_pathfinder.dm:4
public partial class obj_item_mod_module_pathfinder : obj_item_mod_module
{
    // tg: code/modules/mod/modules/module_pathfinder.dm:1 (file-local, #undef at :251)
    private const float PATHFINDER_PRE_ANIMATE_TIME = 2 * SECONDS;

    // tg: code/modules/mod/modules/module_pathfinder.dm:34
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        implant = Dm.@new<obj_item_implant_mod>(this);
        jet_icon = image(icon: Dm.resource("icons/obj/clothing/modsuit/mod_modules.dmi"), icon_state: "mod_jet", layer: LOW_ITEM_LAYER) as image;
        return dot;
    }

    // tg: code/modules/mod/modules/module_pathfinder.dm:40
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref implant);
        return base.Destroy(force);
    }

    // tg: code/modules/mod/modules/module_pathfinder.dm:44
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (gone == implant)
        {
            implant_inside = FALSE;
            await update_icon_state();
        }
        return await base.Exited(gone, direction);
    }

    // tg: code/modules/mod/modules/module_pathfinder.dm:50
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(implant_inside) ? "pathfinder" : "pathfinder_empty";
        return dot;
    }

    // tg: code/modules/mod/modules/module_pathfinder.dm:54
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(implant_inside))
            Dm.append(ref dot, span_notice(I18n.T("Use it on a human to implant them.")));
        else
            Dm.append(ref dot, span_warning(I18n.T("The implant is missing.")));
        return dot;
    }

    // tg: code/modules/mod/modules/module_pathfinder.dm:61
    public override async DmTask<object> attack__proc(mob_living target = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (!ishuman(target) || !Dm.truthy(implant_inside)) // Not human, or no implant in module
            return null;
        if (!Dm.truthy(await do_after(user, 1.5f * SECONDS, target: target)))
        {
            balloon_alert(user, I18n.T("interrupted!"));
            return null;
        }
        if (!Dm.truthy(await implant.implant(target, user))) // If implant fails
        {
            balloon_alert(user, I18n.T("can't implant!"));
            return null;
        }
        if (target == user)
            to_chat(user, span_notice(I18n.T("You implant yourself with [].", implant)));
        else
            target.visible_message(span_notice(I18n.T("[] implants [].", user, target)), span_notice(I18n.T("[] implants you with [].", user, implant)));
        playsound(this, Dm.resource("sound/effects/spray.ogg"), 30, TRUE, -6);
        return null;
    }

    // tg: code/modules/mod/modules/module_pathfinder.dm:76
    public override async DmTask<object> on_use(mob activator = null)
    {
        object dot = await base.on_use(activator);
        if (mod.wearer != null && Dm.truthy(implant_inside)) // implant them
        {
            await try_implant(activator);
            return dot;
        }
        if (mod.wearer != null)
            balloon_alert(activator, I18n.T("suit already worn!"));
        else
            recall(activator);
        return dot;
    }

    /// Assuming we have a wearer, attempt to implant them.
    // tg: code/modules/mod/modules/module_pathfinder.dm:88
    public virtual async DmTask try_implant(mob activator = null)
    {
        if (!ishuman(mod.wearer)) // Wearer isn't human
            return;
        if (!Dm.truthy(await implant.implant(mod.wearer, mod.wearer)))
        {
            balloon_alert(activator, I18n.T("can't implant!"));
            return;
        }
        balloon_alert(activator, I18n.T("implanted"));
        if (!(activator == mod.wearer)) // someone else implanted you
            balloon_alert(mod.wearer, I18n.T("tracker implanted!"));
        playsound(this, Dm.resource("sound/effects/spray.ogg"), 30, TRUE, -6);
    }

    // tg: code/modules/mod/modules/module_pathfinder.dm:99
    public virtual async DmTask attach(mob_living user = null)
    {
        if (!ishuman(user))
            return;
        mob_living_carbon_human human_user = user as mob_living_carbon_human;
        if (Dm.truthy(human_user.get_item_by_slot(mod.slot_flags)) && !Dm.truthy(await human_user.dropItemToGround(human_user.get_item_by_slot(mod.slot_flags) as obj_item)))
            return;
        if (!Dm.truthy(await human_user.equip_to_slot_if_possible(mod, mod.slot_flags, qdel_on_fail: FALSE, disable_warning: TRUE)))
            return;
        await mod.quick_deploy(user);
        await human_user.update_action_buttons(TRUE);
        playsound(mod, Dm.resource("sound/machines/ping.ogg"), 50, TRUE);
        await drain_power(use_energy_cost);
    }

    // tg: code/modules/mod/modules/module_pathfinder.dm:112
    public virtual object recall(mob recaller = null)
    {
        if (implant == null)
        {
            balloon_alert(recaller, I18n.T("no implant!"));
            return FALSE;
        }
        if (recaller != implant.imp_in && !Dm.truthy(allow_suit_activation)) // No pAI recalling
        {
            balloon_alert(recaller, I18n.T("invalid user!"));
            return FALSE;
        }
        if (Dm.truthy(mod.open))
        {
            balloon_alert(recaller, I18n.T("cover open!"));
            return FALSE;
        }
        if (Dm.truthy(in_transit))
        {
            balloon_alert(recaller, I18n.T("suit in transit!"));
            return FALSE;
        }
        object atom_on_turf = get_atom_on_turf(mod);
        if (ismob(atom_on_turf))
        {
            if (atom_on_turf == recaller)
                balloon_alert(recaller, I18n.T("already worn!"));
            else
                recaller.balloon_alert(recaller, I18n.T("suit is worn by somebody else!"));
            return FALSE;
        }

        in_transit = TRUE;
        // tg quirk kept: the suit's pixel_x is put back to the module's base_pixel_y
        animate(mod, 0.5f * SECONDS, Dm.named("pixel_x", base_pixel_y), Dm.named("pixel_y", base_pixel_y));
        mod.Shake(pixelshiftx: 1, pixelshifty: 1, duration: PATHFINDER_PRE_ANIMATE_TIME);
        _addtimer(Dm.@new<datum_callback>(this, nameof(do_recall), recaller), PATHFINDER_PRE_ANIMATE_TIME, TIMER_DELETE_ME, file: "code/modules/mod/modules/module_pathfinder.dm", line: 136);

        balloon_alert(recaller, I18n.T("suit recalled"));
        if (!(recaller == mod.wearer))
            balloon_alert(mod.wearer, I18n.T("suit recalled"));
        return TRUE;
    }

    /// Pod-transport the suit to its owner
    // tg: code/modules/mod/modules/module_pathfinder.dm:144
    public virtual async DmTask do_recall(mob recaller = null)
    {
        object container = get_atom_on_turf(mod);
        if (ismob(container))
        {
            balloon_alert(recaller, I18n.T("launch interrupted!"));
            in_transit = FALSE;
            return;
        }

        if (iscloset(container))
        {
            obj_structure_closet closet = container as obj_structure_closet;
            if (!Dm.truthy(closet.opened))
            {
                if (!Dm.truthy(await closet.open()))
                {
                    playsound(closet, Dm.resource("sound/effects/bang.ogg"), vol: 50, vary: TRUE);
                    closet.bust_open();
                }
            }
        }

        mod.add_overlay(jet_icon);
        playsound(mod, Dm.resource("sound/vehicles/rocketlaunch.ogg"), vol: 80, vary: FALSE);
        turf land_target = get_turf(implant.imp_in);
        obj_structure_closet_supplypod pod = await podspawn(Dm.list(
            Dm.kv("target", get_turf(mod)),
            Dm.kv("path", typeof(obj_structure_closet_supplypod_transport_module_pathfinder)),
            Dm.kv("reverse_dropoff_coords", Dm.list(land_target.x, land_target.y, land_target.z)))) as obj_structure_closet_supplypod;

        await pod.insert(mod, pod);
        RegisterSignal(pod, COMSIG_SUPPLYPOD_RETURNING, nameof(pod_takeoff));

        if (istype(container, typeof(obj_machinery_suit_storage_unit)))
        {
            obj_machinery_suit_storage_unit storage = container as obj_machinery_suit_storage_unit;
            storage.locked = FALSE;
            await storage.open_machine();
        }
    }

    /// Track when pod has taken off so we don't falsely report the initial landing
    // tg: code/modules/mod/modules/module_pathfinder.dm:177
    [SignalHandler]
    public virtual void pod_takeoff(datum pod = null)
    {
        RegisterSignal(pod, COMSIG_SUPPLYPOD_LANDED, nameof(pod_landed));
    }

    /// When the pod landed, we can recall again
    // tg: code/modules/mod/modules/module_pathfinder.dm:182
    [SignalHandler]
    public virtual void pod_landed()
    {
        in_transit = FALSE;
        mod.cut_overlay(jet_icon);
        playsound(mod, Dm.resource("sound/items/handling/toolbox/toolbox_drop.ogg"), vol: 80, vary: FALSE);
        if (implant?.imp_in != null && Dm.truthy(Waiting.NoSleep(implant.imp_in.Adjacent(this))))
            Waiting.Detach(attach(implant.imp_in));
    }
}

// ###########
// THE INPLANT
// ###########

// tg: code/modules/mod/modules/module_pathfinder.dm:195
public partial class obj_item_implant_mod : obj_item_implant
{
    // tg: code/modules/mod/modules/module_pathfinder.dm:209
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!istype(loc, typeof(obj_item_mod_module_pathfinder)))
            return INITIALIZE_HINT_QDEL;
        module = loc as obj_item_mod_module_pathfinder;
        return dot;
    }

    // tg: code/modules/mod/modules/module_pathfinder.dm:215
    public override object Destroy(float? force = null)
    {
        module = null;
        return base.Destroy(force);
    }
}

// tg: code/modules/mod/modules/module_pathfinder.dm:219
public partial class datum_action_item_action_mod_recall : datum_action_item_action
{
    // tg: code/modules/mod/modules/module_pathfinder.dm:230
    [DmParams("Target")]
    public override async DmTask New(params object[] __extra)
    {
        object Target = Dm.arg(__extra, 0);
        await base.New(__extra);
        if (!istype(Target, typeof(obj_item_implant_mod)))
        {
            qdel(this);
            return;
        }
    }

    // tg: code/modules/mod/modules/module_pathfinder.dm:236
    public override async DmTask<object> do_effect(object trigger_flags = null)
    {
        obj_item_implant_mod implant = target as obj_item_implant_mod;
        if (!(recall_cooldown <= world.time))
        {
            implant.balloon_alert(owner, I18n.T("on cooldown!"));
            return null;
        }
        if (Dm.truthy(implant.module.recall(owner)))
        {
            implant.balloon_alert(owner, I18n.T("suit incoming..."));
            recall_cooldown = world.time + 5 * SECONDS;
        }
        return null;
    }
}

/// Special pod subtype we use just to make insertion check easy
// tg: code/modules/mod/modules/module_pathfinder.dm:246
public partial class obj_structure_closet_supplypod_transport_module_pathfinder : obj_structure_closet_supplypod_transport
{
    // tg: code/modules/mod/modules/module_pathfinder.dm:248
    public override async DmTask<object> insertion_allowed(atom AM = null)
    {
        atom to_insert = AM;
        return istype(to_insert, typeof(obj_item_mod_control)) ? TRUE : FALSE;
    }
}
