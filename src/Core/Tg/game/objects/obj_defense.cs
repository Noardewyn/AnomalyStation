// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/obj_defense.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL. M4a P2: /obj/hitby; M4b slice 3: /obj/fire_act with custom_fire_overlay; M5 slice 1 (atom integrity): the damage of
// hitby and fire_act, the burning component, force_pushed, move_crushed, collision_damage, acid_melt, burn, the
// deconstruction chain (atom_deconstruct, handle_deconstruct, deconstruct) and /obj/atom_destruction.
// The M7 projectiles slice: bullet_act :30. M5x slice 1 (explosions, D149): ex_act :10 and GetExplosionBlock :218. M5x slice 5: singularity_act :100
// (the singularity). M7 zaps: zap_act :148 and zap_buckle_check :157. Not declared, their /atom roots not being declared
// (each with its milestone): blob_act :63 (blobs, M10+),
// acid_act :110 came with the acid (silicons SI1c, D326); attack_alien :68 with the xenomorphs (gaps X5). The MetaStation leftovers:
// attack_hulk :53 (a hulk's punch).

// tg: code/game/objects/obj_defense.dm:2
public partial class obj : atom_movable
{
    // tg: code/game/objects/obj_defense.dm:10  (set waitfor = FALSE, inherited from /atom/ex_act: the wrapper of waiting-model.md
    // 5.4; take_damage may sleep)
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(ex_act__body(severity, target, __extra, dot), dot);
    }

    private async DmTask<object> ex_act__body(object severity, object target, object[] __extra, DotBox<object> dot)
    {
        if (Dm.truthy(Dm.bitand(resistance_flags, Dm.bitor(INDESTRUCTIBLE, BOMB_PROOF))))
            return FALSE;

        dot.Value = base.ex_act(severity, target, __extra); //contents explosion
        if (QDELETED(this))
            return TRUE;
        // DEVIATION(DV27): a type whose own damage procs are not translated (D132) takes no blast damage, as it takes no hit
        if (InertSeam.MissingDamageOverride(this))
            return dot.Value;
        if (target == this)
        {
            await take_damage(INFINITY, BRUTE, BOMB, 0);
            return TRUE;
        }
        switch (Dm.num(severity))
        {
            case EXPLODE_DEVASTATE:
                await take_damage(INFINITY, BRUTE, BOMB, 0);
                break;
            case EXPLODE_HEAVY:
                await take_damage(rand(100, 250), BRUTE, BOMB, 0);
                break;
            case EXPLODE_LIGHT:
                await take_damage(rand(10, 90), BRUTE, BOMB, 0);
                break;
        }

        return TRUE;
    }

    // tg: code/game/objects/obj_defense.dm:2
    // (the slot's parameter name hitting_atom is kept; tg names it hit_by here)
    public override async DmTask<object> hitby(atom_movable hitting_atom = null, object skipcatch = null, object hitpush = null, object blocked = null, datum_thrownthing throwingdatum = null)
    {
        atom_movable hit_by = hitting_atom;
        await base.hitby(hit_by, skipcatch, hitpush, blocked, throwingdatum);
        float damage_taken = hit_by.throwforce;
        if (isitem(hit_by))
        {
            obj_item as_item = hit_by as obj_item;
            damage_taken *= Dm.num(as_item.get_demolition_modifier(this));
        }
        // DEVIATION(DV27): a type whose own damage procs are not translated refuses the hit to the thrower (D132).
        if (InertSeam.MissingDamageOverride(this, throwingdatum?.get_thrower() as mob))
            return null;
        await take_damage(damage_taken, BRUTE, MELEE, 1, get_dir(this, hit_by));
        return null;
    }

    // tg: code/game/objects/obj_defense.dm:30  (the M7 projectiles slice)
    public override async DmTask<object> bullet_act(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, float? blocked = null)
    {
        piercing_hit ??= FALSE;
        object dot = null;
        dot = await base.bullet_act(hitting_projectile, def_zone, piercing_hit, blocked);
        if (!Dm.eq(dot, BULLET_ACT_HIT))
            return dot;

        float damage_sustained = 0;
        // DEVIATION(DV27): a type whose own damage procs are not translated takes no damage from the shot (D132)
        if (!QDELETED(this) && !InertSeam.MissingDamageOverride(this)) //Bullet on_hit effect might have already destroyed this object
        {
            damage_sustained = Dm.num(await take_damage(
                Dm.num(hitting_projectile.damage) * Dm.num(hitting_projectile.get_demolition_modifier(this)),
                hitting_projectile.damage_type,
                hitting_projectile.armor_flag,
                FALSE,
                REVERSE_DIR(hitting_projectile.dir),
                hitting_projectile.armour_penetration
            ));
        }
        if (hitting_projectile.suppressed != SUPPRESSED_VERY)
        {
            visible_message(
                span_danger(I18n.T("[] is hit by \\a [][]!", this, hitting_projectile, (Dm.truthy(damage_sustained) ? "" : I18n.T(", []", no_damage_feedback)))),
                vision_distance: COMBAT_MESSAGE_RANGE
            );
        }

        return (damage_sustained > 0 ? BULLET_ACT_HIT : BULLET_ACT_BLOCK);
    }

    // tg: code/game/objects/obj_defense.dm:89
    public override async DmTask<object> force_pushed(atom_movable pusher = null, float? force = null, object direction = null)
    {
        return TRUE;
    }

    // tg: code/game/objects/obj_defense.dm:92
    public override async DmTask<object> move_crushed(atom_movable pusher = null, float? force = null, object direction = null)
    {
        force ??= MOVE_FORCE_DEFAULT;
        await collision_damage(pusher, force, direction);
        return TRUE;
    }

    // tg: code/game/objects/obj_defense.dm:96
    public virtual async DmTask collision_damage(atom_movable pusher = null, float? force = null, object direction = null)
    {
        force ??= MOVE_FORCE_DEFAULT;
        float amt = Dm.max(0, ((force.Value - (Dm.num(move_resist) * MOVE_FORCE_CRUSH_RATIO)) / (Dm.num(move_resist) * MOVE_FORCE_CRUSH_RATIO)) * 10);
        await take_damage(amt, BRUTE, attack_dir: REVERSE_DIR(Dm.num(direction)));
    }

    ///// ACID

    ///the obj's reaction when touched by acid
    // tg: code/game/objects/obj_defense.dm:110
    public override async DmTask<object> acid_act(object acidpwr = null, object acid_volume = null, params object[] __extra)
    {
        object dot = await base.acid_act(acidpwr, acid_volume, __extra);
        if (Dm.truthy(Dm.bitand(resistance_flags, UNACIDABLE)) || Dm.le(acid_volume, 0) || Dm.le(acidpwr, 0))
            return FALSE;

        await AddComponent(typeof(datum_component_acid), acidpwr, acid_volume, Dm.truthy(custom_acid_overlay) ? custom_acid_overlay : GLOB.acid_overlay);
        return TRUE;
    }

    ///called when the obj is destroyed by acid.
    // tg: code/game/objects/obj_defense.dm:119
    public virtual async DmTask acid_melt()
    {
        await deconstruct(FALSE);
    }

    //// FIRE

    ///Called when the obj is exposed to fire.
    // tg: code/game/objects/obj_defense.dm:125
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        if (HAS_TRAIT(this, TRAIT_UNDERFLOOR))
            return null;
        float potential_damage = 0.02f * Dm.num(exposed_temperature);
        if (Dm.truthy(exposed_temperature) && !Dm.truthy(Dm.bitand(resistance_flags, FIRE_PROOF)) && (potential_damage > damage_deflection))
            await take_damage(clamp(potential_damage, 0, 20), BURN, FIRE, 0);
        if (QDELETED(this)) // take_damage() can send our obj to an early grave, let's stop here if that happens
            return null;
        if (!Dm.truthy(Dm.bitand(resistance_flags, ON_FIRE)) && Dm.truthy(Dm.bitand(resistance_flags, FLAMMABLE)) && !Dm.truthy(Dm.bitand(resistance_flags, FIRE_PROOF)))
        {
            object fire_overlay = custom_fire_overlay__proc() is var or__1 && Dm.truthy(or__1) ? or__1 : GLOB.fire_overlay;
            await AddComponent(typeof(datum_component_burning), fire_overlay, burning_particles);
            if (Dm.truthy(_listen_lookup?[COMSIG_ATOM_FIRE_ACT]))
                _SendSignal(COMSIG_ATOM_FIRE_ACT, Dm.list(this, exposed_temperature, exposed_volume));
            return TRUE;
        }
        return await base.fire_act(exposed_temperature, exposed_volume);
    }

    /// Returns a custom fire overlay, if any
    // tg: code/game/objects/obj_defense.dm:140
    public virtual object custom_fire_overlay__proc()
    {
        return custom_fire_overlay;
    }

    /// Should be called when the atom is destroyed by fire, comparable to acid_melt() proc
    // tg: code/game/objects/obj_defense.dm:144
    public virtual async DmTask burn__proc()
    {
        await deconstruct(FALSE);
    }

    /**
     * Custom behaviour per atom subtype on how they should deconstruct themselves
     * Arguments
     *
     * * disassembled - TRUE means we cleanly took this atom apart using tools. FALSE means this was destroyed in a violent way
     */
    // tg: code/game/objects/obj_defense.dm:169
    public virtual async DmTask atom_deconstruct(float? disassembled = null)
    {
        // PROTECTED_PROC(TRUE)
        return;
    }

    /**
     * The interminate proc between deconstruct() & atom_deconstruct(). By default this delegates deconstruction to
     * atom_deconstruct if NO_DEBRIS_AFTER_DECONSTRUCTION is absent but subtypes can override this to handle NO_DEBRIS_AFTER_DECONSTRUCTION in their
     * own unique way. Override this if for example you want to dump out important content like mobs from the
     * atom before deconstruction regardless if NO_DEBRIS_AFTER_DECONSTRUCTION is present or not
     * Arguments
     *
     * * disassembled - TRUE means we cleanly took this atom apart using tools. FALSE means this was destroyed in a violent way
     */
    // tg: code/game/objects/obj_defense.dm:183
    public virtual async DmTask handle_deconstruct(float? disassembled = null)
    {
        // SHOULD_CALL_PARENT(FALSE)
        disassembled ??= TRUE;
        if (!Dm.truthy(Dm.bitand(obj_flags, NO_DEBRIS_AFTER_DECONSTRUCTION)))
            await atom_deconstruct(disassembled);
    }

    /**
     * The obj is deconstructed into pieces, whether through careful disassembly or when destroyed.
     * Arguments
     *
     * * disassembled - TRUE means we cleanly took this atom apart using tools. FALSE means this was destroyed in a violent way
     */
    // tg: code/game/objects/obj_defense.dm:195
    public virtual async DmTask deconstruct(float? disassembled = null)
    {
        // SHOULD_NOT_OVERRIDE(TRUE)
        disassembled ??= TRUE;
        // DEVIATION(DV27): an object whose own handle_deconstruct or atom_deconstruct is not translated is not taken apart
        // (D132): neither its parent's debris nor its deletion.
        if (InertSeam.MissingDeconstructOverride(this))
            return;

        //allow objects to deconstruct themselves
        await handle_deconstruct(disassembled);

        //inform objects we were deconstructed
        if (Dm.truthy(_listen_lookup?[COMSIG_OBJ_DECONSTRUCT]))
            _SendSignal(COMSIG_OBJ_DECONSTRUCT, Dm.list(this, disassembled));

        //delete our self
        qdel(this);
    }

    ///what happens when the obj's integrity reaches zero.
    // tg: code/game/objects/obj_defense.dm:208
    public override async DmTask atom_destruction(object damage_flag = null)
    {
        await base.atom_destruction(damage_flag);
        if (Dm.eq(damage_flag, ACID))
            await acid_melt();
        else if (Dm.eq(damage_flag, FIRE))
            await burn__proc();
        else
            await deconstruct(FALSE);
    }

    ///returns how much the object blocks an explosion. Used by subtypes.
    // tg: code/game/objects/obj_defense.dm:218
    public virtual object GetExplosionBlock()
    {
        throw Dm.CRASH("Unimplemented GetExplosionBlock()", "code/game/objects/obj_defense.dm", 219);
    }
}

