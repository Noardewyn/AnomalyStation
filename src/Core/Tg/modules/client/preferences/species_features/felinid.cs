// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/species_features/felinid.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP1 (D338): the whole file: the felinid's tail and ears. Both types are var-only (species_feature's procs, _preference.cs): declared
// here so that they are written types (DV180: GLOB.preference_entries holds the translated preferences).

// tg: code/modules/client/preferences/species_features/felinid.dm:1
public partial class datum_preference_choiced_species_feature_tail_felinid : datum_preference_choiced_species_feature { }

// tg: code/modules/client/preferences/species_features/felinid.dm:8
public partial class datum_preference_choiced_species_feature_felinid_ears : datum_preference_choiced_species_feature { }
