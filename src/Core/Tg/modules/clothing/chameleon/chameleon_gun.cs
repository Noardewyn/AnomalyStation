// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/chameleon/chameleon_gun.dm,
// code/modules/projectiles/projectile/energy/chameleon.dm, code/modules/projectiles/ammunition/energy/laser.dm (the chameleon casing)
// and code/modules/clothing/chameleon/chameleon_action_subtypes.dm (the gun's actions)
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign G1b (D316): the chameleon laser, a harmless gun that looks, sounds and fires like the gun it mimics, with
// its action, its casing and its projectile (all whole). The var blocks are generated (D111).

public partial class obj_item_gun_energy_laser_chameleon : obj_item_gun_energy_laser
{
    // tg: code/modules/clothing/chameleon/chameleon_gun.dm:17
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_empprotection), Dm.bitor(Dm.bitor(EMP_PROTECT_SELF, EMP_PROTECT_CONTENTS), EMP_NO_EXAMINE)));
        // Init order shenanigans dictate we have to do this last so we can't just use `active_type`
        var gun_action = Dm.locate<datum_action_item_action_chameleon_change_gun>(actions) as datum_action_item_action_chameleon_change_gun;
        if (gun_action != null)
            Waiting.NoSleep(gun_action.update_look(default_look));
        return dot;
    }

    /**
     * Description: Resets the currently loaded chameleon variables, essentially resetting it to brand new.
     * Arguments: []
     */
    // tg: code/modules/clothing/chameleon/chameleon_gun.dm:28
    public virtual void reset_chameleon_vars()
    {
        chameleon_projectile_vars = Dm.list();

        if (chambered != null)
            chambered.firing_effect_type = Dm.initial(chambered, (obj_item_ammo_casing p) => p.firing_effect_type);

        fire_sound = Dm.initial(this, (obj_item_gun p) => p.fire_sound);
        burst_size = Dm.initial(this, (obj_item_gun p) => p.burst_size);
        fire_delay = Dm.initial(this, (obj_item_gun p) => p.fire_delay);
        inhand_x_dimension = Dm.initial(this, (obj_item p) => p.inhand_x_dimension);
        inhand_y_dimension = Dm.initial(this, (obj_item p) => p.inhand_y_dimension);

        qdel(chambered.loaded_projectile); // QDEL_NULL
        chambered.loaded_projectile = null;
        chambered.newshot();
    }

    /**
     * Description: Sets what gun we should be mimicking.
     * Arguments: [obj/item/gun/gun_to_set (the gun we're trying to mimic)]
     */
    // tg: code/modules/clothing/chameleon/chameleon_gun.dm:47
    public virtual object set_chameleon_gun(obj_item_gun gun_to_set = null)
    {
        if (gun_to_set == null)
        {
            stack_trace(Dm.text("[] is not a valid gun.", gun_to_set), "code/modules/clothing/chameleon/chameleon_gun.dm", 49);
            return FALSE;
        }

        fire_sound = gun_to_set.fire_sound;
        burst_size = gun_to_set.burst_size;
        fire_delay = gun_to_set.fire_delay;
        inhand_x_dimension = gun_to_set.inhand_x_dimension;
        inhand_y_dimension = gun_to_set.inhand_y_dimension;

        // We dupe this casing and then delete it at the end, to grab the projectile.
        obj_item_ammo_casing casing_to_dupe = null;

        if (gun_to_set is obj_item_gun_ballistic ball_gun)
        {
            // We also need to copy the starting magazine for ballistics.
            Type casing_type = Dm.initial(ball_gun.spawn_magazine_type, (obj_item_ammo_box p) => p.ammo_type) as Type;
            casing_to_dupe = Dm.@new(casing_type, this) as obj_item_ammo_casing;
        }
        else if (gun_to_set is obj_item_gun_magic magic_gun)
            casing_to_dupe = Dm.@new(magic_gun.ammo_type as Type, this) as obj_item_ammo_casing;
        else if (gun_to_set is obj_item_gun_energy energy_gun)
        {
            // Even if the energy gun has multiple ammo types, we copy the first. Energy guns always (should) have a list in ammo_type.
            if (energy_gun.ammo_type is DmList ammo_list && ammo_list.len > 0)
            {
                // a live gun's Initialize turned its types into casings; an inert gun still holds the types
                object first_casing = ammo_list[1];
                casing_to_dupe = Dm.@new(first_casing is datum d ? d.type : first_casing as Type, this) as obj_item_ammo_casing;
            }
        }
        else if (gun_to_set is obj_item_gun_syringe)
            casing_to_dupe = Dm.@new<obj_item_ammo_casing_syringegun>(this);
        else
            casing_to_dupe = Dm.@new<obj_item_ammo_casing>(this);

        set_chameleon_ammo(casing_to_dupe);
        qdel(casing_to_dupe);
        return null;
    }

    /**
     * Description: Sets the ammo type our gun should have.
     * Arguments: [obj/item/ammo_casing/cartridge (the ammo_casing we're trying to copy)]
     */
    // tg: code/modules/clothing/chameleon/chameleon_gun.dm:91
    public virtual object set_chameleon_ammo(obj_item_ammo_casing cartridge = null)
    {
        if (cartridge == null)
        {
            stack_trace(Dm.text("[] is not a valid ammo casing.", cartridge), "code/modules/clothing/chameleon/chameleon_gun.dm", 92);
            return FALSE;
        }

        obj_projectile projectile = cartridge.loaded_projectile;
        set_chameleon_projectile(projectile);
        return null;
    }

    /**
     * Description: Sets the current projectile variables for our chameleon gun.
     * Arguments: [obj/projectile/template_projectile (the projectile we're trying to copy)]
     */
    // tg: code/modules/clothing/chameleon/chameleon_gun.dm:103
    public virtual object set_chameleon_projectile(obj_projectile template_projectile = null)
    {
        if (template_projectile == null)
        {
            stack_trace(Dm.text("[] is not a valid projectile.", template_projectile), "code/modules/clothing/chameleon/chameleon_gun.dm", 104);
            return FALSE;
        }

        chameleon_projectile_vars = Dm.list(Dm.kv("name", "practice laser"), Dm.kv("icon", Dm.resource("icons/obj/weapons/guns/projectiles.dmi")), Dm.kv("icon_state", "laser"));

        object default_state = template_projectile.icon_state == null ? "laser" : template_projectile.icon_state;

        chameleon_projectile_vars["name"] = template_projectile.name;
        chameleon_projectile_vars["icon"] = template_projectile.icon;
        chameleon_projectile_vars["icon_state"] = default_state;
        chameleon_projectile_vars["speed"] = template_projectile.speed;
        chameleon_projectile_vars["color"] = template_projectile.color;
        chameleon_projectile_vars["hitsound"] = template_projectile.hitsound;
        chameleon_projectile_vars["impact_effect_type"] = template_projectile.impact_effect_type;
        chameleon_projectile_vars["range"] = template_projectile.range;
        chameleon_projectile_vars["suppressed"] = template_projectile.suppressed;
        chameleon_projectile_vars["hitsound_wall"] = template_projectile.hitsound_wall;
        chameleon_projectile_vars["pass_flags"] = template_projectile.pass_flags;

        if (chambered is obj_item_ammo_casing_energy_chameleon cartridge)
        {
            cartridge.loaded_projectile.name = template_projectile.name;
            cartridge.loaded_projectile.icon = template_projectile.icon;
            cartridge.loaded_projectile.icon_state = Dm.str(default_state);
            cartridge.loaded_projectile.speed = template_projectile.speed;
            cartridge.loaded_projectile.color = template_projectile.color;
            cartridge.loaded_projectile.hitsound = template_projectile.hitsound;
            cartridge.loaded_projectile.impact_effect_type = template_projectile.impact_effect_type;
            cartridge.loaded_projectile.range = template_projectile.range;
            cartridge.loaded_projectile.suppressed = template_projectile.suppressed;
            cartridge.loaded_projectile.hitsound_wall = template_projectile.hitsound_wall;
            cartridge.loaded_projectile.pass_flags = template_projectile.pass_flags;

            cartridge.projectile_vars = chameleon_projectile_vars.Copy();
        }

        if (Dm.truthy(real_hits))
        {
            qdel(chambered.loaded_projectile);
            chambered.projectile_type = template_projectile.type;
        }

        qdel(template_projectile);
        return null;
    }

    /**
     * Description: Resets our chameleon variables, then resets the entire gun to mimic the given guntype.
     * Arguments: [guntype (the gun we're copying, pathtyped to obj/item/gun)]
     */
    // tg: code/modules/clothing/chameleon/chameleon_gun.dm:152
    public virtual void set_chameleon_disguise(Type guntype = null)
    {
        reset_chameleon_vars();
        obj_item_gun new_gun = Dm.@new(guntype, this) as obj_item_gun;
        set_chameleon_gun(new_gun);
        qdel(new_gun);
    }
}

