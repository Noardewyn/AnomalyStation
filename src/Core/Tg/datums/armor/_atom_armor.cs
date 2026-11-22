// Translated from tgstation (snapshot 1d99bd7f): code/datums/armor/_atom_armor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. M4a P2 (at the lead's assignment: the worn items' armour that run_armor_check reads): get_armor and
// get_armor_rating; M5 slice 1 (atom integrity): set_armor and set_armor_rating.

// tg: code/datums/armor/_atom_armor.dm:2
public partial class atom
{
    /// Get the atom's armor reference
    // tg: code/datums/armor/_atom_armor.dm:2
    public virtual datum_armor get_armor()
    {
        return (armor ??= get_armor_by_type(armor_type) as datum_armor);
    }

    /// Helper to get a specific rating for the atom's armor
    // tg: code/datums/armor/_atom_armor.dm:7
    public virtual object get_armor_rating(object damage_type = null)
    {
        datum_armor armor = get_armor();
        return armor.get_rating(damage_type);
    }

    /// Sets the armor of this atom to the specified armor
    // tg: code/datums/armor/_atom_armor.dm:12  (armor: datum/armor, which tg also fills with a typepath, :17)
    public virtual void set_armor(object armor = null)
    {
        if (this.armor == armor)
            return;
        if (!Dm.@in(this.armor?.GetType(), GLOB.armor_by_type))
            qdel(this.armor);
        this.armor = (ispath(armor) ? get_armor_by_type(armor) : armor) as datum_armor;
    }

    /// Helper to update the atom's armor to a new armor with the specified rating
    // tg: code/datums/armor/_atom_armor.dm:20
    public virtual void set_armor_rating(object damage_type = null, object rating = null)
    {
        datum_armor armor = get_armor();
        set_armor(armor.generate_new_with_specific(Dm.list(Dm.kv(Dm.text("[]", damage_type), rating))));
    }
}
