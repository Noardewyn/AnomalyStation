// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/explodable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

///Component specifically for explosion sensetive things, currently only applies to heat based explosions but can later perhaps be used for things that are dangerous to handle carelessly like nitroglycerin.
// tg: code/datums/components/explodable.dm:2
public partial class datum_component_explodable : datum_component
{
    // tg: code/datums/components/explodable.dm:22
    [DmParams("devastation_range", "heavy_impact_range", "light_impact_range", "flame_range", "flash_range", "uncapped", "delete_after")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object devastation_range = Dm.arg(__extra, 0);
        object heavy_impact_range = Dm.arg(__extra, 1);
        object light_impact_range = Dm.arg(__extra, 2);
        object flame_range = Dm.arg(__extra, 3);
        object flash_range = Dm.arg(__extra, 4);
        object uncapped = Dm.arg(__extra, 5) ?? FALSE;
        object delete_after = Dm.arg(__extra, 6) ?? EXPLODABLE_DELETE_PARENT;
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;

        RegisterSignal(parent, COMSIG_ATOM_ATTACKBY, nameof(explodable_attack));
        RegisterSignal(parent, COMSIG_ATOM_EX_ACT, nameof(detonate));
        RegisterSignal(parent, COMSIG_ATOM_TOOL_ACT(TOOL_WELDER), nameof(welder_react));
        RegisterSignal(parent, COMSIG_ATOM_BULLET_ACT, nameof(projectile_react));
        if (ismovable(parent))
        {
            RegisterSignal(parent, COMSIG_MOVABLE_IMPACT, nameof(explodable_impact));
            RegisterSignal(parent, COMSIG_MOVABLE_BUMP, nameof(explodable_bump));
            if (isitem(parent))
            {
                RegisterSignals(parent, Dm.list(COMSIG_ITEM_ATTACK, COMSIG_ITEM_ATTACK_ATOM, COMSIG_ITEM_HIT_REACT), nameof(explodable_attack));
                if (isclothing(parent))
                {
                    RegisterSignal(parent, COMSIG_ITEM_EQUIPPED, nameof(on_equip));
                    RegisterSignal(parent, COMSIG_ITEM_DROPPED, nameof(on_drop));
                }
            }
        }

        atom atom_parent = parent as atom;
        if (atom_parent.atom_storage != null)
            RegisterSignal(parent, COMSIG_ATOM_ENTERED, nameof(explodable_insert_item));

        if (Dm.truthy(devastation_range))
            this.devastation_range = Dm.num(devastation_range);
        if (Dm.truthy(heavy_impact_range))
            this.heavy_impact_range = Dm.num(heavy_impact_range);
        if (Dm.truthy(light_impact_range))
            this.light_impact_range = Dm.num(light_impact_range);
        if (Dm.truthy(flame_range))
            this.flame_range = Dm.num(flame_range);
        if (Dm.truthy(flash_range))
            this.flash_range = Dm.num(flash_range);
        this.uncapped = uncapped;
        this.delete_after = delete_after;
        return null;
    }

    /// Explode if our parent is a storage place and something with high heat is inserted in.
    // tg: code/datums/components/explodable.dm:57
    [SignalHandler]
    public virtual void explodable_insert_item(datum source = null, obj_item I = null)
    {
        if (I == null || !Dm.truthy(Dm.bitand(I.item_flags, IN_STORAGE)))
            return;
        check_if_detonate(I);
    }

    // tg: code/datums/components/explodable.dm:63
    [SignalHandler]
    public virtual void explodable_impact(datum source = null, atom hit_atom = null, datum_thrownthing throwing_datum = null, object caught = null)
    {
        if (!Dm.truthy(caught))
            check_if_detonate(hit_atom);
    }

    // tg: code/datums/components/explodable.dm:69
    [SignalHandler]
    public virtual void explodable_bump(datum source = null, atom A = null)
    {
        check_if_detonate(A);
    }

    ///Called when you use this object to attack sopmething
    // tg: code/datums/components/explodable.dm:75
    [SignalHandler]
    public virtual void explodable_attack(datum source = null, atom_movable target = null, mob_living user = null)
    {
        check_if_detonate(target);
    }

    /// Welder check. Here because tool_act is higher priority than attackby.
    // tg: code/datums/components/explodable.dm:81
    [SignalHandler]
    public virtual object welder_react(datum source = null, mob user = null, obj_item tool = null)
    {
        if (Dm.truthy(check_if_detonate(tool)))
            return ITEM_INTERACT_BLOCKING;
        return null;
    }

