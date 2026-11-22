// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/plant_backfire.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the whole file (the plant genes' backfire).

/// -- Plant backfire element --
/// Certain high-danger plants, like death-nettles, will backfire and harm the holder if they're not properly protected.
/// If a user is protected with something like leather gloves, they can handle them normally.
/// If they're not protected properly, we invoke a callback on the user, harming or inconveniencing them.
// tg: code/datums/elements/plant_backfire.dm:5
public partial class datum_element_plant_backfire : datum_element
{
    // tg: code/datums/elements/plant_backfire.dm:15
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object cancel_action = Dm.arg(__extra, 0) ?? FALSE;
        object extra_traits = Dm.arg(__extra, 1);
        object extra_genes = Dm.arg(__extra, 2);
        object dot = await base.Attach(target, __extra);
        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;

        this.cancel_action = Dm.num(cancel_action);
        this.extra_traits = extra_traits;
        this.extra_genes = extra_genes;

        RegisterSignal(target, COMSIG_ITEM_PRE_ATTACK, nameof(attack_safety_check));
        RegisterSignal(target, COMSIG_ITEM_PICKUP, nameof(pickup_safety_check));
        RegisterSignal(target, COMSIG_MOVABLE_PRE_THROW, nameof(throw_safety_check));
        return dot;
    }

    // tg: code/datums/elements/plant_backfire.dm:28
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, Dm.list(COMSIG_ITEM_PRE_ATTACK, COMSIG_ITEM_PICKUP, COMSIG_MOVABLE_PRE_THROW));
        return dot;
    }

    /**
     * Checks before we attack if we're okay to continue.
     *
     * source - our plant
     * user - the mob wielding our [source]
     */
    // tg: code/datums/elements/plant_backfire.dm:38
    [SignalHandler]
    public virtual object attack_safety_check(obj_item source = null, atom target = null, mob user = null)
    {
        // Covers stuff like tk, since we aren't actually touching the plant.
        if (!Dm.truthy(user.is_holding(source)))
            return null;
        if (!Dm.truthy(backfire(source, user)))
            return null;

        return Dm.truthy(cancel_action) ? COMPONENT_CANCEL_ATTACK_CHAIN : NONE;
    }

    /**
     * Checks before we pick up the plant if we're okay to continue.
     *
     * source - our plant
     * user - the mob picking our [source]
     */
    // tg: code/datums/elements/plant_backfire.dm:55
    [SignalHandler]
    public virtual void pickup_safety_check(obj_item source = null, mob user = null)
    {
        backfire(source, user);
    }

    /**
     * Checks before we throw the plant if we're okay to continue.
     *
     * source - our plant
     * thrower - the mob throwing our [source]
     */
    // tg: code/datums/elements/plant_backfire.dm:66
    [SignalHandler]
    public virtual object throw_safety_check(obj_item source = null, DmList arguments = null)
    {
        mob_living thrower = arguments?[4] as mob_living; // the 4th arg = the mob throwing our item
        if (thrower == null || !Dm.truthy(thrower.is_holding(source)))
            return null;
        if (!Dm.truthy(backfire(source, thrower)))
            return null;

        return Dm.truthy(cancel_action) ? COMPONENT_CANCEL_ATTACK_CHAIN : NONE;
    }

    /**
     * The actual backfire occurs here.
     * Checks if the user is able to safely handle the plant.
     * If not, sends the backfire signal (meaning backfire will occur and be handled by one or multiple genes).
     *
     * Returns FALSE if the user was safe and no backfire occured.
     * Returns TRUE if the user was not safe and a backfire actually happened.
     */
    // tg: code/datums/elements/plant_backfire.dm:85
    public virtual object backfire(obj_item plant = null, mob user = null)
    {
        if (Dm.truthy(plant_safety_check(plant, user as mob_living_carbon)))
            return FALSE;

        if (Dm.truthy(plant._listen_lookup?[COMSIG_PLANT_ON_BACKFIRE]))
            plant._SendSignal(COMSIG_PLANT_ON_BACKFIRE, Dm.list(plant, user));
        return TRUE;
    }

    /**
     * Actually checks if our user is safely handling our plant.
     *
     * Checks for TRAIT_PLANT_SAFE, and returns TRUE if we have it.
     * Then, any extra traits we need to check (Like TRAIT_PIERCEIMMUNE for nettles) and returns TRUE if we have one of them.
     * Then, any extra genes we need to check (Like liquid contents for bluespace tomatos) and returns TRUE if we don't have the gene.
     *
     * source - our plant
     * user - the carbon handling our [source]
     *
     * returns FALSE if none of the checks are successful.
     */
    // tg: code/datums/elements/plant_backfire.dm:104
    public virtual object plant_safety_check(obj_item plant = null, mob_living_carbon user = null)
    {
        if (user == null)
            return TRUE;

        if (HAS_TRAIT(user, TRAIT_PLANT_SAFE))
            return TRUE;

        foreach (object checked_trait in Dm.each(extra_traits))
        {
            if (HAS_TRAIT(user, checked_trait))
                return TRUE;
        }

        obj_item_seeds our_seed = plant.get_plant_seed() as obj_item_seeds;
        if (our_seed != null)
        {
            foreach (object checked_gene in Dm.each(extra_genes))
            {
                if (our_seed.get_gene(checked_gene) == null)
                    return TRUE;
            }
        }

        foreach (obj_item_clothing worn_item in Dm.each<obj_item_clothing>(user.get_equipped_items()))
        {
            if (Dm.truthy(Dm.bitand(worn_item.body_parts_covered, HANDS)) && Dm.truthy(Dm.bitand(worn_item.clothing_flags, THICKMATERIAL)))
                return TRUE;
        }

        return FALSE;
    }
}
