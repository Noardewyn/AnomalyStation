// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/floor/mineral_floor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps G2 (D291): the whole file. The mineral floors keep their own picture until broken or burnt (their material's effects
// come from the material, the plasma floor's fire among them); bananium squeaks and honks, uranium irradiates whoever steps on
// it and its uranium neighbours.

/* In this file:
 *
 * Plasma floor
 * Gold floor
 * Silver floor
 * Bananium floor
 * Diamond floor
 * Uranium floor
 * Shuttle floor (Titanium)
 */

// tg: code/game/turfs/open/floor/mineral_floor.dm:12
public partial class turf_open_floor_mineral : turf_open_floor
{
    // tg: code/game/turfs/open/floor/mineral_floor.dm:22
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icons = typelist("icons", icons) as DmList;
        return dot;
    }

    // tg: code/game/turfs/open/floor/mineral_floor.dm:26
    public override object broken_states()
    {
        return icon_state == null ? Dm.list() : Dm.list(Dm.text("[]_dam", Dm.initial(this, nameof(icon_state))));
    }

    // tg: code/game/turfs/open/floor/mineral_floor.dm:29
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (!Dm.truthy(broken) && !Dm.truthy(burnt) && !Dm.@in(icon_state, icons))
            icon_state = (string)Dm.initial(this, nameof(icon_state));
        return await base.update_icon_state(__extra);
    }
}

//Plasma floor that can't be removed, for disco inferno
// tg: code/game/turfs/open/floor/mineral_floor.dm:46
public partial class turf_open_floor_mineral_plasma_disco : turf_open_floor_mineral_plasma
{
    // tg: code/game/turfs/open/floor/mineral_floor.dm:46
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return null;
    }
}

// tg: code/game/turfs/open/floor/mineral_floor.dm:54
public partial class turf_open_floor_mineral_plasma_tiled : turf_open_floor_mineral_plasma
{
    // tg: code/game/turfs/open/floor/mineral_floor.dm:54
    public override object broken_states()
    {
        return Dm.list("damaged1", "damaged2", "damaged3", "damaged4", "damaged5");
    }
}

// tg: code/game/turfs/open/floor/mineral_floor.dm:72
public partial class turf_open_floor_mineral_gold_tiled : turf_open_floor_mineral_gold
{
    // tg: code/game/turfs/open/floor/mineral_floor.dm:72
    public override object broken_states()
    {
        return Dm.list("damaged1", "damaged2", "damaged3", "damaged4", "damaged5");
    }
}

// tg: code/game/turfs/open/floor/mineral_floor.dm:80
public partial class turf_open_floor_mineral_gold_sun : turf_open_floor_mineral_gold
{
    // tg: code/game/turfs/open/floor/mineral_floor.dm:80
    public override object broken_states()
    {
        return Dm.list("damaged1", "damaged2", "damaged3", "damaged4", "damaged5");
    }
}

// tg: code/game/turfs/open/floor/mineral_floor.dm:97
public partial class turf_open_floor_mineral_silver_tiled : turf_open_floor_mineral_silver
{
    // tg: code/game/turfs/open/floor/mineral_floor.dm:97
    public override object broken_states()
    {
        return Dm.list("damaged1", "damaged2", "damaged3", "damaged4", "damaged5");
    }
}

// tg: code/game/turfs/open/floor/mineral_floor.dm:105
public partial class turf_open_floor_mineral_silver_moon : turf_open_floor_mineral_silver
{
    // tg: code/game/turfs/open/floor/mineral_floor.dm:105
    public override object broken_states()
    {
        return Dm.list("damaged1", "damaged2", "damaged3", "damaged4", "damaged5");
    }
}

// tg: code/game/turfs/open/floor/mineral_floor.dm:117
public partial class turf_open_floor_mineral_titanium : turf_open_floor_mineral
{
    // tg: code/game/turfs/open/floor/mineral_floor.dm:117
    public override object broken_states()
    {
        return Dm.list("damaged1", "damaged2", "damaged3", "damaged4", "damaged5");
    }
}

// tg: code/game/turfs/open/floor/mineral_floor.dm:161
public partial class turf_open_floor_mineral_titanium_tiled : turf_open_floor_mineral_titanium
{
    // tg: code/game/turfs/open/floor/mineral_floor.dm:161
    public override object broken_states()
    {
        return Dm.list("damaged1", "damaged2", "damaged3", "damaged4", "damaged5");
    }
}

