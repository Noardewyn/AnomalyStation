// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/carp/magicarp.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4): the magic projectiles stay inert with their wizard (a row).

public partial class datum_controller_global_vars
{
    /// Associative list of magic projectiles to their display names
    // tg: code/modules/mob/living/basic/space_fauna/carp/magicarp.dm:2
    public DmList magicarp_spell_types;
    public virtual void InitGlobalmagicarp_spell_types()
    {
        magicarp_spell_types = Dm.list(
            Dm.kv(typeof(obj_projectile_magic_animate), "dancing"),
            Dm.kv(typeof(obj_projectile_magic_arcane_barrage), "arcane"),
            Dm.kv(typeof(obj_projectile_magic_babel), "babbling"),
            Dm.kv(typeof(obj_projectile_magic_change), "transforming"),
            Dm.kv(typeof(obj_projectile_magic_death), "grim"),
            Dm.kv(typeof(obj_projectile_magic_door), "unbarred"),
            Dm.kv(typeof(obj_projectile_magic_fireball), "blazing"),
            Dm.kv(typeof(obj_projectile_magic_freeze), "frigid"),
            Dm.kv(typeof(obj_projectile_magic_levitate), "weightless"),
            Dm.kv(typeof(obj_projectile_magic_resurrection), "vital"),
            Dm.kv(typeof(obj_projectile_magic_spellblade), "vorpal"),
            Dm.kv(typeof(obj_projectile_magic_swap), "quantum"),
            Dm.kv(typeof(obj_projectile_magic_teleport), "warping"));
        Dm.append(ref gvars_datum_init_order, "magicarp_spell_types");
    }

    /// Spells which xenobiology magicarp can cast
    // tg: code/modules/mob/living/basic/space_fauna/carp/magicarp.dm:19
    public DmList xenobiology_magicarp_spell_types;
    public virtual void InitGlobalxenobiology_magicarp_spell_types()
    {
        xenobiology_magicarp_spell_types = Dm.list(
            Dm.kv(typeof(obj_projectile_magic_animate), "dancing"),
            Dm.kv(typeof(obj_projectile_magic_arcane_barrage), "arcane"),
            Dm.kv(typeof(obj_projectile_magic_door), "unbarred"),
            Dm.kv(typeof(obj_projectile_magic_fireball), "blazing"),
            Dm.kv(typeof(obj_projectile_magic_freeze), "frigid"),
            Dm.kv(typeof(obj_projectile_magic_levitate), "weightless"),
            Dm.kv(typeof(obj_projectile_magic_spellblade), "vorpal"),
            Dm.kv(typeof(obj_projectile_magic_swap), "quantum"),
            Dm.kv(typeof(obj_projectile_magic_teleport), "warping"));
        Dm.append(ref gvars_datum_init_order, "xenobiology_magicarp_spell_types");
    }

    /// Associative list of magic projectiles to their colours
    // tg: code/modules/mob/living/basic/space_fauna/carp/magicarp.dm:32
    public DmList magicarp_spell_colours;
    public virtual void InitGlobalmagicarp_spell_colours()
    {
        magicarp_spell_colours = Dm.list(
            Dm.kv(typeof(obj_projectile_magic_animate), COLOR_CARP_RUSTY),
            Dm.kv(typeof(obj_projectile_magic_arcane_barrage), COLOR_CARP_PURPLE),
            Dm.kv(typeof(obj_projectile_magic_babel), COLOR_CARP_BROWN),
            Dm.kv(typeof(obj_projectile_magic_change), COLOR_CARP_PINK),
            Dm.kv(typeof(obj_projectile_magic_death), COLOR_CARP_DARK_BLUE),
            Dm.kv(typeof(obj_projectile_magic_door), COLOR_CARP_GREEN),
            Dm.kv(typeof(obj_projectile_magic_fireball), COLOR_CARP_RED),
            Dm.kv(typeof(obj_projectile_magic_freeze), COLOR_CARP_TURQUOISE),
            Dm.kv(typeof(obj_projectile_magic_levitate), COLOR_CARP_DARK_PURPLE),
            Dm.kv(typeof(obj_projectile_magic_resurrection), COLOR_CARP_PALE_GREEN),
            Dm.kv(typeof(obj_projectile_magic_spellblade), COLOR_CARP_SILVER),
            Dm.kv(typeof(obj_projectile_magic_swap), COLOR_CARP_LIGHT_BLUE),
            Dm.kv(typeof(obj_projectile_magic_teleport), COLOR_CARP_GRAPE));
        Dm.append(ref gvars_datum_init_order, "magicarp_spell_colours");
    }
}

/**
 * # Magicarp
 *
 * Carp which fires magic projectiles.
 */
