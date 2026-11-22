// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/grenades/clusterbuster.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps F1): the clusterbuster, its segments and the payload spawners. The fantasy bonuses are live-partial rows
// (fantasy affixes are not ported). The kinds whose payloads are still inert (EMP, the syndicate minibomb, the spawner
// grenades) scatter those payloads, which their own packages make live; the slime kinds' extracts are xenobiology's.

////////////////////
//Clusterbang
////////////////////

// tg: code/game/objects/items/grenades/clusterbuster.dm:8
public partial class obj_item_grenade_clusterbuster : obj_item_grenade
{
    // tg: code/game/objects/items/grenades/clusterbuster.dm:5  #define RANDOM_DETONATE_MIN_TIME (1.5 SECONDS) (file-local)
    public const float RANDOM_DETONATE_MIN_TIME = 1.5f * SECONDS;
    // tg: code/game/objects/items/grenades/clusterbuster.dm:6  #define RANDOM_DETONATE_MAX_TIME (6 SECONDS) (file-local)
    public const float RANDOM_DETONATE_MAX_TIME = 6 * SECONDS;

    // tg: code/game/objects/items/grenades/clusterbuster.dm:28
    public override async DmTask<object> detonate(mob_living lanced_by = null)
    {
        object dot = await base.detonate(lanced_by);
        if (!Dm.truthy(dot))
            return dot;

        await update_mob();
        float numspawned = rand(min_spawned, max_spawned);
        float again = 0;

        for (float _ = 1, n = numspawned; _ <= n; _++)
        {
            if (prob(segment_chance))
            {
                again++;
                numspawned--;
            }
        }

        for (float loop = 1; loop <= again; loop++)
            Dm.@new<obj_item_grenade_clusterbuster_segment>(drop_location(), this); //Creates 'segments' that launches a few more payloads

        Dm.@new(payload_spawner, drop_location(), payload, numspawned); //Launches payload
        playsound(this, grenade_arm_sound, 75, TRUE, -3);
        qdel(this);
        return dot;
    }
}

//////////////////////
//Clusterbang segment
//////////////////////
// tg: code/game/objects/items/grenades/clusterbuster.dm:52
public partial class obj_item_grenade_clusterbuster_segment : obj_item_grenade_clusterbuster
{
    // tg: code/game/objects/items/grenades/clusterbuster.dm:59
    [DmParams("mapload", "base")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_item_grenade_clusterbuster @base = Dm.arg<obj_item_grenade_clusterbuster>(__extra, 0);
        object dot = base.Initialize(mapload, __extra);

        if (@base != null)
        {
            name = Dm.text("[] segment", @base.name);
            base_state = Dm.text("[]_segment", @base.base_state);
            icon_state = base_state;
            payload_spawner = @base.payload_spawner;
            payload = @base.payload;
            grenade_arm_sound = @base.grenade_arm_sound;
            min_spawned = @base.min_spawned;
            max_spawned = @base.max_spawned;
        }
        icon_state = Dm.text("[]_active", base_state);
        active = TRUE;
        float steps = rand(1, 4);
        for (float step = 1; step <= steps; step++)
            Waiting.NoSleep(step_away(this, loc));
        _addtimer(Dm.@new<datum_callback>(this, nameof(detonate)), rand(RANDOM_DETONATE_MIN_TIME, RANDOM_DETONATE_MAX_TIME), file: "code/game/objects/items/grenades/clusterbuster.dm", line: 76);
        return dot;
    }

    // tg: code/game/objects/items/grenades/clusterbuster.dm:78
    public override async DmTask<object> detonate(mob_living lanced_by = null)
    {
        Dm.@new(payload_spawner, drop_location(), payload, rand(min_spawned, max_spawned));
        playsound(this, grenade_arm_sound, 75, TRUE, -3);
        qdel(this);
        return null;
    }
}

//////////////////////////////////
//The payload spawner effect
/////////////////////////////////
// tg: code/game/objects/items/grenades/clusterbuster.dm:86
public partial class obj_effect_payload_spawner : obj_effect
{
    // tg: code/game/objects/items/grenades/clusterbuster.dm:86
    [DmParams("mapload", "type", "numspawned")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Type payload_type = Dm.arg<Type>(__extra, 0);
        object numspawned = Dm.arg<object>(__extra, 1);
        base.Initialize(mapload, __extra);
        if (payload_type != null && isnum(numspawned))
            spawn_payload(payload_type, Dm.num(numspawned));
        return INITIALIZE_HINT_QDEL;
    }

    // tg: code/game/objects/items/grenades/clusterbuster.dm:92
    public virtual void spawn_payload(Type type = null, float numspawned = 0)
    {
        for (float _ = 1; _ <= numspawned; _++)
        {
            obj_item_grenade grenade = Dm.@new(type, loc) as obj_item_grenade;
            if (istype(grenade, typeof(obj_item_grenade)))
            {
                grenade.active = TRUE;
                grenade.type_cluster = TRUE;
                _addtimer(Dm.@new<datum_callback>(grenade, nameof(obj_item_grenade.detonate)), rand(obj_item_grenade_clusterbuster.RANDOM_DETONATE_MIN_TIME, obj_item_grenade_clusterbuster.RANDOM_DETONATE_MAX_TIME), file: "code/game/objects/items/grenades/clusterbuster.dm", line: 98);
            }
            float steps = rand(1, 4);
            for (float step = 1; step <= steps; step++)
                Waiting.NoSleep(step_away(this, loc)); // tg steps the spawner itself away from its own turf (a no-op), not the grenade
        }
    }
}

// tg: code/game/objects/items/grenades/clusterbuster.dm:103
public partial class obj_effect_payload_spawner_random_slime : obj_effect_payload_spawner
{
    // tg: code/game/objects/items/grenades/clusterbuster.dm:109
    public override void spawn_payload(Type type = null, float numspawned = 0)
    {
        for (float _ = 1; _ <= numspawned; _++)
        {
            Type chosen = pick(subtypesof(typeof(obj_item_slime_extract))) as Type;
            Dm.@new(chosen, loc);
            if (Dm.truthy(@volatile))
            {
                // addtimer(CALLBACK(slime_extract, TYPE_PROC_REF(/obj/item/slime_extract, auto_activate_reaction)), rand(...)): the
                // slime extracts are inert (xenobiology), so a volatile extract does not go off by itself
                Dm.Deferred("code/game/objects/items/grenades/clusterbuster.dm:113", "spawn_payload: auto_activate_reaction of a volatile slime extract (the slime extracts, xenobiology)");
            }
            float steps = rand(1, 4);
            for (float step = 1; step <= steps; step++)
                Waiting.NoSleep(step_away(this, loc));
        }
    }
}

//random clusterbuster spawner
// tg: code/game/objects/items/grenades/clusterbuster.dm:181
public partial class obj_item_grenade_clusterbuster_random : obj_item_grenade_clusterbuster
{
    // tg: code/game/objects/items/grenades/clusterbuster.dm:184
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        Type real_type = pick(subtypesof(typeof(obj_item_grenade_clusterbuster))) as Type;
        Dm.@new(real_type, loc);
        return INITIALIZE_HINT_QDEL;
    }
}
