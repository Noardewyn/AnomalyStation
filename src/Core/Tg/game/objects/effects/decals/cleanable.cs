// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/decals/cleanable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M3 I1a): the cleanable decal's life cycle (Initialize with the random icon state and the merge of a second decal
// of its type, Destroy, washing, the reagent holder's lazy creation), so that the mapped dirt, cobwebs and other messes are
// live; the beauty element since gaps MS1. Not here: the reagents a decal carries into its holder and merges (reagents, M8),
// diseases (M8); scooping and the combat-mode attack are generated live-partial stubs (docs/ai/specs/live-partial.txt, ## I1);
// the bloodcrawl check came with Bubblegum, who warps through blood. spawn_unique_cleanable (M4b: welding fuel from the fire
// extender makes a fuel pool); spawn_glitter (a /turf proc of this file; gaps ST3, the glittery crates). The var block is generated (D111).

// tg: code/game/objects/effects/decals/cleanable.dm:1
[DmPath("/obj/effect/decal/cleanable")]
public partial class obj_effect_decal_cleanable : obj_effect_decal
{
    // tg: code/game/objects/effects/decals/cleanable.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        DmList diseases = Dm.arg<DmList>(__extra, 0);
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(LAZYLEN(random_icon_states)))
        {
            icon_state = pick(random_icon_states) as string;
            base_icon_state = icon_state;
        }

        if (isturf(loc))
        {
            foreach (obj_effect_decal_cleanable other in Dm.each<obj_effect_decal_cleanable>(loc))
            {
                if (other != this && other.type == type && !QDELETED(other) && Dm.truthy(replace_decal(other)))
                {
                    Waiting.NoSleep(handle_merge_decal(other));
                    return INITIALIZE_HINT_QDEL;
                }
            }
        }

        if (Dm.truthy(is_mopped))
            ADD_TRAIT(this, TRAIT_MOPABLE, INNATE_TRAIT);

        if (Dm.truthy(LAZYLEN(diseases)))
            Waiting.NoSleep(add_diseases(diseases));

        Waiting.NoSleep(AddElement(typeof(datum_element_beauty), beauty));

        turf our_turf = get_turf(this);
        if (Dm.truthy(our_turf) && Dm.truthy(is_station_level(our_turf.z)))
            SSblackbox.record_feedback("tally", "station_mess_created", 1, name);
        return dot;
    }

    // tg: code/game/objects/effects/decals/cleanable.dm:44
    public override object Destroy(float? force = null)
    {
        turf our_turf = get_turf(this);
        if (Dm.truthy(our_turf) && Dm.truthy(is_station_level(our_turf.z)))
            SSblackbox.record_feedback("tally", "station_mess_destroyed", 1, name);
        return base.Destroy(force);
    }

    // tg: code/game/objects/effects/decals/cleanable.dm:50
    public virtual async DmTask add_diseases(DmList diseases = null)
    {
        DmList diseases_to_add = Dm.list();
        foreach (datum_disease disease in Dm.each_cast<datum_disease>(diseases))
        {
            if (Dm.truthy(Dm.bitand(disease.spread_flags, DISEASE_SPREAD_CONTACT_FLUIDS)))
                Dm.append(diseases_to_add, disease);
        }
        if (Dm.truthy(LAZYLEN(diseases_to_add)))
            await AddComponent(typeof(datum_component_infective), diseases_to_add);
    }

    /// Check if we should give up in favor of the pre-existing decal
    // tg: code/game/objects/effects/decals/cleanable.dm:59
    public virtual object replace_decal(obj_effect_decal_cleanable other = null)
    {
        if (Dm.truthy(mergeable_decal))
            return TRUE;
        return null;
    }

    // tg: code/game/objects/effects/decals/cleanable.dm:63
    public override async DmTask<object> wash(object clean_types = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.wash(clean_types, __extra);
        if (Dm.truthy(dot) || Dm.truthy(Dm.bitand(clean_types, clean_type)))
        {
            qdel(this);
            dot = Dm.bitor(dot, Dm.bitor(COMPONENT_CLEANED, COMPONENT_CLEANED_GAIN_XP));
        }
        return dot;
    }

    // tg: code/game/objects/effects/decals/cleanable.dm:69
    public virtual async DmTask handle_merge_decal(obj_effect_decal_cleanable merger = null)
    {
        if (!Dm.truthy(reagents) && !Dm.truthy(decal_reagent))
            return;

        // the merge of the two decals' reagents (:73-79): the decal_reagent hand-over or reagents.trans_to into the merger,
        // whose holder procs (add_reagent, trans_to) are not in the port yet
        Dm.Deferred("code/game/objects/effects/decals/cleanable.dm:73", "handle_merge_decal: the merged decal takes the reagents of the deleted one (reagents, M8)");
    }

    /// Returns reagents datum if it exists, or lazyloads one if it doesn't
    // tg: code/game/objects/effects/decals/cleanable.dm:81
    public virtual async DmTask<object> lazy_init_reagents()
    {
        if (Dm.truthy(reagents))
            return reagents;
        return await init_reagents(decal_reagent, reagent_amount);
    }

    // tg: code/game/objects/effects/decals/cleanable.dm:87
    public virtual async DmTask<object> init_reagents(object reagent = null, object amount = null)
    {
        if (!Dm.truthy(reagent))
            return null;

        create_reagents(amount);
        await ((datum_reagents)reagents).add_reagent(reagent, amount);
        return reagents;
    }
}