// tg: code/modules/mob/living/basic/space_fauna/carp/magicarp.dm:52
public partial class mob_living_basic_carp_magic : mob_living_basic_carp
{
    // tg: code/modules/mob/living/basic/space_fauna/carp/magicarp.dm:73
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        allowed_projectile_types = spell_list();
        assign_spell();
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/carp/magicarp.dm:78
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref spell);
        return base.Destroy(force);
    }

    /// Returns the list of spells we are allowed to cast
    // tg: code/modules/mob/living/basic/space_fauna/carp/magicarp.dm:83
    public virtual DmList spell_list()
    {
        return GLOB.magicarp_spell_types;
    }

    /// Choose a spell
    // tg: code/modules/mob/living/basic/space_fauna/carp/magicarp.dm:87
    public virtual void assign_spell()
    {
        Type spell_type = pick(allowed_projectile_types) as Type;
        name = Dm.text("[] []", GLOB.magicarp_spell_types[spell_type], name);
        Waiting.NoSleep(set_greyscale(colors: Dm.list(GLOB.magicarp_spell_colours[spell_type])));

        spell = Dm.@new<datum_action_cooldown_mob_cooldown_projectile_attack_magicarp_bolt>(this);
        spell.projectile_type = spell_type;
        spell.button_icon_state = initial(spell_type, "icon_state") as string;
        Waiting.NoSleep(spell.Grant(this));
        (ai_controller as datum_ai_controller).set_blackboard_key(BB_MAGICARP_SPELL, spell);
        assign_spell_ai(spell_type);
    }

    /// Sets up AI for the spell we have chosen
    // tg: code/modules/mob/living/basic/space_fauna/carp/magicarp.dm:102
    public virtual void assign_spell_ai(object spell_type = null)
    {
        DmList spell_special_targeting = TgState.Current.mob_living_basic_carp_magic__spell_special_targeting ??= Dm.list(
            Dm.kv(typeof(obj_projectile_magic_animate), MAGICARP_SPELL_OBJECTS),
            Dm.kv(typeof(obj_projectile_magic_door), MAGICARP_SPELL_WALLS),
            Dm.kv(typeof(obj_projectile_magic_resurrection), MAGICARP_SPELL_CORPSES));
        (ai_controller as datum_ai_controller).set_blackboard_key(BB_MAGICARP_SPELL_SPECIAL_TARGETING, spell_special_targeting[spell_type]);
    }

    /// Shoot when you click away from you
    // tg: code/modules/mob/living/basic/space_fauna/carp/magicarp.dm:111
    public override async DmTask<object> RangedAttack(atom atom_target = null, object modifiers = null)
    {
        await spell.Trigger(null, null, atom_target);
        return null;
    }
}

/// Magicarp which fires a random spell each time
// tg: code/modules/mob/living/basic/space_fauna/carp/magicarp.dm:115
public partial class mob_living_basic_carp_magic_chaos : mob_living_basic_carp_magic
{
    // tg: code/modules/mob/living/basic/space_fauna/carp/magicarp.dm:121
    public override void assign_spell()
    {
        datum_action_cooldown_mob_cooldown_projectile_attack_magicarp_bolt_chaos chaos_bolt = Dm.@new<datum_action_cooldown_mob_cooldown_projectile_attack_magicarp_bolt_chaos>(this);
        chaos_bolt.permitted_projectiles = allowed_projectile_types;
        Waiting.NoSleep(chaos_bolt.Grant(this));
        spell = chaos_bolt;
        (ai_controller as datum_ai_controller).set_blackboard_key(BB_MAGICARP_SPELL, spell);
        RegisterSignal(spell, COMSIG_ACTION_TRIGGER, nameof(apply_colour));
    }
}

/// Spawned by xenobiology, can only fire safe spells
// tg: code/modules/mob/living/basic/space_fauna/carp/magicarp.dm:133
public partial class mob_living_basic_carp_magic_xenobiology : mob_living_basic_carp_magic
{
    // tg: code/modules/mob/living/basic/space_fauna/carp/magicarp.dm:136
    public override DmList spell_list()
    {
        return GLOB.xenobiology_magicarp_spell_types;
    }
}

/// Spawned by xenobiology, can only fire safe spells
// tg: code/modules/mob/living/basic/space_fauna/carp/magicarp.dm:140
public partial class mob_living_basic_carp_magic_chaos_xenobiology : mob_living_basic_carp_magic_chaos
{
    // tg: code/modules/mob/living/basic/space_fauna/carp/magicarp.dm:143
    public override DmList spell_list()
    {
        return GLOB.xenobiology_magicarp_spell_types;
    }
}

public sealed partial class TgState
{
    internal DmList mob_living_basic_carp_magic__spell_special_targeting;
}
