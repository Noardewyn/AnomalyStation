// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/equipment/kinetic_crusher/trophies_misc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Alternate PKC skins
// tg: code/modules/mining/equipment/kinetic_crusher/trophies_misc.dm:2
public partial class datum_atom_skin_crusher_skin : datum_atom_skin
{
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_misc.dm:17
    public override void apply(atom apply_to = null, mob user = null)
    {
        base.apply(apply_to, user);
        obj_item_kinetic_crusher crusher = apply_to as obj_item_kinetic_crusher;
        bool reset = Dm.truthy(reset_missing);
        // APPLY_VAR_OR_RESET_INITIAL (__DEFINES/reskin.dm:10), inline
        if (Dm.truthy(new_projectile_icon)) crusher.projectile_icon = new_projectile_icon; else if (reset) crusher.projectile_icon = Dm.initial(crusher.type, (obj_item_kinetic_crusher c) => c.projectile_icon);
        if (Dm.truthy(new_projectile_icon_state)) crusher.projectile_icon_state = new_projectile_icon_state as string; else if (reset) crusher.projectile_icon_state = Dm.initial(crusher.type, (obj_item_kinetic_crusher c) => c.projectile_icon_state);
        if (Dm.truthy(new_inhandx)) crusher.inhand_x_dimension = Dm.num(new_inhandx); else if (reset) crusher.inhand_x_dimension = Dm.initial(crusher.type, (obj_item_kinetic_crusher c) => c.inhand_x_dimension);
        if (Dm.truthy(new_inhandy)) crusher.inhand_y_dimension = Dm.num(new_inhandy); else if (reset) crusher.inhand_y_dimension = Dm.initial(crusher.type, (obj_item_kinetic_crusher c) => c.inhand_y_dimension);
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_misc.dm:24
    public override void clear_skin(atom clear_from = null, mob user = null)
    {
        base.clear_skin(clear_from, user);
        obj_item_kinetic_crusher crusher = clear_from as obj_item_kinetic_crusher;
        // RESET_INITIAL_IF_SET (__DEFINES/reskin.dm:18), inline
        if (Dm.truthy(new_projectile_icon)) crusher.projectile_icon = Dm.initial(crusher.type, (obj_item_kinetic_crusher c) => c.projectile_icon);
        if (Dm.truthy(new_projectile_icon_state)) crusher.projectile_icon_state = Dm.initial(crusher.type, (obj_item_kinetic_crusher c) => c.projectile_icon_state);
        if (Dm.truthy(new_inhandx)) crusher.inhand_x_dimension = Dm.initial(crusher.type, (obj_item_kinetic_crusher c) => c.inhand_x_dimension);
        if (Dm.truthy(new_inhandy)) crusher.inhand_y_dimension = Dm.initial(crusher.type, (obj_item_kinetic_crusher c) => c.inhand_y_dimension);
    }
}

// tg: code/modules/mining/equipment/kinetic_crusher/trophies_misc.dm:37
public partial class datum_atom_skin_crusher_skin_harpoon : datum_atom_skin_crusher_skin
{
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_misc.dm:44
    public override void apply(atom apply_to = null, mob user = null)
    {
        base.apply(apply_to, user);
        RegisterSignal(apply_to, COMSIG_ITEM_ATTACK_ANIMATION, nameof(on_attack_animation));
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_misc.dm:48
    public override void clear_skin(atom clear_from = null, mob user = null)
    {
        base.clear_skin(clear_from, user);
        UnregisterSignal(clear_from, COMSIG_ITEM_ATTACK_ANIMATION);
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_misc.dm:52
    [SignalHandler]
    public virtual void on_attack_animation(obj_item source = null, atom_movable attacker = null, atom attacked_atom = null, object animation_type = null, DmList image_override = null, DmList animation_override = null)
    {
        // If nothing is forcing an animation type, attack with a piercing animation because we're a harpoon
        if (!Dm.truthy(animation_type))
            Dm.append(ref animation_override, ATTACK_ANIMATION_PIERCE);
    }
}

// tg: code/modules/mining/equipment/kinetic_crusher/trophies_misc.dm:59
public partial class datum_atom_skin_crusher_skin_dagger : datum_atom_skin_crusher_skin
{
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_misc.dm:65
    public override void apply(atom apply_to = null, mob user = null)
    {
        base.apply(apply_to, user);
        RegisterSignal(apply_to, COMSIG_ITEM_ATTACK_ANIMATION, nameof(on_attack_animation));
        RegisterSignal(apply_to, COMSIG_CRUSHER_FIRED_BLAST, nameof(on_fired_blast));
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_misc.dm:70
    public override void clear_skin(atom clear_from = null, mob user = null)
    {
        base.clear_skin(clear_from, user);
        UnregisterSignal(clear_from, COMSIG_ITEM_ATTACK_ANIMATION);
        UnregisterSignal(clear_from, COMSIG_CRUSHER_FIRED_BLAST);
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_misc.dm:75
    [SignalHandler]
    public virtual void on_attack_animation(obj_item_kinetic_crusher source = null, atom_movable attacker = null, atom attacked_atom = null, object animation_type = null, DmList image_override = null, DmList animation_override = null, DmList angle_override = null)
    {
        // If we've been forcefully assigned an animation type already, we shouldn't do the custom attack animation logic
        if (Dm.truthy(animation_type))
            return;

        if (isliving(attacked_atom))
        {
            mob_living target = attacked_atom as mob_living;
            if (Dm.truthy(source.last_projectile_pb))
            {
                Dm.append(ref image_override, image(icon: Dm.resource("icons/obj/mining.dmi"), icon_state: "crusher_dagger_blaster"));
                Dm.append(ref angle_override, 0);
                Dm.append(ref animation_override, ATTACK_ANIMATION_PIERCE);
                source.last_projectile_pb = FALSE;
                return;
            }

            if (Dm.truthy(target.has_status_effect(typeof(datum_status_effect_crusher_mark))))
                Dm.append(ref animation_override, ATTACK_ANIMATION_PIERCE);
        }

        Dm.append(ref image_override, image(icon: Dm.resource("icons/obj/mining.dmi"), icon_state: "crusher_dagger_melee"));
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_misc.dm:96
    [SignalHandler]
    public virtual void on_fired_blast(obj_item_kinetic_crusher source = null, atom target = null, mob_living user = null, obj_projectile_destabilizer destabilizer = null)
    {
        if (isliving(target) && Dm.num(get_dist(target, user)) <= 1)
            user.do_item_attack_animation(target, used_item: source);
    }
}

/// Unlockable (or forced) skins
// tg: code/modules/mining/equipment/kinetic_crusher/trophies_misc.dm:123
public partial class obj_item_crusher_trophy_retool_kit : obj_item_crusher_trophy
{
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_misc.dm:133
    public override string effect_desc()
    {
        return I18n.T("the crusher to have the appearance of \\a []", Dm.initial(forced_skin, (datum_atom_skin s) => s.preview_name));
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_misc.dm:136
    public override async DmTask<object> add_to(obj_item_kinetic_crusher pkc = null, mob_living user = null)
    {
        object dot = await base.add_to(pkc, user);
        if (!Dm.truthy(dot))
            return dot;

        pkc.update_reskin(forced_skin);
        return dot;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_misc.dm:143
    public override async DmTask<object> remove_from(obj_item_kinetic_crusher pkc = null, mob_living user = null)
    {
        pkc.update_reskin(null); // resets reskin component
        return await base.remove_from(pkc, user);
    }
}

// tg: code/modules/mining/equipment/kinetic_crusher/trophies_misc.dm:147
public partial class obj_item_crusher_trophy_retool_kit_ashenskull : obj_item_crusher_trophy_retool_kit
{
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_misc.dm:153
    public override string effect_desc()
    {
        return I18n.T("the crusher to appear corrupted by infernal powers");
    }
}