public partial class obj_item_gun_energy_laser_chameleon_ballistic_only : obj_item_gun_energy_laser_chameleon
{
    // tg: code/modules/clothing/chameleon/chameleon_gun.dm:162
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_chameleon_disguise(default_look);
        return dot;
    }
}

public partial class obj_item_ammo_casing_energy_chameleon : obj_item_ammo_casing_energy
{
    // tg: code/modules/projectiles/ammunition/energy/laser.dm:86
    public override async DmTask ready_proj(atom target = null, mob_living user = null, object quiet = null, string zone_override = null, atom fired_from = null)
    {
        await base.ready_proj(target, user, quiet, zone_override, fired_from);

        loaded_projectile.name = Dm.str(projectile_vars["name"]);
        loaded_projectile.icon = projectile_vars["icon"];
        loaded_projectile.icon_state = Dm.str(projectile_vars["icon_state"]);
        loaded_projectile.speed = Dm.num(projectile_vars["speed"]);
        loaded_projectile.color = projectile_vars["color"];
        loaded_projectile.hitsound = projectile_vars["hitsound"];
        loaded_projectile.impact_effect_type = projectile_vars["impact_effect_type"] as Type;
        loaded_projectile.range = Dm.num(projectile_vars["range"]);
        loaded_projectile.suppressed = Dm.num(projectile_vars["suppressed"]);
        loaded_projectile.hitsound_wall = projectile_vars["hitsound_wall"];
        loaded_projectile.pass_flags = Dm.num(projectile_vars["pass_flags"]);
    }
}

public partial class obj_projectile_energy_chameleon : obj_projectile_energy
{
    // Doesn't deal any damage or has any side effects.
    // tg: code/modules/projectiles/projectile/energy/chameleon.dm:4
    public override object is_hostile_projectile()
    {
        return FALSE;
    }
}

public partial class datum_action_item_action_chameleon_change_gun : datum_action_item_action_chameleon_change
{
    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:301
    [DmParams("Target")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (target is not obj_item_gun_energy_laser_chameleon)
        {
            stack_trace(Dm.text("Adding chameleon gun action to non-chameleon gun ([])", target), "code/modules/clothing/chameleon/chameleon_action_subtypes.dm", 305);
            qdel(this);
        }
    }

    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:307
    public override async DmTask initialize_blacklist()
    {
        await base.initialize_blacklist();
        Dm.combine(ref chameleon_blacklist, typecacheof(typeof(obj_item_gun_energy_minigun)));
    }

    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:311
    public override async DmTask update_look(Type picked_item = null)
    {
        obj_item_gun_energy_laser_chameleon chameleon_gun = target as obj_item_gun_energy_laser_chameleon;
        chameleon_gun.set_chameleon_disguise(picked_item);
        await base.update_look(picked_item);
    }
}
