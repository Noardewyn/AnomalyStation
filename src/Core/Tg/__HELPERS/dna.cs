// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/dna.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327): the genetics helper macros. The file is outside code/__DEFINES, so the defines converter does not
// emit them: they are public members of GlobalProcs (rules 11.4). GET_UF_BLOCK_LEN (:15) reads GLOB.features_block_lengths, which
// the snapshot no longer declares; nothing uses it.

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/dna.dm:5  #define GET_INITIALIZED_MUTATION(A) GLOB.all_mutations[A]
    public static object GET_INITIALIZED_MUTATION(object A) => GLOB.all_mutations[A];

    // tg: code/__HELPERS/dna.dm:6  #define GET_GENE_STRING(A, B) (B.mutation_index[A])
    public static object GET_GENE_STRING(object A, datum_dna B) => B.mutation_index[A];

    // tg: code/__HELPERS/dna.dm:7  #define GET_SEQUENCE(A) (GLOB.full_sequences[A])
    public static object GET_SEQUENCE(object A) => GLOB.full_sequences[A];

    // tg: code/__HELPERS/dna.dm:8  #define GET_MUTATION_TYPE_FROM_ALIAS(A) (GLOB.alias_mutations[A])
    public static object GET_MUTATION_TYPE_FROM_ALIAS(object A) => GLOB.alias_mutations[A];

    // tg: code/__HELPERS/dna.dm:10  #define GET_MUTATION_STABILIZER(A) ((A.stabilizer_coeff < 0) ? 1 : A.stabilizer_coeff)
    public static float GET_MUTATION_STABILIZER(datum_mutation A) => Dm.num(A.stabilizer_coeff) < 0 ? 1 : Dm.num(A.stabilizer_coeff);

    // tg: code/__HELPERS/dna.dm:11  #define GET_MUTATION_SYNCHRONIZER(A) ((A.synchronizer_coeff < 0) ? 1 : A.synchronizer_coeff)
    public static float GET_MUTATION_SYNCHRONIZER(datum_mutation A) => Dm.num(A.synchronizer_coeff) < 0 ? 1 : Dm.num(A.synchronizer_coeff);

    // tg: code/__HELPERS/dna.dm:12  #define GET_MUTATION_POWER(A) ((A.power_coeff < 0) ? 1 : A.power_coeff)
    public static float GET_MUTATION_POWER(datum_mutation A) => Dm.num(A.power_coeff) < 0 ? 1 : Dm.num(A.power_coeff);

    // tg: code/__HELPERS/dna.dm:13  #define GET_MUTATION_ENERGY(A) ((A.energy_coeff < 0) ? 1 : A.energy_coeff)
    public static float GET_MUTATION_ENERGY(datum_mutation A) => Dm.num(A.energy_coeff) < 0 ? 1 : Dm.num(A.energy_coeff);
}