// tg: code/game/objects/obj_defense.dm:100 (interleaved types, rules 1.9; M5x slice 5)
public partial class obj : atom_movable
{
    // tg: code/game/objects/obj_defense.dm:100
    public override async DmTask<object> singularity_act()
    {
        Dm.append(ref SSexplosions.high_mov_atom, this);
        if (this != null && !QDELETED(this))
            qdel(this);
        return 2;
    }
}

// tg: code/game/objects/obj_defense.dm:148 (interleaved types, rules 1.9; the M7 zaps slice)
public partial class obj : atom_movable
{
    ///Called when the obj is hit by a tesla bolt.
    // tg: code/game/objects/obj_defense.dm:148
    public override async DmTask<object> zap_act(object power = null, object zap_flags = null)
    {
        // DEVIATION(DV27): the interaction guard (D132) on an object whose own tg zap_act is not translated (a ridden vehicle):
        // nothing of the object's happens and the bolt ends there (the /atom root's null)
        if (InertSeam.MissingOverride(this, "zap_act"))
            return null;
        if (QDELETED(this))
            return 0;
        ADD_TRAIT(this, TRAIT_BEING_SHOCKED, WAS_SHOCKED);
        _addtimer(TRAIT_CALLBACK_REMOVE(this, TRAIT_BEING_SHOCKED, WAS_SHOCKED), 1 * SECONDS, file: "code/game/objects/obj_defense.dm", line: 152);
        return Dm.div(power, 2);
    }

