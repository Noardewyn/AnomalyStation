using Anomaly.Core.DmShim;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tests.M9;

/// <summary>
/// M9 slice D2: the radio's and the wall intercom's screentips (radio.dm:133, intercom.dm:73): a screwdriver or a key names the
/// click's action, a wrench on an unscrewed intercom names its detaching.
/// </summary>
public class RadioScreentipTests
{
    [Fact]
    public void A_radio_and_an_intercom_name_their_tool_actions()
    {
        var r = new AudioRoom(5, 5, seed: 3);
        r.In(() =>
        {
            mob_living_carbon_human user = Dm.@new<mob_living_carbon_human>(locate(2, 2, 1));
            var radio = Dm.@new<obj_item_radio>(locate(3, 2, 1));
            var intercom = Dm.@new<obj_item_radio_intercom>(locate(3, 3, 1));
            var screwdriver = Dm.@new<obj_item_screwdriver>(locate(2, 2, 1));
            var wrench = Dm.@new<obj_item_wrench>(locate(2, 2, 1));
            var key = Dm.@new<obj_item_encryptionkey>(locate(2, 2, 1));

            DmList context = Dm.list();
            Assert.Equal(CONTEXTUAL_SCREENTIP_SET, Dm.num(radio.add_context(radio, context, screwdriver, user)));
            Assert.Equal(("Remove encryption key", "Unscrew"), (Dm.str(context[SCREENTIP_CONTEXT_LMB]), Dm.str(context[SCREENTIP_CONTEXT_RMB])));
            context = Dm.list();
            Assert.Equal(CONTEXTUAL_SCREENTIP_SET, Dm.num(radio.add_context(radio, context, key, user)));
            Assert.Equal("Install encryption key", Dm.str(context[SCREENTIP_CONTEXT_LMB]));

            context = Dm.list();
            Assert.Equal(CONTEXTUAL_SCREENTIP_SET, Dm.num(intercom.add_context(intercom, context, screwdriver, user)));
            Assert.Equal(("Unscrew from wall", "Unscrew from wall"), (Dm.str(context[SCREENTIP_CONTEXT_LMB]), Dm.str(context[SCREENTIP_CONTEXT_RMB]))); // no key: the same action
            context = Dm.list();
            Assert.NotEqual(CONTEXTUAL_SCREENTIP_SET, Dm.num(intercom.add_context(intercom, context, wrench, user))); // still screwed to the wall
            intercom.unscrewed = 1;
            context = Dm.list();
            Assert.Equal(CONTEXTUAL_SCREENTIP_SET, Dm.num(intercom.add_context(intercom, context, wrench, user)));
            Assert.Equal("Detach from wall", Dm.str(context[SCREENTIP_CONTEXT_RMB]));
        });
        r.AssertNoRuntimes();
    }
}