// tg: code/game/turfs/open/floor/mineral_floor.dm:205
public partial class turf_open_floor_mineral_plastitanium : turf_open_floor_mineral
{
    // tg: code/game/turfs/open/floor/mineral_floor.dm:205
    public override object broken_states()
    {
        return Dm.list("damaged1", "damaged2", "damaged3", "damaged4", "damaged5");
    }
}

// tg: code/game/turfs/open/floor/mineral_floor.dm:260
public partial class turf_open_floor_mineral_bananium_tiled : turf_open_floor_mineral_bananium
{
    // tg: code/game/turfs/open/floor/mineral_floor.dm:260
    public override object broken_states()
    {
        return Dm.list("damaged1", "damaged2", "damaged3", "damaged4", "damaged5");
    }
}

// tg: code/game/turfs/open/floor/mineral_floor.dm:320
public partial class turf_open_floor_mineral_diamond_tiled : turf_open_floor_mineral_diamond
{
    // tg: code/game/turfs/open/floor/mineral_floor.dm:320
    public override object broken_states()
    {
        return Dm.list("damaged1", "damaged2", "damaged3", "damaged4", "damaged5");
    }
}

// tg: code/game/turfs/open/floor/mineral_floor.dm:341
public partial class turf_open_floor_mineral_uranium_tiled : turf_open_floor_mineral_uranium
{
    // tg: code/game/turfs/open/floor/mineral_floor.dm:341
    public override object broken_states()
    {
        return Dm.list("damaged1", "damaged2", "damaged3", "damaged4", "damaged5");
    }
}

// tg: code/game/turfs/open/floor/mineral_floor.dm:398
public partial class turf_open_floor_mineral_bluespace : turf_open_floor_mineral
{
    // tg: code/game/turfs/open/floor/mineral_floor.dm:398
    public override object broken_states()
    {
        return Dm.list("damaged1", "damaged2", "damaged3", "damaged4", "damaged5");
    }
}

// tg: code/game/turfs/open/floor/mineral_floor.dm:414
public partial class turf_open_floor_mineral_telecrystal : turf_open_floor_mineral
{
    // tg: code/game/turfs/open/floor/mineral_floor.dm:414
    public override object broken_states()
    {
        return Dm.list("damaged1", "damaged2", "damaged3", "damaged4", "damaged5");
    }
}

// tg: code/game/turfs/open/floor/mineral_floor.dm:430
public partial class turf_open_floor_mineral_adamantine : turf_open_floor_mineral
{
    // tg: code/game/turfs/open/floor/mineral_floor.dm:430
    public override object broken_states()
    {
        return Dm.list("damaged1", "damaged2", "damaged3", "damaged4", "damaged5");
    }
}

// tg: code/game/turfs/open/floor/mineral_floor.dm:446
public partial class turf_open_floor_mineral_metal_hydrogen : turf_open_floor_mineral
{
    // tg: code/game/turfs/open/floor/mineral_floor.dm:446
    public override object broken_states()
    {
        return Dm.list("damaged1", "damaged2", "damaged3", "damaged4", "damaged5");
    }
}

// tg: code/game/turfs/open/floor/mineral_floor.dm:462
public partial class turf_open_floor_mineral_runite : turf_open_floor_mineral
{
    // tg: code/game/turfs/open/floor/mineral_floor.dm:462
    public override object broken_states()
    {
        return Dm.list("damaged1", "damaged2", "damaged3", "damaged4", "damaged5");
    }
}

// tg: code/game/turfs/open/floor/mineral_floor.dm:478
public partial class turf_open_floor_mineral_mythril : turf_open_floor_mineral
{
    // tg: code/game/turfs/open/floor/mineral_floor.dm:478
    public override object broken_states()
    {
        return Dm.list("damaged1", "damaged2", "damaged3", "damaged4", "damaged5");
    }
}