    //The surgeon general warns that being buckled to certain objects receiving powerful shocks is greatly hazardous to your health
    ///Only tesla coils, vehicles, and grounding rods currently call this because mobs are already targeted over all other objects, but this might be useful for more things later.
    // tg: code/game/objects/obj_defense.dm:157
    public virtual async DmTask zap_buckle_check(object strength = null)
    {
        if (Dm.truthy(has_buckled_mobs()))
        {
            foreach (object m in Dm.each(buckled_mobs))
            {
                mob_living buckled_mob = m as mob_living;
                await buckled_mob.electrocute_act(clamp(round(Dm.num(strength) * 1.25e-3f), 10, 90) + rand(-5, 5), this, flags: SHOCK_TESLA);
            }
        }
    }
}

// Gaps package C1: a basic mob's or simple animal's attack on an object.
public partial class obj : atom_movable
{
    // tg: code/game/objects/obj_defense.dm:72  (the user's same-named vars read by name, as the living mob's)
    public override async DmTask<object> attack_animal(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_animal(user, modifiers);
        mob_living animal = (mob_living)user;
        object obj_damage = Dm.get_var(animal, "obj_damage");
        if (!Dm.truthy(animal.melee_damage_upper) && !Dm.truthy(obj_damage))
        {
            await animal.emote("custom", message: I18n.T("[] [].", Dm.get_var(animal, "friendly_verb_continuous"), this));
            return FALSE;
        }
        else
        {
            turf current_turf = get_turf(this); //we want to save the turf to play the sound there, cause being destroyed deletes us!
            object play_soundeffect = Dm.get_var(animal, "environment_smash");
            string damage_type = Dm.get_var(animal, "melee_damage_type") as string;
            float armour_penetration = Dm.num(Dm.get_var(animal, "armour_penetration"));
            if (Dm.truthy(obj_damage))
                dot = await attack_generic(animal, Dm.num(obj_damage), damage_type, MELEE, Dm.num(play_soundeffect), armour_penetration);
            else
                dot = await attack_generic(animal, rand(Dm.num(animal.melee_damage_lower), Dm.num(animal.melee_damage_upper)), damage_type, MELEE, Dm.num(play_soundeffect), armour_penetration);
            if (Dm.truthy(dot) && Dm.truthy(play_soundeffect))
                playsound(current_turf, Dm.resource("sound/effects/meteorimpact.ogg"), 100, TRUE);
            if (Dm.truthy(animal.client))
                log_combat(animal, this, "attacked");
        }
        return dot;
    }
}

