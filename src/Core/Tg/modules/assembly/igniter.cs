// Translated from tgstation (snapshot 1d99bd7f): code/modules/assembly/igniter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M5 slice 10, the flamethrower): the igniter (its sparks, activate: the hotspot at its turf, the holder's signal and
// the sparks; attack_self and ignition_effect) and the condenser's activate (the turf's air cooled). The flamethrower's two
// igniter procs (flamethrower_process, ignite_turf) are in game/objects/items/weaponry/ranged/flamethrower.cs, as in tg.
// suicide_act (M10) is a generated live-partial stub. The var blocks are generated.

// tg: code/modules/assembly/igniter.dm:6
public partial class obj_item_assembly_igniter : obj_item_assembly
{
    // tg: code/modules/assembly/igniter.dm:1  #define EXPOSED_VOLUME 1000 (file-local, #undef at :73)
    private const int EXPOSED_VOLUME = 1000;
    // tg: code/modules/assembly/igniter.dm:2  #define ROOM_TEMP 293 (file-local, #undef at :74)
    protected const int ROOM_TEMP = 293;
    // tg: code/modules/assembly/igniter.dm:3  #define MIN_FREEZE_TEMP 50 (file-local, #undef at :75)
    protected const int MIN_FREEZE_TEMP = 50;
    // tg: code/modules/assembly/igniter.dm:4  #define MAX_FREEZE_TEMP 1000000 (file-local, #undef at :76)
    protected const int MAX_FREEZE_TEMP = 1000000;

    // tg: code/modules/assembly/igniter.dm:22
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        sparks = Dm.@new<datum_effect_system_basic_spark_spread>(this, 2, FALSE);
        sparks.attach(this);
        return dot;
    }

    // tg: code/modules/assembly/igniter.dm:27
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref sparks);
        return base.Destroy(force);
    }

    // tg: code/modules/assembly/igniter.dm:31
    public override async DmTask<object> activate(mob activator = null)
    {
        if (!Dm.truthy(await base.activate(activator)))
            return FALSE; //Cooldown check
        turf location = get_turf(loc);
        if (Dm.truthy(location))
            location.hotspot_expose(heat, EXPOSED_VOLUME);
        if (Dm.truthy(holder))
        {
            atom signal_target = holder.loc;
            if (Dm.truthy(signal_target._listen_lookup?[COMSIG_IGNITER_ACTIVATE]))
                signal_target._SendSignal(COMSIG_IGNITER_ACTIVATE, Dm.list(signal_target, this));
        }
        if (QDELETED(this))
            return TRUE;
        await sparks.start();
        return TRUE;
    }

    // tg: code/modules/assembly/igniter.dm:44
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        // Don't trigger when interacting with assemblies
        if (!Dm.truthy(holder))
            await activate();
        // tg: code/modules/assembly/igniter.dm:48
        add_fingerprint(user);
        return null;
    }

    // tg: code/modules/assembly/igniter.dm:50
    public override async DmTask<object> ignition_effect(atom A = null, mob user = null)
    {
        object dot = span_notice(I18n.T("[] fiddles with [], and manages to light [].", user, this, A));
        await activate();
        // tg: code/modules/assembly/igniter.dm:53
        add_fingerprint(user);
        return dot;
    }
}

//For the Condenser, which functions like the igniter but makes things colder.
// tg: code/modules/assembly/igniter.dm:56
public partial class obj_item_assembly_igniter_condenser : obj_item_assembly_igniter
{
    // tg: code/modules/assembly/igniter.dm:63
    public override async DmTask<object> activate(mob activator = null)
    {
        object dot = await base.activate(activator);
        if (!Dm.truthy(dot))
            return dot; //Cooldown check
        turf location = get_turf(loc);
        if (Dm.truthy(location))
        {
            var enviro = (datum_gas_mixture)await location.return_air();
            enviro.temperature = clamp(min(ROOM_TEMP, Dm.num(enviro.temperature) * 0.85f), MIN_FREEZE_TEMP, MAX_FREEZE_TEMP);
            location.air_update_turf(FALSE, FALSE);
        }
        return dot;
    }
}
