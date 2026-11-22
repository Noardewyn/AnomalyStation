// Translated from tgstation (snapshot 1d99bd7f): code/modules/mod/modules/modules_service.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO2 (D333): the file whole. The service MOD modules: the bike horn, the advanced balloon blower, the microwave beam (cooks an
// item on the floor at a distance), the waddle module (squeaky boots and a clown's waddle), the janitorial mister (a recharging space
// cleaner sprayer) and the perfumer (washes its user). The var blocks are generated (D111).

///Bike Horn - Plays a bike horn sound.
// tg: code/modules/mod/modules/modules_service.dm:4
public partial class obj_item_mod_module_bikehorn : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_service.dm:16
    public override async DmTask<object> on_use(mob activator = null)
    {
        playsound(this, Dm.resource("sound/items/bikehorn.ogg"), 100, FALSE);
        await drain_power(use_energy_cost);
        return null;
    }
}

///Microwave Beam - Microwaves items instantly.
// tg: code/modules/mod/modules/modules_service.dm:30
public partial class obj_item_mod_module_microwave_beam : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_service.dm:44
    public override async DmTask<object> on_select_use(atom target = null)
    {
        object dot = await base.on_select_use(target);
        if (!Dm.truthy(dot))
            return dot;
        if (!isitem(target))
            return dot;
        if (!isturf(target.loc))
        {
            balloon_alert(mod.wearer, I18n.T("not in storage!"));
            return dot;
        }
        obj_item microwave_target = target as obj_item;
        do_sparks(2, TRUE, mod.wearer);
        mod.wearer.Beam(target, icon_state: Dm.text("lightning[]", rand(1, 12)), time: 5);
        if (Dm.truthy(Dm.bitand(microwave_target.microwave_act(microwaver: mod.wearer), COMPONENT_MICROWAVE_SUCCESS)))
            playsound(this, Dm.resource("sound/machines/microwave/microwave-end.ogg"), 50, FALSE);
        else
            balloon_alert(mod.wearer, I18n.T("can't be microwaved!"));
        do_sparks(2, TRUE, microwave_target);
        await drain_power(use_energy_cost);
        return dot;
    }
}

//Waddle - Makes you waddle and squeak.
// tg: code/modules/mod/modules/modules_service.dm:64
public partial class obj_item_mod_module_waddle : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_service.dm:78
    public override async DmTask on_part_activation()
    {
        obj_item shoes = mod.get_part_from_slot(ITEM_SLOT_FEET) as obj_item;
        if (shoes != null)
            await shoes.AddComponent(typeof(datum_component_squeak), Dm.list(Dm.kv(Dm.resource("sound/effects/footstep/clownstep1.ogg"), 1), Dm.kv(Dm.resource("sound/effects/footstep/clownstep2.ogg"), 1)), 50, Dm.named("falloff_exponent", 20)); //die off quick please
        await mod.wearer.AddElementTrait(TRAIT_WADDLING, REF(this), typeof(datum_element_waddling));
        if (is_clown_job(mod.wearer.mind?.assigned_role))
            await mod.wearer.add_mood_event("clownshoes", typeof(datum_mood_event_clownshoes));
    }

    // tg: code/modules/mod/modules/modules_service.dm:86
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        deleting ??= FALSE;
        obj_item shoes = mod.get_part_from_slot(ITEM_SLOT_FEET) as obj_item;
        if (shoes != null && !Dm.truthy(deleting))
            qdel(shoes.GetComponent(typeof(datum_component_squeak)));
        REMOVE_TRAIT(mod.wearer, TRAIT_WADDLING, REF(this));
        if (is_clown_job(mod.wearer.mind?.assigned_role))
            mod.wearer.clear_mood_event("clownshoes");
    }
}

// recharging cleaner spray module
// tg: code/modules/mod/modules/modules_service.dm:95
public partial class obj_item_mod_module_mister_cleaner : obj_item_mod_module_mister
{
    // tg: code/modules/mod/modules/modules_service.dm:103
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        (reagents as datum_reagents).flags = AMOUNT_VISIBLE;
        Waiting.NoSleep((reagents as datum_reagents).add_reagent(typeof(datum_reagent_space_cleaner), volume));
        return dot;
    }

    // tg: code/modules/mod/modules/modules_service.dm:108
    public override async DmTask on_active_process(object seconds_per_tick = null)
    {
        float refill_add = min(Dm.num(volume) - Dm.num((reagents as datum_reagents).total_volume), 2 * Dm.num(seconds_per_tick));
        if (refill_add > 0)
            await (reagents as datum_reagents).add_reagent(typeof(datum_reagent_space_cleaner), refill_add);
    }
}

// tg: code/modules/mod/modules/modules_service.dm:113
public partial class obj_item_mod_module_selfcleaner : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_service.dm:123
    public override async DmTask<object> on_use(mob activator = null)
    {
        await activator.wash(CLEAN_WASH);
        await drain_power(use_energy_cost);
        playsound(activator, Dm.resource("sound/effects/spray.ogg"), 50, FALSE);
        return null;
    }
}
