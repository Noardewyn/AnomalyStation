// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/chromosome.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327): the chromosomes the DNA console makes from used activators and applies to a mutation, and
// generate_chromosome. The var blocks are generated (D111).

// tg: code/game/objects/items/chromosome.dm:1
public partial class obj_item_chromosome : obj_item
{
    // tg: code/game/objects/items/chromosome.dm:22
    public virtual object can_apply(datum_mutation mutation = null)
    {
        if (mutation == null || QDELETED(mutation.owner) || !Dm.eq(mutation.can_chromosome, CHROMOSOME_NONE))
            return FALSE;
        if (stabilizer_coeff != null && !Dm.eq(mutation.stabilizer_coeff, MUTATION_COEFFICIENT_UNMODIFIABLE))
            return TRUE;
        if (synchronizer_coeff != null && !Dm.eq(mutation.synchronizer_coeff, MUTATION_COEFFICIENT_UNMODIFIABLE))
            return TRUE;
        if (power_coeff != null && !Dm.eq(mutation.power_coeff, MUTATION_COEFFICIENT_UNMODIFIABLE))
            return TRUE;
        if (energy_coeff != null && !Dm.eq(mutation.energy_coeff, MUTATION_COEFFICIENT_UNMODIFIABLE))
            return TRUE;
        return null;
    }

    // tg: code/game/objects/items/chromosome.dm:34
    public virtual async DmTask apply(datum_mutation mutation = null)
    {
        if (!Dm.eq(mutation.stabilizer_coeff, MUTATION_COEFFICIENT_UNMODIFIABLE) && Dm.truthy(stabilizer_coeff))
            mutation.stabilizer_coeff = Dm.num(stabilizer_coeff);
        if (!Dm.eq(mutation.synchronizer_coeff, MUTATION_COEFFICIENT_UNMODIFIABLE) && Dm.truthy(synchronizer_coeff))
            mutation.synchronizer_coeff = Dm.num(synchronizer_coeff);
        if (!Dm.eq(mutation.power_coeff, MUTATION_COEFFICIENT_UNMODIFIABLE) && Dm.truthy(power_coeff))
            mutation.power_coeff = Dm.num(power_coeff);
        if (!Dm.eq(mutation.energy_coeff, MUTATION_COEFFICIENT_UNMODIFIABLE) && Dm.truthy(energy_coeff))
            mutation.energy_coeff = Dm.num(energy_coeff);
        mutation.can_chromosome = CHROMOSOME_USED;
        mutation.chromosome_name = name;
        await mutation.setup();
        qdel(this);
    }
}

// tg: code/game/objects/items/chromosome.dm:49 (global procs; interleaved types, rules 1.9)
public static partial class GlobalProcs
{
    // tg: code/game/objects/items/chromosome.dm:50  var/static/list/chromosomes (a proc-level static, rules 10.6)
    private static ref DmList generate_chromosome__chromosomes => ref TgState.Current.GlobalProcs__generate_chromosome__chromosomes;

    // tg: code/game/objects/items/chromosome.dm:49
    public static object generate_chromosome()
    {
        if (generate_chromosome__chromosomes == null)
        {
            generate_chromosome__chromosomes = Dm.list();
            foreach (object A in Dm.each(subtypesof(typeof(obj_item_chromosome))))
            {
                if (!Dm.truthy(Dm.initial(A, (obj_item_chromosome CM) => CM.weight)))
                    break;
                generate_chromosome__chromosomes[A] = Dm.initial(A, (obj_item_chromosome CM) => CM.weight);
            }
        }
        return pick_weight(generate_chromosome__chromosomes);
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/chromosome.dm:50  var/static/list/chromosomes
    internal DmList GlobalProcs__generate_chromosome__chromosomes;
}
