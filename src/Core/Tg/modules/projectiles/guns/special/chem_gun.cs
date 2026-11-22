// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/special/chem_gun.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate G4b (D316): the reagent gun, whole (its fantasy affixes: gaps GU1; the fantasy component that calls them is not ported).
// Its casing is in ammunition/special/syringe.cs.

//his isn't a subtype of the syringe gun because the syringegun subtype is made to hold syringes
//this is meant to hold reagents/obj/item/gun/syringe
// tg: code/modules/projectiles/guns/special/chem_gun.dm:3
public partial class obj_item_gun_chem : obj_item_gun
{
    // tg: code/modules/projectiles/guns/special/chem_gun.dm:21
    public override async DmTask apply_fantasy_bonuses(object bonus = null)
    {
        await base.apply_fantasy_bonuses(bonus);
        max_syringes = Dm.num(modify_fantasy_variable("max_syringes", max_syringes, bonus, minimum: 1));
        time_per_syringe = Dm.num(modify_fantasy_variable("time_per_syringe", time_per_syringe, -Dm.num(bonus) * 10));
    }

    // tg: code/modules/projectiles/guns/special/chem_gun.dm:26
    public override async DmTask remove_fantasy_bonuses(object bonus = null)
    {
        max_syringes = Dm.num(reset_fantasy_variable("max_syringes", max_syringes));
        time_per_syringe = Dm.num(reset_fantasy_variable("time_per_syringe", time_per_syringe));
        await base.remove_fantasy_bonuses(bonus);
    }

    // tg: code/modules/projectiles/guns/special/chem_gun.dm:32
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        chambered = Dm.@new<obj_item_ammo_casing_chemgun>(this);
        START_PROCESSING(SSobj, this);
        create_reagents(90, OPENCONTAINER);
        return dot;
    }

    // tg: code/modules/projectiles/guns/special/chem_gun.dm:38
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        STOP_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/modules/projectiles/guns/special/chem_gun.dm:42
    public override object can_shoot()
    {
        return syringes_left;
    }

    // tg: code/modules/projectiles/guns/special/chem_gun.dm:45
    public override async DmTask handle_chamber(float? empty_chamber = null, float? from_firing = null, float? chamber_next_round = null)
    {
        if (chambered != null && chambered.loaded_projectile == null && Dm.truthy(syringes_left))
            chambered.newshot();
    }

    // tg: code/modules/projectiles/guns/special/chem_gun.dm:49
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (syringes_left >= max_syringes)
            return null;
        if (Dm.num(world.time) < last_synth + time_per_syringe)
            return null;
        to_chat(loc, span_warning(I18n.T("You hear a click as [] synthesizes a new dart.", this)));
        syringes_left++;
        if (chambered != null && chambered.loaded_projectile == null)
            chambered.newshot();
        last_synth = Dm.num(world.time);
        return null;
    }
}
