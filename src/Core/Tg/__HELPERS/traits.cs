// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/traits.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

public static partial class GlobalProcs
{
    // TRAIT_CALLBACK_ADD and TRAIT_CALLBACK_REMOVE are CALLBACK(GLOBAL_PROC, GLOBAL_PROC_REF(x), ##target, ##trait,
    // ##source): the ## before each argument only joins it to the preceding comma (nothing is pasted into an
    // identifier), so they are GlobalProcs members that create the callback (rules 7.7, 11.1).
    // tg: code/__HELPERS/traits.dm:1
    public static datum_callback TRAIT_CALLBACK_ADD(object target, object trait, object source) => Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.___TraitAdd), target, trait, source);
    // tg: code/__HELPERS/traits.dm:2
    public static datum_callback TRAIT_CALLBACK_REMOVE(object target, object trait, object source) => Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.___TraitRemove), target, trait, source);

    ///DO NOT USE ___TraitAdd OR ___TraitRemove as a replacement for ADD_TRAIT / REMOVE_TRAIT defines. To be used explicitly for callback.
    // tg: code/__HELPERS/traits.dm:5
    public static void ___TraitAdd(object target = null, object trait = null, object source = null)
    {
        if (!Dm.truthy(target) || !Dm.truthy(trait) || !Dm.truthy(source))
            return;

        if (islist(target))
        {
            foreach (datum listed_target in Dm.each<datum>(target))
                ADD_TRAIT(listed_target, trait, source);
            return;
        }

        // ASSERT, inline at its use (__DEFINES/assert.dm:4; rules 11.1): the message is built only on failure.
        if (!(isdatum(target)))
            throw Dm.CRASH(assertion_message("code/__HELPERS/traits.dm", 14, "isdatum(target)", Dm.text("Invalid target used in TRAIT_CALLBACK_ADD! Expected a datum reference, got [] instead.", target)), "code/__HELPERS/traits.dm", 14);

        datum datum_target = target as datum;
        ADD_TRAIT(datum_target, trait, source);
    }

    ///DO NOT USE ___TraitAdd OR ___TraitRemove as a replacement for ADD_TRAIT / REMOVE_TRAIT defines. To be used explicitly for callback.
    // tg: code/__HELPERS/traits.dm:20
    public static void ___TraitRemove(object target = null, object trait = null, object source = null)
    {
        if (!Dm.truthy(target) || !Dm.truthy(trait) || !Dm.truthy(source))
            return;

        if (islist(target))
        {
            foreach (datum listed_target in Dm.each<datum>(target))
                REMOVE_TRAIT(listed_target, trait, source);
            return;
        }

        // ASSERT, inline at its use (__DEFINES/assert.dm:4; rules 11.1): the message is built only on failure.
        if (!(isdatum(target)))
            throw Dm.CRASH(assertion_message("code/__HELPERS/traits.dm", 29, "isdatum(target)", Dm.text("Invalid target used in TRAIT_CALLBACK_REMOVE! Expected a datum reference, got [] instead.", target)), "code/__HELPERS/traits.dm", 29);

        datum datum_target = target as datum;
        REMOVE_TRAIT(datum_target, trait, source);
    }
}

public partial class datum
{
    /// Proc that handles adding multiple traits to a target via a list. Must have a common source and target.
    // tg: code/__HELPERS/traits.dm:36
    public virtual void add_traits(DmList list_of_traits = null, object source = null)
    {
        // ASSERT, inline at its use (__DEFINES/assert.dm:4; rules 11.1): the message is built only on failure.
        if (!(islist(list_of_traits)))
            throw Dm.CRASH(assertion_message("code/__HELPERS/traits.dm", 37, "islist(list_of_traits)", Dm.text("Invalid arguments passed to add_traits! Invoked on [] with [], source being [].", this, list_of_traits, source)), "code/__HELPERS/traits.dm", 37);
        foreach (object trait in Dm.each(list_of_traits))
            ADD_TRAIT(this, trait, source);
    }

    /// Proc that handles removing multiple traits from a target via a list. Must have a common source and target.
    // tg: code/__HELPERS/traits.dm:42
    public virtual void remove_traits(DmList list_of_traits = null, object source = null)
    {
        // ASSERT, inline at its use (__DEFINES/assert.dm:4; rules 11.1): the message is built only on failure.
        if (!(islist(list_of_traits)))
            throw Dm.CRASH(assertion_message("code/__HELPERS/traits.dm", 43, "islist(list_of_traits)", Dm.text("Invalid arguments passed to remove_traits! Invoked on [] with [], source being [].", this, list_of_traits, source)), "code/__HELPERS/traits.dm", 43);
        foreach (object trait in Dm.each(list_of_traits))
            REMOVE_TRAIT(this, trait, source);
    }
}