// tg: code/game/objects/obj_defense.dm:63  (interleaved, rules 1.9)
public partial class obj : atom_movable
{
    // tg: code/game/objects/obj_defense.dm:53
    public override async DmTask<object> attack_hulk(mob_living_carbon_human user = null, params object[] __extra)
    {
        await base.attack_hulk(user, __extra);
        if (Dm.truthy(density))
            playsound(this, Dm.resource("sound/effects/meteorimpact.ogg"), 100, TRUE);
        else
            playsound(this, Dm.resource("sound/effects/bang.ogg"), 50, TRUE);
        object damage = await take_damage(hulk_damage(), BRUTE, MELEE, 0, get_dir(this, user));
        string feedback = Dm.truthy(damage) ? "" : Dm.text(", []", no_damage_feedback);
        user.visible_message(span_danger(I18n.T("[] smashes [][]!", user, this, feedback)), span_danger(I18n.T("You smash [][]!", this, feedback)), null, COMBAT_MESSAGE_RANGE);
        return TRUE;
    }

    // tg: code/game/objects/obj_defense.dm:63
    public override async DmTask<object> blob_act(obj_structure_blob attacking_blob = null)
    {
        if (!Dm.truthy(await base.blob_act(attacking_blob)) || HAS_TRAIT(this, TRAIT_UNDERFLOOR))
            return null;
        await take_damage(400, BRUTE, MELEE, 0, get_dir(this, attacking_blob));
        return null;
    }

    // tg: code/game/objects/obj_defense.dm:68  (gaps X5: an alien's slash at an object)
    public override async DmTask<object> attack_alien(mob_living_carbon user = null, DmList modifiers = null)
    {
        if (Dm.truthy(await attack_generic(user, 60, BRUTE, MELEE, 0)))
            playsound(this.loc, Dm.resource("sound/items/weapons/slash.ogg"), 100, TRUE);
        return null;
    }
}