    /// Shot by something
    // tg: code/datums/components/explodable.dm:88
    [SignalHandler]
    public virtual void projectile_react(datum source = null, obj_projectile shot = null)
    {
        if (Dm.eq(shot.damage_type, BURN) && Dm.num(shot.damage) > 0)
            detonate();
    }

    ///Called when you attack a specific body part of the thing this is equipped on. Useful for exploding pants.
    // tg: code/datums/components/explodable.dm:95
    [SignalHandler]
    public virtual void explodable_attack_zone(datum source = null, object damage = null, object damagetype = null, object def_zone = null, params object[] __extra)
    {
        if (!Dm.truthy(def_zone))
            return;
        if (!Dm.eq(damagetype, BURN)) //Don't bother if it's not fire.
            return;
        if (isbodypart(def_zone))
        {
            obj_item_bodypart hitting = def_zone as obj_item_bodypart;
            def_zone = hitting.body_zone;
        }
        if (!Dm.truthy(is_hitting_zone(def_zone))) //You didn't hit us! ha!
            return;
        detonate();
    }

    // tg: code/datums/components/explodable.dm:109
    [SignalHandler]
    public virtual void on_equip(datum source = null, mob equipper = null, object slot = null)
    {
        RegisterSignal(equipper, COMSIG_MOB_APPLY_DAMAGE, nameof(explodable_attack_zone), TRUE);
    }

    // tg: code/datums/components/explodable.dm:114
    [SignalHandler]
    public virtual void on_drop(datum source = null, mob user = null)
    {
        UnregisterSignal(user, COMSIG_MOB_APPLY_DAMAGE);
    }

    /// Checks if we're hitting the zone this component is covering
    // tg: code/datums/components/explodable.dm:120
    public virtual object is_hitting_zone(object def_zone = null)
    {
        obj_item item = parent as obj_item;
        mob_living_carbon wearer = item.loc as mob_living_carbon; //Get whoever is equipping the item currently
        if (wearer == null)
            return FALSE;

        DmList worn_items = wearer.get_equipped_items() as DmList;

        if (!Dm.@in(item, worn_items))
            return FALSE;

        if (Dm.truthy(Dm.bitand(item.body_parts_covered, body_zone2cover_flags(def_zone))))
            return TRUE;
        return FALSE;
    }

    // tg: code/datums/components/explodable.dm:135
    public virtual object check_if_detonate(object target = null)
    {
        if (!isitem(target))
            return null;
        obj_item I = target as obj_item;
        if (Dm.num(I.get_temperature()) >= FIRE_MINIMUM_TEMPERATURE_TO_EXIST)
        {
            detonate(); //If we're touching a hot item we go boom
            return TRUE;
        }
        return null;
    }

    /// Explode and remove the object
    // tg: code/datums/components/explodable.dm:144
    [SignalHandler]
    public virtual void detonate()
    {
        if (Dm.truthy(exploding))
            return; // If we don't do this and this doesn't delete it can lock the MC into only processing Input, Timers, and Explosions.

        atom bomb = parent as atom;
        float do_log = light_impact_range >= 1 ? TRUE : FALSE;

        exploding = TRUE;
        Waiting.NoSleep(explosion(bomb, devastation_range, heavy_impact_range, light_impact_range, flame_range, flash_range, do_log, Dm.num(uncapped))); //epic explosion time

        if (Dm.eq(delete_after, EXPLODABLE_DELETE_SELF))
        {
            qdel(this);
        }
        else if (Dm.eq(delete_after, EXPLODABLE_DELETE_PARENT))
        {
            if (isobj(bomb))
            {
                obj obj_bomb = bomb as obj;
                Waiting.NoSleep(obj_bomb.deconstruct(disassembled: FALSE));
            }
            else
            {
                qdel(bomb);
            }
        }
        else
        {
            _addtimer(Dm.@new<datum_callback>(this, nameof(reset_exploding)), 0.1f * SECONDS, file: "code/datums/components/explodable.dm", line: 165);
        }
    }

    /**
     * Resets the expoding flag
     */
    // tg: code/datums/components/explodable.dm:170
    [SignalHandler]
    public virtual void reset_exploding()
    {
        exploding = FALSE;
    }
}
