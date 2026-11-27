// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/crossbreeding/reproductive.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the reproductive extracts, fed three monkey cubes by hand or from a bio bag, which they digest into one to four
// plain extracts of their colour (the stomach is datums/storage/subtypes/others/extract_inventory.dm). The var blocks are
// generated (D111).

/*
Reproductive extracts:
	When fed three monkey cubes, produces between
	1 and 4 normal slime extracts of the same colour.
*/
// tg: code/modules/research/xenobiology/crossbreeding/reproductive.dm:9
public partial class obj_item_slimecross_reproductive : obj_item_slimecross
{
    // tg: code/modules/research/xenobiology/crossbreeding/reproductive.dm:19
    [DmParams()]
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_danger(I18n.T("It appears to have eaten [] Monkey Cube[]", Dm.length(contents), p_s())));
        return dot;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/reproductive.dm:23
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(create_storage(storage_type: typeof(datum_storage_extract_inventory)));
        return dot;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/reproductive.dm:27
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        datum_storage_extract_inventory slime_storage = atom_storage as datum_storage_extract_inventory;
        if (!(slime_storage is datum_storage_extract_inventory)) // what
            return NONE;

        if ((last_produce + cooldown) > world.time)
        {
            to_chat(user, span_warning(I18n.T("[] is still digesting!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.length(contents) >= feedAmount) //if for some reason the contents are full, but it didnt digest, attempt to digest again
        {
            to_chat(user, span_warning(I18n.T("[] appears to be full but is not digesting! Maybe poking it stimulated it to digest.", this)));
            if (slime_storage != null)
                await slime_storage.processCubes(user);
            return ITEM_INTERACT_BLOCKING;
        }

        if (tool is obj_item_storage_bag_xeno)
        {
            DmList inserted = Dm.list();
            await tool.atom_storage.remove_type(typeof(obj_item_food_monkeycube), this, feedAmount - Dm.length(contents), TRUE, FALSE, user, inserted);
            if (inserted.len == 0)
            {
                to_chat(user, span_warning(I18n.T("There are no monkey cubes in the bio bag!")));
                return ITEM_INTERACT_BLOCKING;
            }
            to_chat(user, span_notice(I18n.T("You feed [] monkey cube[] to [], and it pulses gently.", Dm.length(inserted), Dm.length(inserted) > 1 ? "s" : "", this)));
            playsound(this, "sound/items/eatfood.ogg", 20, TRUE);
            if (slime_storage != null)
                await slime_storage.processCubes(user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (!(tool is obj_item_food_monkeycube))
            return NONE;

        if (!Dm.truthy(atom_storage?.attempt_insert(tool, user, @override: TRUE, force: STORAGE_FULLY_LOCKED)))
        {
            to_chat(user, span_notice(I18n.T("The [] rejects []!", this, tool))); //in case it fails to insert for whatever reason you get feedback
            return ITEM_INTERACT_BLOCKING;
        }

        to_chat(user, span_notice(I18n.T("You feed [] to [], and it pulses gently.", tool, this)));
        if (slime_storage != null)
            await slime_storage.processCubes(user);
        playsound(this, "sound/items/eatfood.ogg", 20, TRUE);
        return ITEM_INTERACT_SUCCESS;
    }
}
