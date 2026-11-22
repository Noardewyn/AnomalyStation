// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/decals/cleanable/ants.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the admin var edit and the emissive light (DV12) (gaps package C6).

// tg: code/game/objects/effects/decals/cleanable/ants.dm:1
public partial class obj_effect_decal_cleanable_ants : obj_effect_decal_cleanable
{
    // tg: code/game/objects/effects/decals/cleanable/ants.dm:13
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (Dm.truthy(mapload) && reagent_amount > 2)
            reagent_amount = rand(reagent_amount - 2, reagent_amount);
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(update_ant_damage());
        return dot;
    }

    // tg: code/game/objects/effects/decals/cleanable/ants.dm:24
    public override async DmTask handle_merge_decal(obj_effect_decal_cleanable merger = null)
    {
        await base.handle_merge_decal(merger);
        await (merger as obj_effect_decal_cleanable_ants).update_ant_damage();
    }

    // tg: code/game/objects/effects/decals/cleanable/ants.dm:29
    public virtual async DmTask update_ant_damage(object ant_min_damage = null, object ant_max_damage = null)
    {
        if (!Dm.truthy(ant_max_damage))
            ant_max_damage = Dm.min(10, round((reagents != null ? Dm.num((reagents as datum_reagents).get_reagent_amount(typeof(datum_reagent_ants))) : reagent_amount) * 0.1f, 0.1f)); // 100u ants = 10 max_damage
        if (!Dm.truthy(ant_min_damage))
            ant_min_damage = 0.1f;
        object ant_flags = Dm.bitor(CALTROP_NOCRAWL, CALTROP_NOSTUN); /// Small amounts of ants won't be able to bite through shoes.
        if (Dm.num(ant_max_damage) > 1)
            ant_flags = Dm.bitor(Dm.bitor(CALTROP_NOCRAWL, CALTROP_NOSTUN), CALTROP_BYPASS_SHOES);

        if (GetComponent(typeof(datum_component_caltrop)) is datum_component_caltrop caltrop_comp)
        {
            caltrop_comp.min_damage = Dm.num(ant_min_damage);
            caltrop_comp.max_damage = Dm.num(ant_max_damage);
            caltrop_comp.flags = Dm.num(ant_flags);
            caltrop_comp.soundfile = bite_sound;
        }
        else
            await AddComponent(typeof(datum_component_caltrop), Dm.named("min_damage", ant_min_damage), Dm.named("max_damage", ant_max_damage), Dm.named("flags", ant_flags), Dm.named("soundfile", bite_sound));

        update_appearance(UPDATE_ICON);
    }

    // tg: code/game/objects/effects/decals/cleanable/ants.dm:49
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (istype(this, typeof(obj_effect_decal_cleanable_ants_fire))) //i fucking hate this but you're forced to call parent in update_icon_state()
            return await base.update_icon_state(__extra);
        if (!Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)))
            return await base.update_icon_state(__extra);

        if (GetComponent(typeof(datum_component_caltrop)) is not datum_component_caltrop caltrop_comp)
            return await base.update_icon_state(__extra);

        float max_damage = Dm.num(caltrop_comp.max_damage);
        object initial_state = Dm.initial(GetType(), (atom p) => p.icon_state);
        if (max_damage <= 1)
            icon_state = initial_state as string;
        else if (max_damage <= 4)
            icon_state = Dm.text("[]_2", initial_state);
        else if (max_damage <= 7)
            icon_state = Dm.text("[]_3", initial_state);
        else
            icon_state = Dm.text("[]_4", initial_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/effects/decals/cleanable/ants.dm:70
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        // the emissive overlay of the live ants: DV12
        return await base.update_overlays(__extra);
    }

    // tg: code/game/objects/effects/decals/cleanable/ants.dm:74
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        Dm.@new<obj_effect_decal_cleanable_ants_fire>(loc);
        qdel(this);
        return null;
    }
}

// tg: code/game/objects/effects/decals/cleanable/ants.dm:78
public partial class obj_effect_decal_cleanable_ants_fire : obj_effect_decal_cleanable_ants
{
    // tg: code/game/objects/effects/decals/cleanable/ants.dm:85
    public override async DmTask update_ant_damage(object ant_min_damage = null, object ant_max_damage = null)
    {
        await base.update_ant_damage(15f, 25f);
    }

    // tg: code/game/objects/effects/decals/cleanable/ants.dm:88
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        return null;
    }
}
