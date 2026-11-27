// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/decals/cleanable/mess.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the dirt and the dust (M3 I1a: their init, the tiled dirt's smoothing), the glass debris (M5x slice 1: its init and
// ex_act, so a blast clears the debris of the windows it breaks), the shreds (M6 slice 2: shredded clothing leaves them), the vomit's slurping (gaps S1), the ashes (syndicate G4a), the glowing goo (silicons SI1c), the glitter (gaps ST3), the old vomit and the black bile a legion's tumour makes its host throw up; the other messes' procs come with their modules. The vomit's [DmPath] is package B's (wave 4: the signatures of its turf and area files name the type, rules 2.3).
// The var blocks are generated (D111).

//Vomit (sorry)
// tg: code/game/objects/effects/decals/cleanable/mess.dm:190
[DmPath("/obj/effect/decal/cleanable/vomit")]
public partial class obj_effect_decal_cleanable_vomit : obj_effect_decal_cleanable
{
    // tg: code/game/objects/effects/decals/cleanable/mess.dm:198
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot) || !HAS_TRAIT(user, TRAIT_VOMIT_SLURPER))
            return dot;
        playsound(get_turf(this), Dm.resource("sound/items/drink.ogg"), 50, TRUE); //slurp
        user.visible_message(span_alert(I18n.T("[] extends a small proboscis into the vomit pool, sucking it with a slurping sound.", user)));
        if (await lazy_init_reagents() is datum_reagents pool)
            await pool.trans_to(user, ((datum_reagents)reagents).total_volume, transferred_by: user, methods: INGEST);
        qdel(this);
        return dot;
    }
}

// tg: code/game/objects/effects/decals/cleanable/mess.dm:35
public partial class obj_effect_decal_cleanable_glass : obj_effect_decal_cleanable
{
    // tg: code/game/objects/effects/decals/cleanable/mess.dm:42
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (Dm.eq(dot, INITIALIZE_HINT_QDEL))
            return dot;
        Waiting.NoSleep(setDir(pick(GLOB.cardinals)));
        return dot;
    }

    // tg: code/game/objects/effects/decals/cleanable/mess.dm:48
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        qdel(this);
        return TRUE;
    }
}

// tg: code/game/objects/effects/decals/cleanable/mess.dm:66
public partial class obj_effect_decal_cleanable_dirt : obj_effect_decal_cleanable
{
    // tg: code/game/objects/effects/decals/cleanable/mess.dm:80
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (Dm.eq(dot, INITIALIZE_HINT_QDEL))
            return dot;
        icon_state = pick("dirt-flat-0", "dirt-flat-1", "dirt-flat-2", "dirt-flat-3") as string;
        obj_structure_broken_flooring broken_flooring = Dm.locate<obj_structure_broken_flooring>(loc);
        if (broken_flooring != null)
            return dot;
        turf T = get_turf(this);
        if (Dm.truthy(T.tiled_turf) && Dm.truthy(is_tileable))
        {
            icon = Dm.resource("icons/effects/dirt.dmi");
            icon_state = "dirt-0";
            smoothing_flags = SMOOTH_BITMASK;
            QUEUE_SMOOTH(this);
        }
        if (Dm.truthy(Dm.bitand(smoothing_flags, USES_SMOOTHING)))
            QUEUE_SMOOTH_NEIGHBORS(this);
        return dot;
    }

    // tg: code/game/objects/effects/decals/cleanable/mess.dm:97
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(Dm.bitand(smoothing_flags, USES_SMOOTHING)))
            QUEUE_SMOOTH_NEIGHBORS(this);
        return base.Destroy(force);
    }
}

// tg: code/game/objects/effects/decals/cleanable/mess.dm:102
public partial class obj_effect_decal_cleanable_dirt_dust : obj_effect_decal_cleanable_dirt
{
    // tg: code/game/objects/effects/decals/cleanable/mess.dm:109
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        icon_state = base_icon_state as string;
        return dot;
    }
}

