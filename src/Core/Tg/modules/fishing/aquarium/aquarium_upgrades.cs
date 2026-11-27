// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/aquarium/aquarium_upgrades.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the aquarium upgrade kits, which turn a basic aquarium into a new one carrying its fish, feed and settings over
// (the aquarium component's transfer), the bioelectricity generator (which magnifies the electrogenesis of its fish into tesla zaps
// and takes only explosive shocks), and the bluespace fish tank. The var blocks are generated (D111).

/// Aquarium upgrades, can be applied to a basic aquarium to upgrade it into an advanced subtype.
// tg: code/modules/fishing/aquarium/aquarium_upgrades.dm:2
public partial class obj_item_aquarium_upgrade : obj_item
{
    // tg: code/modules/fishing/aquarium/aquarium_upgrades.dm:14
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!HAS_TRAIT(interacting_with, TRAIT_IS_AQUARIUM))
            return NONE;
        if (upgrade_from_type != interacting_with.type)
        {
            interacting_with.balloon_alert(user, I18n.T("wrong kind of aquarium!"));
            return ITEM_INTERACT_BLOCKING;
        }
        interacting_with.balloon_alert(user, I18n.T("upgrading..."));
        // PERFORM_ALL_TESTS(aquarium_upgrade) is FALSE in production (rules 1.7)
        if (!Dm.truthy(await do_after(user, 5 * SECONDS, interacting_with)))
            return ITEM_INTERACT_BLOCKING;
        atom_movable upgraded_aquarium = await Dm.new_async(upgrade_to_type, ((atom_movable)interacting_with).drop_location()) as atom_movable;
        //This should transfer all the fish, reagents and settings from the aquarium component
        await interacting_with.TransferComponents(upgraded_aquarium);
        upgraded_aquarium.balloon_alert(user, I18n.T("upgraded"));
        qdel(this);
        qdel(interacting_with);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/fishing/aquarium/aquarium_upgrades.dm:39
public partial class obj_structure_aquarium_bioelec_gen : obj_structure_aquarium
{
    // tg: code/modules/fishing/aquarium/aquarium_upgrades.dm:48
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_BIOELECTRIC_GENERATOR, INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/fishing/aquarium/aquarium_upgrades.dm:52
    public override async DmTask<object> zap_act(object power = null, object zap_flags = null)
    {
        object explosive = Dm.bitand(zap_flags, ZAP_MACHINE_EXPLOSIVE);
        if (!Dm.truthy(explosive))
            return null; //immune to all other shocks to make sure power can be generated without breaking the generator itself
        return await base.zap_act(power, zap_flags);
    }

    // tg: code/modules/fishing/aquarium/aquarium_upgrades.dm:58
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_boldwarning(I18n.T("WARNING! WARNING! WARNING!")));
        Dm.append(ref dot, span_warning(I18n.T("The bioelectric potential of the fish inside is magnified to dangerous levels by the generator.")));
        Dm.append(ref dot, span_notice(I18n.T("Tesla coils are required to collect this magnified energy... and you'll want a grounding rod to protect yourself as well.")));
        return dot;
    }
}
