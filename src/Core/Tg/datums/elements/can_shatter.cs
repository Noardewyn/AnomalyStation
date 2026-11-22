// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/can_shatter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10x closing: the whole file (a glass object shatters when thrown; reached by a dimensional anomaly's glass theme). The var block is
// generated.

/**
 * When attached to something, will make that thing shatter into shards on throw impact or z level falling
 * Or even when used as a weapon if the 'shatters_as_weapon' arg is TRUE
 */
// tg: code/datums/elements/can_shatter.dm:5
public partial class datum_element_can_shatter : datum_element
{
    // tg: code/datums/elements/can_shatter.dm:16
    [DmParams("target", "shard_type", "number_of_shards", "shattering_sound", "shatters_as_weapon")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object shard_type = Dm.arg(__extra, 0) ?? typeof(obj_item_plate_shard);
        object number_of_shards = Dm.arg(__extra, 1) ?? 5;
        object shattering_sound = Dm.arg(__extra, 2) ?? Dm.resource("sound/items/ceramic_break.ogg");
        object shatters_as_weapon = Dm.arg(__extra, 3) ?? FALSE;
        object dot = await base.Attach(target, Dm.forward(__extra, shard_type, number_of_shards, shattering_sound, shatters_as_weapon));

        if (!ismovable(target))
            return ELEMENT_INCOMPATIBLE;

        this.shard_type = shard_type as Type;
        this.number_of_shards = number_of_shards;
        this.shattering_sound = shattering_sound;

        RegisterSignal(target, COMSIG_MOVABLE_IMPACT, nameof(on_throw_impact));
        RegisterSignal(target, COMSIG_ATOM_ON_Z_IMPACT, nameof(on_z_impact));
        if (Dm.truthy(shatters_as_weapon))
            RegisterSignal(target, COMSIG_ITEM_AFTERATTACK, nameof(on_post_attack_atom));
        return dot;
    }

    // tg: code/datums/elements/can_shatter.dm:37
    [DmParams("target")]
    public override object Detach(datum target = null, params object[] __extra)
    {
        object dot = base.Detach(target, __extra);
        UnregisterSignal(target, Dm.list(COMSIG_MOVABLE_IMPACT, COMSIG_ATOM_ON_Z_IMPACT));
        return dot;
    }

    /// Tells the parent to shatter if we impact a lower zlevel
    // tg: code/datums/elements/can_shatter.dm:43
    [SignalHandler]
    public virtual void on_z_impact(datum source = null, turf impacted_turf = null, object levels = null)
    {
        shatter(source as atom_movable, impacted_turf);
    }

    /// Tells the parent to shatter if we are thrown and impact something
    // tg: code/datums/elements/can_shatter.dm:49
    [SignalHandler]
    public virtual void on_throw_impact(datum source = null, atom hit_atom = null, datum_thrownthing throwing_datum = null, object caught = null)
    {
        if (Dm.truthy(caught))
            return;
        shatter(source as atom_movable, hit_atom);
    }

    /// Handles the actual shattering part, throwing shards of whatever is defined on the component everywhere
    // tg: code/datums/elements/can_shatter.dm:56
    public virtual void shatter(atom_movable source = null, atom hit_atom = null)
    {
        turf scatter_turf = get_turf(hit_atom);
        if (!Dm.truthy(Waiting.NoSleep(hit_atom.CanPass(source, get_dir(source, hit_atom))))) //Object is too dense to fall apart on
            scatter_turf = get_turf(source);

        foreach (obj_item scattered_item in Dm.each<obj_item>(source.contents))
        {
            Waiting.NoSleep(scattered_item.forceMove(scatter_turf));
            (float x, float y) scatter_vector = circle_normal_rand(0, 48);
            scattered_item.pixel_x = scatter_vector.x;
            scattered_item.pixel_y = scatter_vector.y;
        }

        for (int iteration = 1; iteration <= Dm.num(number_of_shards); iteration++)
        {
            obj_item shard = Dm.@new(shard_type, scatter_turf) as obj_item;
            shard.pixel_x = Dm.rand(-6f, 6f);
            shard.pixel_y = Dm.rand(-6f, 6f);
        }
        playsound(scatter_turf, shattering_sound, 60, TRUE);
        if (isobj(source))
        {
            obj obj_source = (obj)source;
            Waiting.NoSleep(obj_source.deconstruct(FALSE));
            return;
        }
        else
            qdel(source);
    }

    // BYOND's generator(GEN_CIRCLE, low, high, NORMAL_RAND).Rand(), as OpenDream draws it (OpenDreamShared/Dream/Generator.cs): a uniform
    // angle in degrees passed to cos and sin as radians, and a radius from a normal of mean (low + high) / 2 and deviation (high - low) / 6,
    // clamped to [low, high].
    private static (float x, float y) circle_normal_rand(float low, float high)
    {
        Rng rng = World.Current.Rng;
        float theta = rng.NextFloat() * 360f;
        double gaussian = Math.Sqrt(-2.0 * Math.Log(1.0 - rng.NextFloat())) * Math.Cos(2.0 * Math.PI * rng.NextFloat());
        float r = (float)Math.Clamp((low + high) / 2.0 + gaussian * (high - low) / 6.0, low, high);
        return (MathF.Cos(theta) * r, MathF.Sin(theta) * r);
    }

    // tg: code/datums/elements/can_shatter.dm:79
    [SignalHandler]
    public virtual void on_post_attack_atom(obj_item source = null, atom attacked_atom = null, mob_living user = null)
    {
        shatter(source, attacked_atom);
    }
}
