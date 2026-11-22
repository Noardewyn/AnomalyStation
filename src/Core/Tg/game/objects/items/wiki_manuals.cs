// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/wiki_manuals.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps OD1 (D333): the Space Law's and the ordnance manual's suicides. The wiki manual's page (display_content, a browser window) is a
// live-partial leaf (docs/ai/specs/live-partial.txt); the rest of the file is var blocks only, generated (D111).

// tg: code/game/objects/items/wiki_manuals.dm:84
public partial class obj_item_book_manual_wiki_security_space_law : obj_item_book_manual_wiki
{
    // tg: code/game/objects/items/wiki_manuals.dm:91
    public override DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] pretends to read \\the [] intently... then promptly dies of laughter!", user, this)));
        return DmTask<object>.FromResult(OXYLOSS);
    }
}

// tg: code/game/objects/items/wiki_manuals.dm:195
public partial class obj_item_book_manual_wiki_ordnance : obj_item_book_manual_wiki
{
    // tg: code/game/objects/items/wiki_manuals.dm:201
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        mob_living_carbon_human H = user as mob_living_carbon_human;
        user.visible_message(span_suicide(I18n.T("[] starts dancing to the Rhumba Beat! It looks like [] trying to commit suicide!", user, user.p_theyre())));
        playsound(loc, Dm.resource("sound/effects/spray.ogg"), 10, TRUE, -3);
        if (QDELETED(H))
            return null;
        await H.emote("spin");
        await sleep(2 * SECONDS);
        foreach (obj_item W in Dm.each<obj_item>(H.contents))
        {
            await H.dropItemToGround(W);
            if (prob(50))
                await step(W, Dm.num(pick(GLOB.alldirs)));
        }
        obj_item_bodypart head = H.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart;
        if (head != null)
            ADD_TRAIT(head, TRAIT_DISFIGURED, TRAIT_GENERIC);
        foreach (obj_item_bodypart part in Dm.each_cast<obj_item_bodypart>(H.get_bodyparts()))
            part.adjustBleedStacks(5);
        H.gib_animation();
        await sleep(0.3f * SECONDS);
        await H.adjust_brute_loss(1000); //to make the body super-bloody
        // if we use gib() then the body gets deleted
        await H.spawn_gibs();
        await H.spill_organs(DROP_ALL_REMAINS);
        await H.spread_bodyparts(DROP_BRAIN);
        return BRUTELOSS;
    }
}
