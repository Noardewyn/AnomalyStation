using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M6;

/// <summary>
/// Grasping one's own bleeding limb (carbon_defense.dm:518-602) in a running world: tg's ctrl-click on oneself (base_click_ctrl,
/// grab, grabbedby) with the bleeding arm's zone selected takes 0.75 seconds and puts the self-grasp hand item in the active hand,
/// which cuts the limb's bleed rate to 0.7 of it and slows its holder; dropping it lets go and the rate comes back. The hand of the
/// limb itself cannot grasp it, and a limb that does not bleed is not grasped.
/// </summary>
public class SelfGraspTests
{
    private const int Second = 20;

    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(12, 9, seed: seed);
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

    private static obj_item_bodypart Part(mob_living_carbon who, string zone) => who.get_bodypart(zone) as obj_item_bodypart;

    private static void CtrlClickSelf(AudioRoom r, mob_living_carbon_human player)
        => r.Command(() => ClientSeam.Click(player.client, player, "left=1;ctrl=1;button=left"));

    [Fact]
    public void ACtrlClickOnOneselfGraspsTheBleedingArmAndSlowsTheBleeding()
    {
        var r = Room(6901);
        mob_living_carbon_human player = r.Join(6, 5, "Bleeder");
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        r.Step(Second);
        float before = r.In(() =>
        {
            Assert.Contains("l_arm bleed stacks adjusted", DevCommands.Bleed(player, BODY_ZONE_L_ARM, 3), StringComparison.Ordinal);
            player.zone_selected = BODY_ZONE_L_ARM;
            player.swap_hand(2); // the right hand grasps the left arm
            return Part(player, BODY_ZONE_L_ARM).cached_bleed_rate;
        });
        Assert.True(before > 0f, "the arm bleeds");
        float speed = r.In(() => Dm.num(player.cached_multiplicative_slowdown));

        CtrlClickSelf(r, player);
        r.Step(Second / 2); // inside the 0.75 s do_after
        Assert.Null(r.In(() => player.get_active_held_item()));
        r.Step(Second);
        obj_item_hand_item_self_grasp grasp = r.In(() => player.get_active_held_item() as obj_item_hand_item_self_grasp);
        Assert.NotNull(grasp);
        r.In(() =>
        {
            obj_item_bodypart arm = Part(player, BODY_ZONE_L_ARM);
            Assert.Same(grasp, arm.grasped_by);
            Assert.Same(arm, grasp.grasped_part);
            Assert.Same(player, grasp.user);
            Assert.Equal(before * 0.7f, arm.cached_bleed_rate, 4);
            Assert.True(Dm.num(player.cached_multiplicative_slowdown) > speed, "the self-grasp slows its holder (SLOWS_WHILE_IN_HAND)");
            Assert.Equal("latexballoon", grasp.icon_state);
            Assert.Equal("icons/obj/weapons/hand.dmi", Dm.str(grasp.icon));
        });
        r.Step(2);
        Assert.True(chat.Html.Any(h => h.Contains("You try grasping at your human left arm, trying to stop the bleeding...", StringComparison.Ordinal)), string.Join("\n", chat.Html));
        Assert.Contains(chat.Html, h => h.Contains("You grab hold of your human left arm tightly.", StringComparison.Ordinal));
        Assert.NotEmpty(r.Sounds.Of(player, "sound/items/weapons/thudswoosh.ogg"));

        // letting go: the item is dropped (DROPDEL deletes it) and the limb bleeds as before
        r.In(() => Waiting.NoSleep(player.dropItemToGround(grasp)));
        r.Step(2);
        r.In(() =>
        {
            Assert.Null(Part(player, BODY_ZONE_L_ARM).grasped_by);
            Assert.Equal(before, Part(player, BODY_ZONE_L_ARM).cached_bleed_rate, 4);
            Assert.True(QDELETED(grasp));
        });
        Assert.Contains(chat.Html, h => h.Contains("You stop holding onto your human left arm.", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheArmsOwnHandCannotGraspItAndAWholeLimbIsNotGrasped()
    {
        var r = Room(6902);
        mob_living_carbon_human player = r.Join(6, 5, "Bleeder");
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        r.Step(Second);
        r.In(() =>
        {
            DevCommands.Bleed(player, BODY_ZONE_L_ARM, 3);
            player.zone_selected = BODY_ZONE_L_ARM;
            player.swap_hand(1); // the left hand
        });
        CtrlClickSelf(r, player);
        r.Step(Second * 2);
        Assert.Null(r.In(() => player.get_active_held_item()));
        Assert.Contains(chat.Html, h => h.Contains("You can't grasp your human left arm with itself!", StringComparison.Ordinal));

        r.In(() =>
        {
            player.zone_selected = BODY_ZONE_R_LEG; // not bleeding
            player.swap_hand(2);
        });
        CtrlClickSelf(r, player);
        r.Step(Second * 2);
        Assert.Null(r.In(() => player.get_active_held_item()));
        r.AssertNoRuntimes();
    }
}
