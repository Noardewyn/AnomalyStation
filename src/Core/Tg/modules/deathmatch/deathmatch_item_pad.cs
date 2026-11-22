// The port's own code (DV584; docs/ai/specs/deathmatch): the weapon pad of the deathmatch arena. Not a translation; it stands on
// tg's random spawner (code/game/objects/effects/spawners/random/_random.dm).
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

[assembly: DmPortType(typeof(Anomaly.Core.Tg.obj_effect_spawner_random_deathmatch_item_pad))]

namespace Anomaly.Core.Tg;

/// <summary>
/// DEVIATION(DV584): a weapon pad of the deathmatch arena. A random spawner that stays where the map put it: it spawns one item from its
/// weighted list, watches it, and once the item leaves the pad's tile (picked up, thrown, pushed) or is deleted it spawns the next one
/// <see cref="refill_delay"/> later, with a burst of sparks. tg's spawners spawn once and delete themselves; tg's deathmatch hands each
/// player a fixed loadout instead.
/// </summary>
[DmPath("/obj/effect/spawner/random/deathmatch_item_pad")]
public partial class obj_effect_spawner_random_deathmatch_item_pad : obj_effect_spawner_random
{
    /// <summary>How long after its item is taken the pad spawns the next (deciseconds); a map may set it per pad.</summary>
    public float refill_delay = 5 * SECONDS;

    /// <summary>The item lying on the pad, or null while the pad waits to refill.</summary>
    public atom_movable held_item;

