using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using System.Text.RegularExpressions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M6;

/// <summary>
/// M6 slice 6, gauze (stacks/medical.dm's medical stack, wrap and gauze, datums/components/limb_applicable.dm, the bodypart's
/// apply_item and Exited) in a running world: a medic's click wraps one gauze off the stack around a cut arm, with tg's sounds,
/// its bandage overlay on the body and the gauze soaking up the cut's flow; a second gauze is refused while the first is clean;
/// an unwounded limb takes longer to wrap; a soaked gauze falls away in rags; the self-check's link takes the gauze off into the
/// hand; wirecutters cut gauze into cloth; the right click's heal loop dresses a burn.
/// </summary>
public class GauzeTests
{
    private const int Second = 20;

    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(12, 9, seed: seed);
        // tg's strings/ files (the scars' texts), as the server mounts them
        if (TgClone.Root != null)
            TgStrings.Mount(DmWorldState.Of(r.World).Files, TgClone.Root);
        r.In(() =>
        {
            for (int x = 2; x <= 11; x++)
            {
                for (int y = 2; y <= 8; y++)
                    Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_floor_iron), Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating))));
            }
        });
        r.Step(Second);
        return r;
    }

    private static obj_item_bodypart Part(mob_living_carbon_human h, string zone) => (obj_item_bodypart)h.get_bodypart(zone);

    private static datum_wound Wound(AudioRoom r, mob_living_carbon_human h, string type, string zone, float severity) =>
        r.In(() => (datum_wound)Waiting.NoSleep(h.cause_wound_of_type_and_severity(type, Part(h, zone), severity)));

    private static obj_item_stack_medical_wrap_gauze GauzeOf(obj_item_bodypart limb) =>
        LAZYACCESS(limb.applied_items, LIMB_ITEM_GAUZE) as obj_item_stack_medical_wrap_gauze;

    private static obj_item_stack_medical_wrap_gauze Hold(AudioRoom r, mob_living_carbon_human medic, string zone)
        => r.In(() =>
        {
            var g = Dm.@new<obj_item_stack_medical_wrap_gauze>(medic.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(medic.put_in_active_hand(g))));
            medic.zone_selected = zone;
            return g;
        });

    private static List<string> BodyIcons(mob_living_carbon_human h)
    {
        var found = new List<string>();
        if (h.overlays_standing[DAMAGE_LAYER] is not mutable_appearance damage)
            return found;
        foreach (object part in Dm.each(damage.overlays))
            if (part is mutable_appearance ma)
                found.Add(Dm.str(ma.icon) + ":" + Dm.str(ma.icon_state));
        return found;
    }

    [Fact]
    public void TheMedicWrapsACutArmWithOneGauzeWithTgsSoundsAndOverlayAndTheGauzeSoaksUpTheFlow()
    {
        var r = Room(6901);
        mob_living_carbon_human medic = r.Join(5, 5, "Medic");
        mob_living_carbon_human victim = r.Dummy(6, 5);
        var cut = (datum_wound_slash_flesh)Wound(r, victim, WOUND_SLASH, BODY_ZONE_L_ARM, WOUND_SEVERITY_MODERATE);
        obj_item_stack_medical_wrap_gauze stack = Hold(r, medic, BODY_ZONE_L_ARM);
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        r.Sounds.Heard.Clear();

        r.Command(() => ClientSeam.Click(medic.client, victim, "left=1;button=left"));
        r.Step(Second); // other_delay 2 s with a wound that takes gauze
        r.In(() => Assert.Null(GauzeOf(Part(victim, BODY_ZONE_L_ARM))));
        Assert.Contains(chat.Html, h => h.Contains("You begin wrapping the wounds on", StringComparison.Ordinal));
        r.Step(Second + 2);
        float flowAfterWrap = r.In(() =>
        {
            obj_item_stack_medical_wrap_gauze wrapped = GauzeOf(Part(victim, BODY_ZONE_L_ARM));
            Assert.NotNull(wrapped);
            Assert.NotSame(stack, wrapped); // split_stack(1): one gauze off the stack
            Assert.Equal(1f, Dm.num(wrapped.amount));
            Assert.Equal(5f, Dm.num(stack.amount));
            Assert.Same(Part(victim, BODY_ZONE_L_ARM), wrapped.loc);
            Assert.Matches("^l_arm[123]$", Dm.str(wrapped.worn_icon_state)); // update_wounds on COMSIG_ITEM_APPLIED_TO_LIMB
            Assert.Contains(BodyIcons(victim), s => s.StartsWith("icons/mob/human/bandage.dmi:l_arm", StringComparison.Ordinal));
            Assert.Equal(0.7f, Dm.num(Part(victim, BODY_ZONE_L_ARM).get_splint_factor()), 3);
            return Dm.num(cut.blood_flow);
        });
        Assert.Contains(chat.Html, h => h.Contains("You bandage the wounds on", StringComparison.Ordinal));
        Assert.Contains(r.Sounds.Heard, h => h.File.StartsWith("sound/items/gauze/bandage_begin", StringComparison.Ordinal));
        Assert.Contains(r.Sounds.Heard, h => h.File.StartsWith("sound/items/gauze/bandage_end", StringComparison.Ordinal));
        Assert.Contains(r.Sounds.Heard, h => h.File.StartsWith("sound/items/handling/cloth/rip", StringComparison.Ordinal));

        // a second gauze is refused while the first is clean (< 20% better)
        r.Command(() => ClientSeam.Click(medic.client, victim, "left=1;button=left"));
        r.Step(3 * Second);
        r.In(() => Assert.Equal(5f, Dm.num(stack.amount)));

        // the gauze soaks up the cut's flow: absorption_rate 0.125 a second off the flow and off the gauze's capacity
        r.Step(4 * Second);
        r.In(() =>
        {
            obj_item_stack_medical_wrap_gauze wrapped = GauzeOf(Part(victim, BODY_ZONE_L_ARM));
            Assert.True(Dm.num(wrapped.absorption_capacity) < 5, "the gauze soaks up blood");
            Assert.True(Dm.num(cut.blood_flow) < flowAfterWrap, "the gauze stems the flow");
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnUnwoundedLimbIsWrappedSlowerAndASoakedGauzeFallsAwayInRags()
    {
        var r = Room(6902);
        mob_living_carbon_human medic = r.Join(5, 5, "Medic");
        mob_living_carbon_human victim = r.Dummy(6, 5);
        Hold(r, medic, BODY_ZONE_R_LEG);
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;

        r.Command(() => ClientSeam.Click(medic.client, victim, "left=1;button=left"));
        r.Step(2 * Second); // other_delay 2 s * 1.25 without a wound
        r.In(() => Assert.Null(GauzeOf(Part(victim, BODY_ZONE_R_LEG))));
        r.Step(Second);
        obj_item_stack_medical_wrap_gauze wrapped = r.In(() => GauzeOf(Part(victim, BODY_ZONE_R_LEG)));
        Assert.NotNull(wrapped);
        Assert.Contains(chat.Html, h => h.Contains("You wrap", StringComparison.Ordinal) && h.Contains("right leg", StringComparison.Ordinal));

        // the gauze is soaked through: it falls off the limb to the floor, used, and the bandage overlay goes
        var cut = (datum_wound_slash_flesh)Wound(r, victim, WOUND_SLASH, BODY_ZONE_R_LEG, WOUND_SEVERITY_MODERATE);
        r.In(() => wrapped.absorption_capacity = 0.05f);
        r.Step(2 * Second);
        r.In(() =>
        {
            Assert.Null(GauzeOf(Part(victim, BODY_ZONE_R_LEG)));
            Assert.Same(victim.loc, wrapped.loc);
            Assert.Equal("used medical gauze", Dm.str(wrapped.name));
            Assert.Equal("nothing", Dm.str(wrapped.worn_icon_state)); // update_wounds on COMSIG_ITEM_UNAPPLIED_FROM_LIMB
            Assert.DoesNotContain(BodyIcons(victim), s => s.StartsWith("icons/mob/human/bandage.dmi", StringComparison.Ordinal));
            Assert.Equal(1f, Dm.num(Part(victim, BODY_ZONE_R_LEG).get_splint_factor()));
        });
        // "falls away in rags" is the owner's visible_message to its viewers: messages to viewers come with hearing (M9,
        // mob.dm:280 deferred), so the medic is not told yet
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSelfChecksLinkTakesTheGauzeOffIntoTheHand()
    {
        var r = Room(6903);
        mob_living_carbon_human patient = r.Join(5, 5, "Patient");
        Hold(r, patient, BODY_ZONE_R_ARM);
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        // the patient wraps their own right arm: self_delay 5 s * 1.25 without a wound
        r.Command(() => ClientSeam.Click(patient.client, patient, "left=1;button=left"));
        r.Step(7 * Second);
        obj_item_stack_medical_wrap_gauze wrapped = r.In(() => GauzeOf(Part(patient, BODY_ZONE_R_ARM)));
        Assert.NotNull(wrapped);
        r.In(() => Waiting.NoSleep(patient.dropItemToGround(patient.get_active_held_item() as obj_item))); // a free hand for it

        r.In(() => patient.check_self_for_injuries());
        string selfCheck = string.Join("\n", chat.Html);
        Match link = Regex.Match(selfCheck, "href='(byond://\\?[^']*gauze_limb=[^']*)'");
        Assert.True(link.Success, selfCheck);
        r.Command(() => ClientSeam.Topic(patient.client, link.Groups[1].Value));
        r.Step(4 * Second); // help_remove_gauze: 3 s
        r.In(() =>
        {
            Assert.Null(GauzeOf(Part(patient, BODY_ZONE_R_ARM)));
            Assert.True(Dm.@in(wrapped, patient.held_items), "the gauze is put in the hand");
        });
        Assert.Contains(chat.Html, h => h.Contains("You finish removing", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void WirecuttersCutGauzeIntoCloth()
    {
        var r = Room(6904);
        mob_living_carbon_human medic = r.Join(5, 5, "Medic");
        (obj_item_stack_medical_wrap_gauze stack, obj_item_wirecutters cutters) = r.In(() =>
        {
            var g = Dm.@new<obj_item_stack_medical_wrap_gauze>(locate(6, 5, 1));
            var c = Dm.@new<obj_item_wirecutters>(medic.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(medic.put_in_active_hand(c))));
            return (g, c);
        });
        r.Command(() => ClientSeam.Click(medic.client, stack, "left=1;button=left"));
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal(4f, Dm.num(stack.amount)); // a clean gauze: two used
            Assert.Contains(Dm.each(medic.loc.contents), a => a is obj_item_stack_sheet_cloth);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRightClicksHealLoopDressesABurn()
    {
        var r = Room(6905);
        mob_living_carbon_human medic = r.Join(5, 5, "Medic");
        mob_living_carbon_human victim = r.Dummy(6, 5);
        var burn = (datum_wound_burn_flesh)Wound(r, victim, WOUND_BURN, BODY_ZONE_CHEST, WOUND_SEVERITY_SEVERE);
        obj_item_stack_medical_wrap_gauze stack = Hold(r, medic, BODY_ZONE_CHEST);
        (float healing, float sanitization, float amount) = r.In(() => (burn.flesh_healing, burn.sanitization, Dm.num(stack.amount)));

        r.Command(() => ClientSeam.Click(medic.client, victim, "right=1;button=right"));
        // other_delay 2 s * 0.9 for the manual heal; the heal and the gauze's use are one call (medical.dm:196, :354), so the wound is read in
        // the tick that applies them: every Life run of the victim eats flesh_healing and sanitization (burns.dm:79, :111) and SSmobs' first
        // fire is staggered by the Master's random 0 to 2 s (master.dm:621), so a fixed number of ticks may hold one
        for (int ticks = 0; r.In(() => Dm.num(stack.amount)) == amount; ticks++)
        {
            Assert.True(ticks <= 2 * Second + 2, "the heal did not finish within its delay");
            r.Step();
        }
        r.In(() =>
        {
            Assert.True(burn.flesh_healing >= healing + 5 - 0.5f, "heal_carbon adds the gauze's flesh_regeneration");
            Assert.True(burn.sanitization >= sanitization + 3 - 0.5f, "and its sanitization");
            Assert.Equal(amount - 1, Dm.num(stack.amount)); // one use
            Assert.Null(GauzeOf(Part(victim, BODY_ZONE_CHEST))); // the heal loop dresses, it does not wrap
        });
        r.AssertNoRuntimes();
    }
}
