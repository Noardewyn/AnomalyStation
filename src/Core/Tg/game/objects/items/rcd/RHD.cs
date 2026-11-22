// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/rcd/RHD.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// E13 (the RCD family): the whole file, the base of the rapid construction, lighting, tiling and decoration devices: their
// matter, loading it from compressed matter cartridges and from sheets, the upgrade disks, the build delay that a changed
// blueprint cancels, the ammo bar overlays and the shared window data. The ore silo link (/datum/remote_materials, the silo
// itself is mining's and cargo's, not ported) is a deferred leaf: no device gets a remote connection, so the silo branches are
// never reached (docs/ai/specs/not-translated.md section 91). E15 translated the examine (the owner's rule of 2026-10-29: examines
// are translated), for the plumbing constructor that reads it.

//RAPID HANDHELD DEVICE. the base for all rapid devices

// tg: code/game/objects/items/rcd/RHD.dm:5
public partial class obj_item_construction : obj_item
{
    // tg: code/game/objects/items/rcd/RHD.dm:3  #define SILO_USE_AMOUNT (SHEET_MATERIAL_AMOUNT / 4) (file-local, rules 11.4)
    private const float SILO_USE_AMOUNT = SHEET_MATERIAL_AMOUNT / 4f;

    // tg: code/game/objects/items/rcd/RHD.dm:50
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // tg passes (5, FALSE, src) to New(location, amount, cardinals_only): no turf and no sparks (amount 0), as written; the
        // port's cardinals_only is a number, so src is passed as its truth, TRUE
        spark_system = Dm.@new<datum_effect_system_basic_spark_spread>(5, FALSE, TRUE);
        spark_system.attach(this);
        if (Dm.truthy(Dm.bitand(construction_upgrades, RCD_UPGRADE_SILO_LINK)))
            Dm.Deferred("code/game/objects/items/rcd/RHD.dm:55", "Initialize: silo_mats = new /datum/remote_materials(src, mapload, FALSE), the ore silo link (the silo is not ported)");
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/rcd/RHD.dm:58
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref spark_system);
        QDEL_NULL(ref silo_mats);
        return base.Destroy(force);
    }

    ///An do_after() specially designed for rhd devices
    // tg: code/game/objects/items/rcd/RHD.dm:64
    public virtual async DmTask<object> build_delay(mob user = null, object delay = null, atom target = null)
    {
        if (Dm.num(delay) <= 0)
            return TRUE;

        blueprint_changed = FALSE;

        return await do_after(user, delay, target, extra_checks: Dm.@new<datum_callback>(this, nameof(blueprint_change)));
    }

    // tg: code/game/objects/items/rcd/RHD.dm:72
    public virtual object blueprint_change()
    {
        return Dm.not(blueprint_changed);
    }

    ///used for examining the RCD and for its UI
    // tg: code/game/objects/items/rcd/RHD.dm:78
    public virtual async DmTask<object> get_silo_iron()
    {
        if (Dm.truthy(silo_link) && Dm.truthy(silo_mats))
        {
            // silo_mats.mat_container && !silo_mats.on_hold(): get_material_amount(/datum/material/iron) / SILO_USE_AMOUNT
            Dm.NotTranslated("code/game/objects/items/rcd/RHD.dm:79", "get_silo_iron: the ore silo's iron (the silo is not ported)");
        }
        return 0;
    }

    ///returns local matter units available. overridden by rcd borg to return power units available
    // tg: code/game/objects/items/rcd/RHD.dm:84
    public virtual object get_matter(mob user = null)
    {
        return matter;
    }

    // tg: code/game/objects/items/rcd/RHD.dm:87  (E15)
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("It currently holds []/[] matter-units.", get_matter(user), max_matter));
        if (Dm.truthy(Dm.bitand(construction_upgrades, RCD_UPGRADE_SILO_LINK)))
        {
            // "[silo_mats.on_hold() ? "ON HOLD" : "ON"]": silo_mats is never made (the ore silo is not ported), so silo_link stays FALSE
            Dm.append(ref dot, I18n.T("Remote storage link state: [].", Dm.truthy(silo_link) ? "ON" : "OFF"));
            object iron = await get_silo_iron();
            if (Dm.truthy(iron))
                Dm.append(ref dot, I18n.T("Remote connection has iron in equivalent to [] RCD unit\\s.", iron)); //1 matter for 1 floor tile, as 4 tiles are produced from 1 iron
        }
        return dot;
    }

    // tg: code/game/objects/items/rcd/RHD.dm:96
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (istype(interacting_with, typeof(obj_item_rcd_upgrade)))
        {
            await install_upgrade(interacting_with as obj_item_rcd_upgrade, user);
            return ITEM_INTERACT_SUCCESS;
        }
        if (Dm.truthy(await insert_matter(interacting_with as obj, user)))
            return ITEM_INTERACT_SUCCESS;
        return await base.interact_with_atom(interacting_with, user, modifiers);
    }

    // tg: code/game/objects/items/rcd/RHD.dm:106
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_rcd_upgrade)))
        {
            await install_upgrade(tool as obj_item_rcd_upgrade, user);
            return ITEM_INTERACT_SUCCESS;
        }
        if (Dm.truthy(await insert_matter(tool, user)))
            return ITEM_INTERACT_SUCCESS;
        return await base.item_interaction(user, tool, modifiers, __extra);
    }

    /// Installs an upgrade into the RCD checking if it is already installed, or if it is a banned upgrade
    // tg: code/game/objects/items/rcd/RHD.dm:117
    public virtual async DmTask<object> install_upgrade(obj_item_rcd_upgrade design_disk = null, mob user = null)
    {
        if (Dm.truthy(Dm.bitand(Dm.num(design_disk.upgrade), construction_upgrades)))
        {
            balloon_alert(user, I18n.T("already installed!"));
            return FALSE;
        }
        if (Dm.truthy(Dm.bitand(Dm.num(design_disk.upgrade), banned_upgrades)))
        {
            balloon_alert(user, I18n.T("cannot install upgrade!"));
            return FALSE;
        }
        construction_upgrades = Dm.bitor(construction_upgrades, Dm.num(design_disk.upgrade));
        if (Dm.truthy(Dm.bitand(Dm.num(design_disk.upgrade), RCD_UPGRADE_SILO_LINK)) && !Dm.truthy(silo_mats))
            Dm.Deferred("code/game/objects/items/rcd/RHD.dm:126", "install_upgrade: silo_mats = new /datum/remote_materials(src, FALSE, FALSE), the ore silo link (the silo is not ported)");
        playsound(loc as atom, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        qdel(design_disk);
        await update_static_data_for_all_viewers();
        return TRUE;
    }

    /// Inserts matter into the RCD allowing it to build
    // tg: code/game/objects/items/rcd/RHD.dm:133
    public virtual async DmTask<object> insert_matter(obj item = null, mob user = null)
    {
        if (iscyborg(user))
            return FALSE;

        float loaded = FALSE;
        if (istype(item, typeof(obj_item_rcd_ammo)))
        {
            obj_item_rcd_ammo ammo = item as obj_item_rcd_ammo;
            float load = min(ammo.ammoamt, max_matter - matter);
            if (load <= 0)
            {
                balloon_alert(user, I18n.T("storage full!"));
                return FALSE;
            }
            ammo.ammoamt -= load;
            if (ammo.ammoamt <= 0)
                qdel(ammo);
            matter += load;
            playsound(loc as atom, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
            loaded = TRUE;
        }
        else if (isstack(item))
            loaded = Dm.num(await loadwithsheets(item as obj_item_stack, user));
        if (Dm.truthy(loaded))
            update_appearance(); //ensures that ammo counters (if present) get updated
        return loaded;
    }

    // tg: code/game/objects/items/rcd/RHD.dm:156
    public virtual async DmTask<object> loadwithsheets(obj_item_stack the_stack = null, mob user = null)
    {
        if (the_stack.matter_amount <= 0)
        {
            balloon_alert(user, I18n.T("invalid sheets!"));
            return FALSE;
        }
        float maxsheets = round((max_matter - matter) / the_stack.matter_amount); //calculate the max number of sheets that will fit in RCD
        if (maxsheets > 0)
        {
            float amount_to_use = min(Dm.num(the_stack.amount), maxsheets);
            await the_stack.use(amount_to_use);
            matter += the_stack.matter_amount * amount_to_use;
            playsound(loc as atom, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
            return TRUE;
        }
        balloon_alert(user, I18n.T("storage full!"));
        return FALSE;
    }

    // tg: code/game/objects/items/rcd/RHD.dm:170
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        playsound(loc as atom, Dm.resource("sound/effects/pop.ogg"), 50, FALSE);
        if (prob(20))
            await spark_system.start();
        return null;
    }

    // tg: code/game/objects/items/rcd/RHD.dm:175
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(has_ammobar))
        {
            float ratio = ceil((matter / max_matter) * ammo_sections);
            if (ratio > 0)
                Dm.append(ref dot, Dm.text("[]_charge[]", Dm.truthy(charge_icon_state) ? charge_icon_state : icon_state, ratio));
        }
        return dot;
    }

    /**
     * Uses resource to do some action. Returns amount of resource used or TRUE/FALSE if only an dry run is required
     *
     * Arguments
     * * amount - the amount of resource to use
     * * mob/user - the player using the resource
     * * dry_run - if TRUE will only check if the amount of resource is available but will not use any
    */
    // tg: code/game/objects/items/rcd/RHD.dm:190
    public virtual async DmTask<object> useResource(object amount = null, mob user = null, float? dry_run = null)
    {
        dry_run ??= FALSE;
        if (!Dm.truthy(silo_mats) || !Dm.truthy(silo_link))
        {
            if (matter < Dm.num(amount))
            {
                if (Dm.truthy(has_ammobar))
                    flick(Dm.text("[]_empty", Dm.truthy(charge_icon_state) ? charge_icon_state : icon_state), this);
                if (Dm.truthy(user))
                    balloon_alert(user, I18n.T("not enough matter!"));
                return FALSE;
            }
            if (!Dm.truthy(dry_run))
            {
                matter -= Dm.num(amount);
                update_appearance();
                playsound(loc as atom, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
            }
        }
        else
        {
            // silo_mats.can_use_resource(ID_DATA(user)), has_enough_of_material(iron, amount * SILO_USE_AMOUNT), use_materials(...)
            Dm.NotTranslated("code/game/objects/items/rcd/RHD.dm:203", "useResource: the ore silo's iron (the silo is not ported)");
        }
        return Dm.truthy(dry_run) ? TRUE : amount;
    }

    // tg: code/game/objects/items/rcd/RHD.dm:216
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList dot = Dm.list();

        dot["silo_upgraded"] = Dm.truthy(Dm.bitand(construction_upgrades, RCD_UPGRADE_SILO_LINK)) ? TRUE : FALSE;
        return dot;
    }

    ///shared data for rcd,rld & plumbing
    // tg: code/game/objects/items/rcd/RHD.dm:222
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        //matter in the rcd
        object total_matter = (Dm.truthy(Dm.bitand(construction_upgrades, RCD_UPGRADE_SILO_LINK)) && Dm.truthy(silo_link)) ? await get_silo_iron() : get_matter(user);
        if (!Dm.truthy(total_matter))
            total_matter = 0;
        data["matterLeft"] = total_matter;

        data["silo_enabled"] = silo_link;

        return data;
    }

    // tg: code/game/objects/items/rcd/RHD.dm:235
    public virtual object toggle_silo(mob user = null)
    {
        if (!Dm.truthy(silo_mats))
        {
            to_chat(user, span_warning(I18n.T("no remote storage connection.")));
            return FALSE;
        }

        // if(!silo_mats.mat_container && !silo_link): no silo detected; silo_link = !silo_link (silo_mats is never made)
        Dm.NotTranslated("code/game/objects/items/rcd/RHD.dm:240", "toggle_silo: the ore silo link (the silo is not ported)");
        return TRUE;
    }

    ///shared action for toggling silo link rcd,rld & plumbing
    // tg: code/game/objects/items/rcd/RHD.dm:249
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.eq(action, "toggle_silo") && Dm.truthy(Dm.bitand(construction_upgrades, RCD_UPGRADE_SILO_LINK)))
        {
            toggle_silo(ui.user);
            return TRUE;
        }

        object update = await handle_ui_act(action, @params, ui, state);
        if (update == null)
            update = FALSE;
        return update;
    }

    /// overwrite to insert custom ui handling for subtypes
    // tg: code/game/objects/items/rcd/RHD.dm:264
    public virtual async DmTask<object> handle_ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        return null;
    }

    // tg: code/game/objects/items/rcd/RHD.dm:267
    public virtual object range_check(atom target = null, mob user = null)
    {
        if (!Dm.eq(target.z, user.z))
            return null;
        if (!Dm.@in(target, dview(7, get_turf(user))))
        {
            balloon_alert(user, I18n.T("out of range!"));
            flick(Dm.text("[]_empty", icon_state), this);
            return FALSE;
        }
        else
            return TRUE;
    }

    /**
     * Checks if we are allowed to interact with a radial menu
     *
     * Arguments:
     * * user The living mob interacting with the menu
     * * remote_anchor The remote anchor for the menu
     */
    // tg: code/game/objects/items/rcd/RHD.dm:284
    public virtual object check_menu(mob_living user = null, object remote_anchor = null)
    {
        if (!istype(user, typeof(mob_living)))
            return FALSE;
        if (Dm.truthy(user.incapacitated))
            return FALSE;
        if (Dm.truthy(remote_anchor) && user.remote_control != remote_anchor)
            return FALSE;
        return TRUE;
    }
}
