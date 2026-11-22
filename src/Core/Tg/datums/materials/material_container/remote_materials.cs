// Translated from tgstation (snapshot 1d99bd7f): code/datums/materials/material_container/remote_materials.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Lavaland L2a (D304): the whole file. The var block is generated (D111).

/*
This component allows machines to connect remotely to a material container
(namely an /obj/machinery/ore_silo) elsewhere. It offers optional graceful
fallback to a local material storage in case remote storage is unavailable, and
handles linking back and forth.
*/

// tg: code/datums/materials/material_container/remote_materials.dm:8
public partial class datum_remote_materials : datum
{
    // tg: code/datums/materials/material_container/remote_materials.dm:31
    [DmParams("parent", "mapload", "allow_standalone", "force_connect", "mat_container_flags", "mat_container_signals")]
    public override async DmTask New(params object[] __extra)
    {
        object parent = Dm.arg(__extra, 0);
        object mapload = Dm.arg(__extra, 1);
        object allow_standalone = Dm.arg(__extra, 2) ?? TRUE;
        object force_connect = Dm.arg(__extra, 3) ?? FALSE;
        object mat_container_flags = Dm.arg(__extra, 4) ?? NONE;
        DmList mat_container_signals = Dm.arg(__extra, 5) as DmList;

        this.parent = parent as atom_movable;

        this.allow_standalone = allow_standalone;
        this.mat_container_flags = Dm.num(mat_container_flags);
        this.mat_container_signals = mat_container_signals;

        RegisterSignal(this.parent, COMSIG_ATOM_TOOL_ACT(TOOL_MULTITOOL), nameof(OnMultitool));

        turf T = get_turf(this.parent);
        bool connect_to_silo = false;
        if (Dm.truthy(force_connect) || (Dm.truthy(mapload) && Dm.truthy(is_station_level(T.z))))
            connect_to_silo = true;

        if (Dm.truthy(mapload)) // wait for silo to initialize during mapload
        {
            connection = Dm.@new<datum_callback>(this, nameof(_PrepareStorage), connect_to_silo ? TRUE : FALSE);
            SSticker.OnRoundstart(connection);
        }
        else //directly register in round
            await _PrepareStorage(connect_to_silo ? TRUE : FALSE);
    }

    /**
     * Internal proc. prepares local storage if onnect_to_silo = FALSE
     *
     * Arguments
     * connect_to_silo- if true connect to global silo. If not successfull then go to local storage
     * only if allow_standalone = TRUE, else you a null mat_container
     */
    // tg: code/datums/materials/material_container/remote_materials.dm:65
    public virtual async DmTask _PrepareStorage(float connect_to_silo = 0)
    {
        connection = null;
        if (Dm.truthy(connect_to_silo))
        {
            silo = GLOB.ore_silo_default;
            if (silo != null)
            {
                silo.connect_receptacle(this, parent);
                if (!Dm.truthy(Dm.bitand(mat_container_flags, MATCONTAINER_NO_INSERT)))
                {
                    RegisterSignal(parent, COMSIG_ATOM_ITEM_INTERACTION, nameof(on_item_insert));
                    RegisterSignal(parent, COMSIG_ATOM_ITEM_INTERACTION_SECONDARY, nameof(on_secondary_insert));
                }
            }
        }

        if (mat_container == null && Dm.truthy(allow_standalone))
            await _MakeLocal();
    }

    // tg: code/datums/materials/material_container/remote_materials.dm:80
    public override object Destroy(float? force = null)
    {
        if (connection != null)
        {
            LAZYREMOVE(ref SSticker.round_start_events, connection);
            connection = null;
        }
        if (silo != null)
        {
            allow_standalone = FALSE;
            Waiting.NoSleep(disconnect());
        }
        QDEL_NULL(ref mat_container);

        return base.Destroy(force);
    }

    // tg: code/datums/materials/material_container/remote_materials.dm:91
    public virtual async DmTask _MakeLocal()
    {
        silo = null;

        mat_container = Dm.@new<datum_material_container>(
            parent,
            SSmaterials.get_materials_by_flag(MATERIAL_SILO_STORED),
            local_size,
            mat_container_flags,
            Dm.named("container_signals", mat_container_signals),
            Dm.named("allowed_items", typeof(obj_item_stack)));
        await DmTask.CompletedTask;
    }

    /// Adds/Removes this connection from the silo
    // tg: code/datums/materials/material_container/remote_materials.dm:106
    public virtual void toggle_holding()
    {
        if ((silo == null))
            return;

        if (!Dm.truthy(silo.holds[this]))
            silo.holds[this] = TRUE;
        else
            silo.holds.Remove(this);
    }

