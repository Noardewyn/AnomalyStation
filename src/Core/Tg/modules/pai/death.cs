// Translated from tgstation (snapshot 1d99bd7f): code/modules/pai/death.dm, login.dm and say.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI6 (D326): the whole file.

public partial class mob_living_silicon_pai : mob_living_silicon
{
    // tg: code/modules/pai/death.dm:1
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        if (Dm.eq(stat, DEAD))
            return null;
        await set_stat(DEAD);
        update_sight();
        clear_fullscreens();
        /**
        * New pAI's get a brand new mind to prevent meta stuff from their previous
        * life. This new mind causes problems down the line if it's not deleted here.
        */
        ghostize();

        if (!QDELETED(card) && loc != card)
        {
            await card.forceMove(drop_location() as atom);
            card.pai = null;
            card.screen_image = typeof(datum_pai_screen_image_off); // initial(card.screen_image)
            card.update_appearance();
        }

        qdel(this);
        return null;
    }

    // tg: code/modules/pai/login.dm:1
    public override async DmTask<object> Login()
    {
        object dot = await base.Login();
        if (!Dm.truthy(dot) || client == null)
            return FALSE;

        client.perspective = EYE_PERSPECTIVE;
        if (Dm.truthy(holoform))
            await client.set_eye(this);
        else
            await client.set_eye(card);
        return dot;
    }

    // tg: code/modules/pai/say.dm:1
    public override object binarycheck() => Dm.bitand((radio as obj_item_radio)?.special_channels ?? 0, RADIO_SPECIAL_BINARY);
}