    public obj_effect_spawner_random_deathmatch_item_pad()
    {
        name = "deathmatch item pad";
        icon_state = "laser_gun"; // tg's random weapon spawner's marker (spawners/random/weapon.dm)
        // The pick weights: guns 3 (the heavy ones 1-2), their ammunition 2, melee 2, armour 2, grenades 1. The guns are tg's of the
        // deathmatch loadouts (deathmatch_loadouts.dm) and of the armory spawners (spawners/random/armory.dm); a gun spawns loaded.
        loot = Dm.list(
            Dm.kv(typeof(obj_item_gun_ballistic_automatic_pistol), 3),
            Dm.kv(typeof(obj_item_gun_ballistic_automatic_pistol_m1911), 3),
            Dm.kv(typeof(obj_item_gun_ballistic_revolver), 3),
            Dm.kv(typeof(obj_item_gun_ballistic_shotgun_automatic_combat_compact), 3),
            Dm.kv(typeof(obj_item_gun_ballistic_shotgun_riot), 3),
            Dm.kv(typeof(obj_item_gun_ballistic_shotgun_bulldog_unrestricted), 2),
            Dm.kv(typeof(obj_item_gun_ballistic_rifle_boltaction), 3),
            Dm.kv(typeof(obj_item_gun_ballistic_rifle_sniper_rifle), 1),
            Dm.kv(typeof(obj_item_gun_ballistic_automatic_c20r_unrestricted), 2),
            Dm.kv(typeof(obj_item_gun_ballistic_automatic_wt550), 3),
            Dm.kv(typeof(obj_item_gun_ballistic_automatic_l6_saw_unrestricted), 1),
            Dm.kv(typeof(obj_item_gun_ballistic_automatic_smartgun), 1),
            Dm.kv(typeof(obj_item_gun_ballistic_rocketlauncher_unrestricted), 1),
            Dm.kv(typeof(obj_item_gun_ballistic_shotgun_china_lake), 1),
            Dm.kv(typeof(obj_item_gun_energy_laser), 3),
            Dm.kv(typeof(obj_item_gun_energy_laser_retro), 2),
            Dm.kv(typeof(obj_item_gun_energy_e_gun), 3),
            Dm.kv(typeof(obj_item_gun_energy_e_gun_hos), 1),
            Dm.kv(typeof(obj_item_gun_energy_disabler), 2),
            Dm.kv(typeof(obj_item_gun_energy_ionrifle), 1),
            Dm.kv(typeof(obj_item_gun_energy_recharge_ebow), 1),
            Dm.kv(typeof(obj_item_gun_energy_laser_captain), 1),

            Dm.kv(typeof(obj_item_ammo_box_magazine_m9mm), 2),
            Dm.kv(typeof(obj_item_ammo_box_magazine_m45), 2),
            Dm.kv(typeof(obj_item_ammo_box_speedloader_c357), 2),
            Dm.kv(typeof(obj_item_storage_box_lethalshot), 2),
            Dm.kv(typeof(obj_item_ammo_box_magazine_m12g), 2),
            Dm.kv(typeof(obj_item_ammo_box_speedloader_strilka310), 2),
            Dm.kv(typeof(obj_item_ammo_box_magazine_sniper_rounds), 1),
            Dm.kv(typeof(obj_item_ammo_box_magazine_smgm45), 2),
            Dm.kv(typeof(obj_item_ammo_box_magazine_wt550m9), 2),
            Dm.kv(typeof(obj_item_ammo_box_magazine_m7mm), 1),
            Dm.kv(typeof(obj_item_ammo_box_magazine_smartgun), 1),
            Dm.kv(typeof(obj_item_ammo_casing_rocket), 1),
            Dm.kv(typeof(obj_item_ammo_casing_a40mm), 1),

            Dm.kv(typeof(obj_item_melee_energy_sword_saber), 1),
            Dm.kv(typeof(obj_item_melee_energy_axe), 1),
            Dm.kv(typeof(obj_item_fireaxe), 2),
            Dm.kv(typeof(obj_item_melee_baton_security_loaded), 2),
            Dm.kv(typeof(obj_item_knife_combat), 2),
            Dm.kv(typeof(obj_item_melee_chainofcommand), 2),
            Dm.kv(typeof(obj_item_spear), 2),
            Dm.kv(typeof(obj_item_claymore), 1),
            Dm.kv(typeof(obj_item_melee_baseball_bat), 2),

            Dm.kv(typeof(obj_item_clothing_suit_armor_vest), 2),
            Dm.kv(typeof(obj_item_clothing_suit_armor_bulletproof), 2),
            Dm.kv(typeof(obj_item_clothing_suit_armor_riot), 2),
            Dm.kv(typeof(obj_item_clothing_suit_armor_laserproof), 2),
            Dm.kv(typeof(obj_item_clothing_suit_armor_swat), 1),
            Dm.kv(typeof(obj_item_clothing_head_helmet_sec), 2),
            Dm.kv(typeof(obj_item_clothing_head_helmet_alt), 2),
            Dm.kv(typeof(obj_item_clothing_head_helmet_toggleable_riot), 2),
            Dm.kv(typeof(obj_item_shield_riot), 2),

            Dm.kv(typeof(obj_item_grenade_frag), 1),
            Dm.kv(typeof(obj_item_grenade_flashbang), 1));
    }

    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra); // spawns the first item (none in a map editor's world, DV504)
        return INITIALIZE_HINT_NORMAL; // and stays, where tg's spawner is deleted
    }

    public override async DmTask<object> make_item(object spawn_loc = null, object type_path_to_make = null)
    {
        object made = await base.make_item(spawn_loc, type_path_to_make);
        if (made is atom_movable item && !QDELETED(item))
        {
            held_item = item;
            RegisterSignal(item, COMSIG_MOVABLE_MOVED, nameof(on_item_moved));
            RegisterSignal(item, COMSIG_QDELETING, nameof(on_item_gone));
        }
        return made;
    }

    [SignalHandler]
    public virtual void on_item_moved(atom_movable source = null, atom old_loc = null, object dir = null, object forced = null)
    {
        if (source != null && source.loc != loc)
            release(source);
    }

    [SignalHandler]
    public virtual void on_item_gone(datum source = null) => release(source);

    private void release(datum item)
    {
        UnregisterSignal(item, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_QDELETING));
        if (held_item != item)
            return;
        held_item = null;
        if (QDELETED(this))
            return; // the pad goes with its item (a test's create and destroy)
        _addtimer(Dm.@new<datum_callback>(this, nameof(refill)), refill_delay);
    }

    public virtual void refill()
    {
        if (QDELETED(this) || held_item != null)
            return;
        Waiting.NoSleep(spawn_loot());
        do_sparks(3, FALSE, this);
    }
}
