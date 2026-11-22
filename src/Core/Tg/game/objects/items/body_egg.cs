// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/body_egg.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: the body egg organ base (the changeling's headslug egg, the alien embryo's parent): it inserts
// itself into the carbon it is made in, marks its host as a xeno host and grows on the host's life and death ticks. Gaps X5 (D333),
// which wrote the same base at the same time, keeps this one: the alien embryo overrides its egg_process and its infection images.
// The var block is generated (D111).

// tg: code/game/objects/items/body_egg.dm:1
public partial class obj_item_organ_body_egg : obj_item_organ
{
    // tg: code/game/objects/items/body_egg.dm:10
    public override async DmTask on_find(mob_living finder = null)
    {
        await base.on_find(finder);
        to_chat(finder, span_warning(I18n.T("You found an unknown alien organism in []'s []!", owner, zone)));
    }

    // tg: code/game/objects/items/body_egg.dm:14
    public override object feel_for_damage(object self_aware = null)
    {
        // keep these stealthy for now, revisit later
        return "";
    }

    // tg: code/game/objects/items/body_egg.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (iscarbon(loc))
            Waiting.NoSleep(Insert(loc as mob_living_carbon));
        return dot;
    }

    // tg: code/game/objects/items/body_egg.dm:23
    public override async DmTask<object> on_mob_insert(mob_living_carbon egg_owner = null, float? special = null, object movement_flags = null)
    {
        special ??= FALSE;
        object dot = null;
        dot = await base.on_mob_insert(egg_owner, special, movement_flags);

        egg_owner.add_traits(Dm.list(TRAIT_XENO_HOST, TRAIT_XENO_IMMUNE), ORGAN_TRAIT);
        egg_owner.med_hud_set_status();
        Waiting.DetachSync(() => AddInfectionImages()); // INVOKE_ASYNC(src, PROC_REF(AddInfectionImages), egg_owner): the proc takes no argument
        return dot;
    }

    // tg: code/game/objects/items/body_egg.dm:30
    public override async DmTask on_mob_remove(object egg_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(egg_owner, special, movement_flags);
        ((mob_living_carbon)egg_owner).remove_traits(Dm.list(TRAIT_XENO_HOST, TRAIT_XENO_IMMUNE), ORGAN_TRAIT);
        ((mob_living_carbon)egg_owner).med_hud_set_status();
        Waiting.DetachSync(() => RemoveInfectionImages()); // INVOKE_ASYNC(src, PROC_REF(RemoveInfectionImages), egg_owner): the proc takes no argument
    }

    // tg: code/game/objects/items/body_egg.dm:36
    public override async DmTask on_death(object seconds_per_tick = null)
    {
        await base.on_death(seconds_per_tick);
        if (owner == null)
            return;
        await egg_process(seconds_per_tick);
    }

    // tg: code/game/objects/items/body_egg.dm:42
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);
        await egg_process(seconds_per_tick);
    }

    // tg: code/game/objects/items/body_egg.dm:46
    public virtual async DmTask egg_process(object seconds_per_tick = null)
    {
        return;
    }

    // tg: code/game/objects/items/body_egg.dm:49
    public virtual void RefreshInfectionImage()
    {
        RemoveInfectionImages();
        AddInfectionImages();
    }

    // tg: code/game/objects/items/body_egg.dm:53
    public virtual void AddInfectionImages()
    {
        return;
    }

    // tg: code/game/objects/items/body_egg.dm:56
    public virtual void RemoveInfectionImages()
    {
        return;
    }
}
