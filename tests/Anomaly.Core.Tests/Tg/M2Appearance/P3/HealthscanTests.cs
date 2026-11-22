using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.M2Appearance.P3.AppearanceFixtures;

namespace Anomaly.Core.Tests.Tg.M2Appearance.P3;

/// <summary>
/// The scanned body for healthscan (fixture, rules 15): the P1 and P2 procs the scan reads are skeletons in P3's branch, so
/// this subtype answers them as tg's do for a breathing human at 37 °C with full blood and no organs listed.
/// </summary>
[DmPath("/mob/living/carbon/human/test_p3_body/scanned")]
public class mob_living_carbon_human_test_p3_body_scanned : mob_living_carbon_human_test_p3_body
{
    public float test_oxy;
    public float test_tox;
    public override object get_oxy_loss() => test_oxy;
    public override object get_tox_loss() => test_tox;
    public override object get_stamina_loss() => 0;
    public override object get_organ_slot(object slot = null) => null;
    public override object get_missing_limbs() => Dm.list();
    public override object has_embedded_objects(float? include_harmless = null) => FALSE;
    public override object get_body_temp_heat_damage_limit() => BODYTEMP_HEAT_DAMAGE_LIMIT;
    public override object get_body_temp_cold_damage_limit() => BODYTEMP_COLD_DAMAGE_LIMIT;
    public override object get_blood_alcohol_content() => 0;
    public override object has_dna() => dna;
}

/// <summary>The dev analyzer (scope 2i): tg's healthscan with tochat = TRUE to the requester only (D40).</summary>
public class HealthscanTests
{
    [Fact]
    public void TheScanGoesToTheRequestersChatOnlyWithTgsDamageReport()
    {
        TgToyRoom room = TgToyRoom.Boot(seed: 71);
        room.In(() =>
        {
            DmWorldState.Current.NotTranslatedThrows = true;
            var chats = new ChatCapture();
            PresentationSinks.Of(room.World).Messages = chats;
            var target = Dm.@new<mob_living_carbon_human_test_p3_body_scanned>(TgToyRoom.T(4, 4));
            foreach (obj_item_bodypart part in new obj_item_bodypart[] { Dm.@new<obj_item_bodypart_chest_test_p3>(), Dm.@new<obj_item_bodypart_head_test_p3>() })
            {
                part.owner = target;
                Dm.append(ref target.test_parts, part);
            }
            target.dna = Dm.@new<datum_dna>();
            target.dna.species = Dm.@new<datum_species_human>();
            target.health = 55;
            target.test_brute = 30.2f;
            target.test_oxy = 15;
            Part(target, BODY_ZONE_CHEST).brute_dam = 30.2f;
            var doctor = Dm.@new<mob_living_carbon_human_test_p3_body>(TgToyRoom.T(5, 4));

            string html = (string)healthscan(doctor, target, SCANNER_VERBOSE, SCANPOWER_BASIC, TRUE);
            Assert.Contains("<b>55% healthy</b>", html);                         // round(55/100, 0.01) * 100
            Assert.Contains("Subject lacks a brain.", html);                      // no brain organ in this body
            Assert.Contains("<b>31</b>", html);                                   // ceil(30.2) brute overall
            Assert.Contains("<td><font color='#cc3333'>31</font></td>", html);   // the chest's row
            Assert.Contains("<td><font color='#33ccff'>15</font></td></tr></table>", html); // oxygen on the chest row
            Assert.DoesNotContain("Head:", html);                                 // an undamaged head has no row
            Assert.Contains("Species: \uE001Human", html);               // tg's name is "\improper Human": the marker stays in the text, as in BYOND
            Assert.Contains(Dm.text("Analyzing results for <b>[]</b>", target.real_name), html); // the fixture human's random name (randomize_human_normie, syndicate G1c)
            Assert.Contains("Body temperature: 37 &deg;C (98.6 &deg;F)", html);
            // to_chat(user, custom_boxed_message("blue_box", .)): one chat message, to the doctor only, the scan as its html
            (object chatTarget, DmList message) = Assert.Single(chats.Messages);
            Assert.Same(doctor, chatTarget);
            Assert.Equal(MESSAGE_TYPE_INFO, message["type"]);
            Assert.Equal("<div class='boxed_message blue_box'>" + html + "</div>", message["html"]);
            DmWorldState.Current.NotTranslatedThrows = false;
        });
        Assert.Empty(room.RuntimeText);
    }

    /// <summary>The chat seam's messages as tg's SSchat would get them (target and the message list with its html).</summary>
    private sealed class ChatCapture : IMessageSink
    {
        public List<(object Target, DmList Message)> Messages { get; } = new();
        public void Chat(object target, DmList message) => Messages.Add((target, message));
        public void Output(object target, string text) { }
        public void Balloon(object viewer, object atom, string text) { }
    }
}
