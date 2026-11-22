// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/blood_walk.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

///Blood walk, a component that causes you to make blood wherever you walk.
// tg: code/datums/components/blood_walk.dm:2
public partial class datum_component_blood_walk : datum_component
{
    // tg: code/datums/components/blood_walk.dm:24
    [DmParams("blood_type", "sound_played", "sound_volume", "blood_spawn_chance", "target_dir_change", "transfer_blood_dna", "max_blood", "blood_dna_info")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!ismovable(parent))
            return COMPONENT_INCOMPATIBLE;

        this.blood_type = Dm.arg<Type>(__extra, 0) ?? typeof(obj_effect_decal_cleanable_blood);
        this.sound_played = Dm.arg(__extra, 1);
        this.sound_volume = Dm.arg(__extra, 2) ?? 80;
        this.blood_spawn_chance = Dm.arg(__extra, 3) ?? 100;
        this.target_dir_change = Dm.arg(__extra, 4) ?? FALSE;
        this.transfer_blood_dna = Dm.arg(__extra, 5) ?? FALSE;
        object max_blood = Dm.arg(__extra, 6) ?? INFINITY;
        DmList blood_dna_info = Dm.arg<DmList>(__extra, 7) ?? Dm.list(Dm.kv("meaty DNA", get_blood_type(typeof(datum_blood_type_meat))));
        this.blood_dna_info = blood_dna_info.Copy();

        blood_remaining = Dm.num(max_blood);
        return null;
    }

    // tg: code/datums/components/blood_walk.dm:49
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(spread_blood));
    }

    // tg: code/datums/components/blood_walk.dm:52
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_MOVABLE_MOVED);
    }

    // tg: code/datums/components/blood_walk.dm:55
    [DmParams("new_comp", "i_am_original", "blood_type", "sound_played", "sound_volume", "blood_spawn_chance", "target_dir_change", "transfer_blood_dna", "max_blood")]
    public override async DmTask InheritComponent(datum_component new_comp = null, object i_am_original = null, params object[] __extra)
    {
        object max_blood = Dm.arg(__extra, 6) ?? INFINITY;
        if (!Dm.truthy(i_am_original))
            return;

        if (Dm.num(max_blood) >= INFINITY || blood_remaining >= INFINITY)
            return;

        // Applying a new version of the blood walk component will add the new version's step count to our's.
        // We will completely disregard any other arguments passed, because we already have arguments set.
        blood_remaining += Dm.num(max_blood);
    }

    ///Spawns blood (if possible) under the source, and plays a sound effect (if any)
    // tg: code/datums/components/blood_walk.dm:78
    [SignalHandler]
    public virtual void spread_blood(atom_movable source = null)
    {
        if (source.loc is not turf current_turf || isclosedturf(current_turf) || Dm.truthy(isgroundlessturf(current_turf)))
            return;

        if (!prob(Dm.num(blood_spawn_chance)))
            return;

        DmList blood_DNA = blood_dna_info.Copy();
        if (Dm.truthy(transfer_blood_dna) && Dm.truthy(GET_ATOM_BLOOD_DNA_LENGTH(source)))
            blood_DNA = Dm.bitor(GET_ATOM_BLOOD_DNA(source), blood_DNA) as DmList;

        if (!Dm.truthy(has_blood_flag(blood_DNA, BLOOD_COVER_TURFS)))
        {
            if (Dm.truthy(has_blood_flag(blood_DNA, BLOOD_ADD_DNA)))
                Waiting.NoSleep(current_turf.add_blood_DNA(blood_DNA));
            return;
        }

        obj_effect_decal_cleanable_blood blood = Dm.@new(blood_type, current_turf, null, blood_DNA) as obj_effect_decal_cleanable_blood;

        if (QDELETED(blood)) // Our blood was placed on somewhere it shouldn't be and qdeleted in init.
            return;

        if (Dm.truthy(target_dir_change))
            Waiting.NoSleep(blood.setDir(source.dir));
        if (sound_played != null)
            playsound(source, sound_played, sound_volume, TRUE, 2, TRUE);

        blood_remaining = Math.Max(blood_remaining - 1, 0);
        if (blood_remaining <= 0)
            qdel(this);
    }
}