// tg: code/game/objects/effects/decals/cleanable/mess.dm:282
public partial class obj_effect_decal_cleanable_shreds : obj_effect_decal_cleanable
{
    // tg: code/game/objects/effects/decals/cleanable/mess.dm:289
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        if (Dm.num(severity) >= EXPLODE_DEVASTATE) //so shreds created during an explosion aren't deleted by the explosion.
        {
            qdel(this);
            return TRUE;
        }

        return FALSE;
    }

    // tg: code/game/objects/effects/decals/cleanable/mess.dm:296
    [DmParams("mapload", "oldname")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object oldname = Dm.arg(__extra, 0);
        object dot = null;
        pixel_x = rand(-10, 10);
        pixel_y = rand(-10, 10);
        if (oldname != null)
            desc = I18n.T("The sad remains of what used to be \\a [].", oldname);
        dot = base.Initialize(mapload, Dm.forward(__extra, oldname));
        return dot;
    }
}

// Syndicate G4a (D316): the ashes (junk mail can hold them).
// tg: code/game/objects/effects/decals/cleanable/mess.dm:8
public partial class obj_effect_decal_cleanable_ash : obj_effect_decal_cleanable
{
    // tg: code/game/objects/effects/decals/cleanable/mess.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        pixel_x = base_pixel_x + rand(-5, 5);
        pixel_y = base_pixel_y + rand(-5, 5);
        return dot;
    }

    // tg: code/game/objects/effects/decals/cleanable/mess.dm:25
    public override object NeverShouldHaveComeHere(turf here_turf = null)
    {
        return !istype(here_turf, typeof(obj_structure_bodycontainer_crematorium)) && Dm.truthy(base.NeverShouldHaveComeHere(here_turf));
    }
}

// Silicons SI1c (D326): the glowing goo (the remains' acid leaves it).
// tg: code/game/objects/effects/decals/cleanable/mess.dm:113
public partial class obj_effect_decal_cleanable_greenglow : obj_effect_decal_cleanable
{
    // tg: code/game/objects/effects/decals/cleanable/mess.dm:122
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        return FALSE;
    }
}

// tg: code/game/objects/effects/decals/cleanable/mess.dm:125
public partial class obj_effect_decal_cleanable_greenglow_filled : obj_effect_decal_cleanable_greenglow
{
    // tg: code/game/objects/effects/decals/cleanable/mess.dm:129
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        decal_reagent = pick(typeof(datum_reagent_uranium), typeof(datum_reagent_uranium_radium)) as Type;
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/game/objects/effects/decals/cleanable/mess.dm:138
public partial class obj_effect_decal_cleanable_greenglow_radioactive : obj_effect_decal_cleanable_greenglow
{
    // tg: code/game/objects/effects/decals/cleanable/mess.dm:145
    [DmParams("mapload", "diseases")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.eq(dot, INITIALIZE_HINT_QDEL))
            return dot;
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_radioactive_emitter),
            Dm.named("cooldown_time", 5 * SECONDS),
            Dm.named("range", 4),
            Dm.named("threshold", RAD_MEDIUM_INSULATION)));
        return dot;
    }
}

// tg: code/game/objects/effects/decals/cleanable/mess.dm:303  (gaps ST3: the glittery crates' glitter)
public partial class obj_effect_decal_cleanable_glitter : obj_effect_decal_cleanable
{
    // tg: code/game/objects/effects/decals/cleanable/mess.dm:311
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (Dm.eq(dot, INITIALIZE_HINT_QDEL))
            return dot;
        add_overlay(mutable_appearance(Dm.resource("icons/effects/glitter.dmi"), "glitter_sparkle" + rand(1, 9), appearance_flags: EMISSIVE_APPEARANCE_FLAGS));
        return dot;
    }
}

// tg: code/game/objects/effects/decals/cleanable/mess.dm:257
public partial class obj_effect_decal_cleanable_vomit_old : obj_effect_decal_cleanable_vomit
{
    // tg: code/game/objects/effects/decals/cleanable/mess.dm:261
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.eq(dot, INITIALIZE_HINT_QDEL))
            return dot;
        icon_state += "-old";
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_SLUDGE, CELL_VIRUS_TABLE_GENERIC, rand(2, 4), 10)); // inert until the cell lines (cytology)
        return dot;
    }
}
