// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/head/tophat.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CL1 (D333): the whole file (a magic wand tapped on the top hat pulls a bunny out of it, or bees, once every 30 seconds, with
// tg's emitter sound and a puff of smoke). The var blocks are generated (D111).

// tg: code/modules/clothing/head/tophat.dm:3
public partial class obj_item_clothing_head_hats_tophat : obj_item_clothing_head_hats
{
    // tg: code/modules/clothing/head/tophat.dm:1  #define RABBIT_CD_TIME (30 SECONDS) (file-local, rules 11.4)
    private const float RABBIT_CD_TIME = 30 * SECONDS;

    // tg: code/modules/clothing/head/tophat.dm:13
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!(tool is obj_item_gun_magic_wand))
            return await base.item_interaction(user, tool, modifiers, __extra);

        await abracadabra(tool, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/clothing/head/tophat.dm:20
    public virtual async DmTask abracadabra(obj_item hitby_wand = null, mob magician = null)
    {
        if (!(rabbit_cooldown <= world.time)) // !COOLDOWN_FINISHED(src, rabbit_cooldown)
        {
            to_chat(magician, span_warning(I18n.T("You can't find another rabbit in []! Seems another hasn't gotten lost in there yet...", this)));
            return;
        }

        rabbit_cooldown = world.time + RABBIT_CD_TIME; // COOLDOWN_START(src, rabbit_cooldown, RABBIT_CD_TIME)
        playsound(get_turf(this), Dm.resource("sound/items/weapons/emitter.ogg"), 70);
        await do_smoke(1, this, this, effect_type: typeof(obj_effect_particle_effect_fluid_smoke_quick));

        if (prob(10))
        {
            magician.visible_message(span_danger(I18n.T("[] taps [] with [], then reaches in and pulls out a bu- wait, those are bees!", magician, this, hitby_wand)), span_danger(I18n.T("You tap [] with your [] and pull out... <b>BEES!</b>", this, hitby_wand.name)));
            float wait_how_many_bees_did_that_guy_pull_out_of_his_hat = rand(4, 8);
            for (float b = 1; b <= wait_how_many_bees_did_that_guy_pull_out_of_his_hat; b++)
            {
                mob_living_basic_bee barry = Dm.@new<mob_living_basic_bee>(get_turf(magician));
                if (prob(20))
                    await barry.say(pick(I18n.T("BUZZ BUZZ"), I18n.T("PULLING A RABBIT OUT OF A HAT IS A TIRED TROPE"), I18n.T("I DIDN'T ASK TO BEE HERE")), forced: "bee hat");
            }
        }
        else
        {
            magician.visible_message(span_notice(I18n.T("[] taps [] with [], then reaches in and pulls out a bunny! Cute!", magician, this, hitby_wand)), span_notice(I18n.T("You tap [] with your [] and pull out a cute bunny!", this, hitby_wand.name)));
            mob_living_basic_rabbit bunbun = Dm.@new<mob_living_basic_rabbit>(get_turf(magician));
            await bunbun.mob_try_pickup(magician as mob_living, instant: TRUE);
        }
    }
}