    /**
     * Sets the storage size for local materials when not linked with silo
     * Arguments
     *
     * * size - the new size for local storage. measured in SHEET_MATERIAL_SIZE units
     */
    // tg: code/datums/materials/material_container/remote_materials.dm:121
    public virtual void set_local_size(float size = 0)
    {
        local_size = size;
        if (silo == null && mat_container != null)
            mat_container.max_amount = size;
    }

    ///Disconnects this component from the silo
    // tg: code/datums/materials/material_container/remote_materials.dm:127
    public virtual async DmTask disconnect()
    {
        if ((silo == null))
            return;

        UnregisterSignal(parent, Dm.list(COMSIG_ATOM_ITEM_INTERACTION, COMSIG_ATOM_ITEM_INTERACTION_SECONDARY));
        silo.disconnect_receptacle(this, parent);

        if (Dm.truthy(allow_standalone))
            await _MakeLocal();
    }

    // tg: code/datums/materials/material_container/remote_materials.dm:137
    public virtual object OnMultitool(datum source = null, mob user = null, obj_item_multitool M = null)
    {
        // SIGNAL_HANDLER
        object dot = NONE;
        if (!QDELETED(M.buffer) && istype(M.buffer, typeof(obj_machinery_ore_silo)))
        {
            if (silo == M.buffer)
            {
                to_chat(user, span_warning(I18n.T("[] is already connected to []!", parent, silo)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!check_z_level(M.buffer as obj))
            {
                to_chat(user, span_warning(I18n.T("[] is too far away to get a connection signal!", parent)));
                return ITEM_INTERACT_BLOCKING;
            }

            obj_machinery_ore_silo new_silo = (obj_machinery_ore_silo)M.buffer;
            datum_material_container new_container = new_silo.materials;
            if (silo != null)
                silo.disconnect_receptacle(this, parent);
            else if (mat_container != null)
            {
                //transfer all mats to silo. whatever cannot be transfered is dumped out as sheets
                if (Dm.truthy(Waiting.NoSleep(mat_container.total_amount())))
                {
                    foreach (datum_material mat in Dm.each<datum_material>(mat_container.materials))
                    {
                        float mat_amount = Dm.num(mat_container.materials[mat]);
                        if (!Dm.truthy(mat_amount) || !Waiting.NoSleep(new_container.has_space(mat_amount)) || !new_container.can_hold_material(mat))
                            continue;
                        new_container.materials[mat] = Dm.num(new_container.materials[mat]) + mat_amount;
                        mat_container.materials[mat] = 0;
                    }
                }
                qdel(mat_container);
            }
            new_silo.connect_receptacle(this, parent);
            if (!Dm.truthy(Dm.bitand(mat_container_flags, MATCONTAINER_NO_INSERT)))
            {
                RegisterSignal(parent, COMSIG_ATOM_ITEM_INTERACTION, nameof(on_item_insert));
                RegisterSignal(parent, COMSIG_ATOM_ITEM_INTERACTION_SECONDARY, nameof(on_secondary_insert));
            }
            to_chat(user, span_notice(I18n.T("You connect [] to [] from the multitool's buffer.", parent, silo)));
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    // tg: code/datums/materials/material_container/remote_materials.dm:170
    public virtual object on_item_insert(datum source = null, mob_living user = null, obj_item target = null)
    {
        // SIGNAL_HANDLER
        // Only insert stacks with left click
        if (isstack(target))
            return attempt_insert(user, target);
        return null;
    }

    // tg: code/datums/materials/material_container/remote_materials.dm:176
    public virtual object on_secondary_insert(datum source = null, mob_living user = null, obj_item target = null)
    {
        // SIGNAL_HANDLER
        return attempt_insert(user, target);
    }

    /// Insert mats into silo
    // tg: code/datums/materials/material_container/remote_materials.dm:181
    public virtual object attempt_insert(mob_living user = null, obj_item target = null)
    {
        if (!Dm.truthy(Dm.bitand(mat_container_flags, MATCONTAINER_ANY_INTENT)) && Dm.truthy(user.combat_mode))
            return null;

        if (silo != null)
        {
            mat_container.user_insert(target, user, parent);
            return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }

    /**
     * Checks if the param silo is in the same level as this components parent i.e. connected machine, rcd, etc
     *
     * Arguments
     * silo_to_check- Is this components parent in the same Z level as this param silo. If null
     * then check this components connected silo
     *
     * Returns true if both are on the station or same z level
     */
    // tg: code/datums/materials/material_container/remote_materials.dm:198
    public virtual bool check_z_level(obj silo_to_check = null)
    {
        silo_to_check ??= silo;
        if ((silo_to_check == null))
            return false;

        return Dm.truthy(is_valid_z_level(get_turf(silo_to_check), get_turf(parent)));
    }

    /// returns TRUE if this connection put on hold by the silo
    // tg: code/datums/materials/material_container/remote_materials.dm:205
    public virtual bool on_hold() => check_z_level() && Dm.truthy(silo.holds[this]);

    /**
     * Check if this connection can use any materials from the silo
     * Returns true only if
     * - The parent is of type movable atom
     * - A mat container is actually present
     * - The silo in not on hold
     * Arguments
     * * check_hold - should we check if the silo is on hold
     * * user_data - in the form rendered by ID_DATA(user); used as a reference for silo bans and access checks
     */
    // tg: code/datums/materials/material_container/remote_materials.dm:218
    public virtual bool can_use_resource(float check_hold = TRUE, DmList user_data = null)
    {
        atom_movable movable_parent = parent;
        if (movable_parent == null)
            return false;
        if (mat_container == null) //no silolink & local storage not supported
        {
            Waiting.NoSleep(movable_parent.say("No access to material storage, please contact the quartermaster."));
            return false;
        }
        if (Dm.truthy(check_hold) && on_hold()) //silo on hold
        {
            Waiting.NoSleep(movable_parent.say("Mineral access is on hold, please contact the quartermaster."));
            return false;
        }
        if (Dm.truthy(movable_parent._listen_lookup?[COMSIG_ORE_SILO_PERMISSION_CHECKED])
            && Dm.truthy(Dm.bitand(movable_parent._SendSignal(COMSIG_ORE_SILO_PERMISSION_CHECKED, Dm.list(movable_parent, user_data, movable_parent)), COMPONENT_ORE_SILO_DENY)))
            return false;
        return true;
    }

    /**
     * Use materials from either the silo(if connected) or from the local storage. If silo then this action
     * is logged else not e.g. action="build" & name="matter bin" means you are trying to build a matter bin
     *
     * Arguments
     * [mats][list]- list of materials to use
     * coefficient- each mat unit is scaled by this value then rounded. This value if usually your machine efficiency e.g. upgraded protolathe has reduced costs
     * multiplier- each mat unit is scaled by this value then rounded after it is scaled by coefficient. This value is your print quatity e.g. printing multiple items
     * action- For logging only. e.g. build, create, i.e. the action you are trying to perform
     * name- For logging only. the design you are trying to build e.g. matter bin, etc.
     * user_data - in the form rendered by ID_DATA(user), for material logging and (if this component is connected to a silo), permission checking
     */
    // tg: code/datums/materials/material_container/remote_materials.dm:244
    public virtual async DmTask<object> use_materials(DmList mats = null, float coefficient = 1, float multiplier = 1, string action = "processed", string name = "design", DmList user_data = null)
    {
        if (!can_use_resource(user_data: user_data))
            return 0;

        object amount_consumed = await mat_container.use_materials(mats, coefficient, multiplier);

        if (silo != null)//log only if silo is linked
        {
            DmList scaled_mats = Dm.list();
            foreach (object i in Dm.each<object>(mats))
                scaled_mats[i] = OPTIMAL_COST(OPTIMAL_COST(Dm.num(mats[i]) * coefficient) * multiplier);
            silo.silo_log(parent as obj_machinery, action, -multiplier, name, scaled_mats, user_data);
        }

        return amount_consumed;
    }

    /**
     * Ejects the given material ref and logs it
     *
     * Arguments
     * [material_ref][datum/material]- The material type you are trying to eject
     * eject_amount- how many sheets to eject
     * [drop_target][atom]- optional where to drop the sheets. null means it is dropped at this components parent location
     * user_data - in the form rendered by ID_DATA(user), for material logging and (if this component is connected to a silo), permission checking
     */
    // tg: code/datums/materials/material_container/remote_materials.dm:267
    public virtual async DmTask<float> eject_sheets(datum_material material_ref = null, object eject_amount = null, atom drop_target = null, DmList user_data = null)
    {
        if (!can_use_resource(user_data: user_data))
            return 0;

        atom_movable movable_parent = parent;
        if ((drop_target == null))
            drop_target = movable_parent.drop_location() as atom;

        return await mat_container.retrieve_stack(eject_amount, material_ref, target: drop_target, context: parent, user_data: user_data);
    }

    /**
     * Insert an item into the mat container, helper proc to insert items with the correct context
     *
     * Arguments
     * * obj/item/weapon - the item you are trying to insert
     * * multiplier - the multiplier applied on the materials consumed
     * * user_data - an alist in the form rendered by ID_DATA(user), for logging who/where/when the item was inserted
     */
    // tg: code/datums/materials/material_container/remote_materials.dm:285
    public virtual async DmTask<object> insert_item(obj_item weapon = null, float multiplier = 1, DmList user_data = null)
    {
        // Inserting materials automatically shouldn't be permission-restricted
        if (!islist(user_data))
            user_data = ID_DATA(null);
        user_data[SILICON_OVERRIDE] = SILICON_OVERRIDE;
        if (!can_use_resource(FALSE, user_data))
            return MATERIAL_INSERT_ITEM_FAILURE;

        return await mat_container.insert_item(weapon, multiplier, parent, user_data: user_data);
    }
}