// tg: code/game/turfs/open/floor/mineral_floor.dm:245
public partial class turf_open_floor_mineral_bananium : turf_open_floor_mineral
{
    // tg: code/game/turfs/open/floor/mineral_floor.dm:263
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_loc, old_locs);
        if (Dm.truthy(dot))
            return dot;
        if (isliving(arrived))
            squeak();
        return dot;
    }

    // tg: code/game/turfs/open/floor/mineral_floor.dm:270  (the port's root returns nothing, so tg's . is null here)
    public override async DmTask initialize_occupant(atom_movable occupant = null)
    {
        await base.initialize_occupant(occupant);
        if (isliving(occupant))
            squeak();
    }

    // tg: code/game/turfs/open/floor/mineral_floor.dm:277
    public override async DmTask<object> attackby(obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.attackby(attacking_item, user, modifiers, attack_modifiers);
        if (!Dm.truthy(dot))
            honk();
        return dot;
    }

    // tg: code/game/turfs/open/floor/mineral_floor.dm:282
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (!Dm.truthy(dot))
            honk();
        return dot;
    }

    // tg: code/game/turfs/open/floor/mineral_floor.dm:287
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_paw(user, modifiers);
        if (!Dm.truthy(dot))
            honk();
        return dot;
    }

    // tg: code/game/turfs/open/floor/mineral_floor.dm:292
    public virtual void honk()
    {
        if (sound_cooldown < Dm.num(world.time))
        {
            playsound(this, Dm.resource("sound/items/bikehorn.ogg"), 50, TRUE);
            sound_cooldown = Dm.num(world.time) + 20;
        }
    }

    // tg: code/game/turfs/open/floor/mineral_floor.dm:297
    public virtual void squeak()
    {
        if (sound_cooldown < Dm.num(world.time))
        {
            playsound(this, SFX_CLOWN_STEP, 50, TRUE);
            sound_cooldown = Dm.num(world.time) + 10;
        }
    }
}

// tg: code/game/turfs/open/floor/mineral_floor.dm:325
public partial class turf_open_floor_mineral_uranium : turf_open_floor_mineral
{
    // tg: code/game/turfs/open/floor/mineral_floor.dm:344
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_loc, old_locs);
        if (Dm.truthy(dot))
            return dot;
        if (isliving(arrived))
            radiate();
        return dot;
    }

    // tg: code/game/turfs/open/floor/mineral_floor.dm:351  (the port's root returns nothing, so tg's . is null here)
    public override async DmTask initialize_occupant(atom_movable occupant = null)
    {
        await base.initialize_occupant(occupant);
        if (isliving(occupant))
            radiate();
    }

    // tg: code/game/turfs/open/floor/mineral_floor.dm:358
    public override async DmTask<object> attackby(obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.attackby(attacking_item, user, modifiers, attack_modifiers);
        if (!Dm.truthy(dot))
            radiate();
        return dot;
    }

    // tg: code/game/turfs/open/floor/mineral_floor.dm:363
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (!Dm.truthy(dot))
            radiate();
        return dot;
    }

    // tg: code/game/turfs/open/floor/mineral_floor.dm:368
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_paw(user, modifiers);
        if (!Dm.truthy(dot))
            radiate();
        return dot;
    }

    // tg: code/game/turfs/open/floor/mineral_floor.dm:373
    public virtual void radiate()
    {
        if (!Dm.truthy(active))
        {
            if (Dm.num(world.time) > last_event + 15)
            {
                active = TRUE;
                radiation_pulse(
                    this,
                    max_range: 1,
                    threshold: RAD_VERY_LIGHT_INSULATION,
                    chance: (URANIUM_IRRADIATION_CHANCE / 3f),
                    minimum_exposure_time: URANIUM_RADIATION_MINIMUM_EXPOSURE_TIME);
                foreach (turf_open_floor_mineral_uranium T in Dm.each<turf_open_floor_mineral_uranium>(orange(1, this)))
                    T.radiate();
                last_event = Dm.num(world.time);
                active = FALSE;
                return;
            }
        }
    }
}

// ALIEN ALLOY
// tg: code/game/turfs/open/floor/mineral_floor.dm:487
public partial class turf_open_floor_mineral_abductor : turf_open_floor_mineral
{
    // tg: code/game/turfs/open/floor/mineral_floor.dm:497
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        return base.Initialize(mapload, __extra);
    }

    // tg: code/game/turfs/open/floor/mineral_floor.dm:501
    public override object break_tile()
    {
        return null; //unbreakable
    }

    // tg: code/game/turfs/open/floor/mineral_floor.dm:504
    public override object burn_tile()
    {
        return null; //unburnable
    }
}