// tg: code/game/objects/effects/decals/cleanable.dm:137 (interleaved types, rules 1.9; M4b: the fuel pool of welding fuel)
public partial class turf : atom
{
    /**
     * A helper to spawn a cleanable of this type on this turf (or below, if a groundless turf), or return the existing one
     */
    // tg: code/game/objects/effects/decals/cleanable.dm:137
    public virtual object spawn_unique_cleanable(Type cleanable_type = null)
    {
        turf checkturf = this;
        while (isgroundlessturf(checkturf) && Dm.truthy(checkturf.zPassOut(DOWN)))
        {
            turf below = GET_TURF_BELOW(checkturf) as turf;
            if (below == null || !Dm.truthy(below.zPassIn(DOWN)))
                break;
            checkturf = below;
        }

        // There is no need to spam unique cleanables, they don't stack and it just chews cpu
        obj_effect_decal_cleanable existing = Dm.locate_type(cleanable_type, checkturf) as obj_effect_decal_cleanable;
        if (existing != null)
            return existing;
        return Dm.@new(cleanable_type, checkturf);
    }

    // tg: code/game/objects/effects/decals/cleanable.dm:151  (gaps ST3: the glittery crates leave glitter where they were)
    public virtual void spawn_glitter(DmList glitter_colors = null)
    {
        obj_effect_decal_cleanable_glitter new_glitter = spawn_unique_cleanable(typeof(obj_effect_decal_cleanable_glitter)) as obj_effect_decal_cleanable_glitter;
        new_glitter.color = pick_weight(glitter_colors);
    }
}

// tg: code/game/objects/effects/decals/cleanable.dm:115 (Bubblegum's blood warp and blood attacks)
public partial class obj_effect_decal_cleanable : obj_effect_decal
{
    /// Checks if this decal can be bloodcrawled in
    // tg: code/game/objects/effects/decals/cleanable.dm:116
    public virtual object can_bloodcrawl_in()
    {
        if (decal_reagent == typeof(datum_reagent_blood) || Dm.truthy((reagents as datum_reagents)?.has_reagent(typeof(datum_reagent_blood))))
            return TRUE;

        DmList blood_DNA = GET_ATOM_BLOOD_DECALS(this);
        foreach (object blood_key in Dm.each(blood_DNA))
        {
            datum_blood_type blood_type = blood_DNA[blood_key] as datum_blood_type;
            if (blood_type.reagent_type == typeof(datum_reagent_blood))
                return TRUE;
        }
        return null;
    }
}
