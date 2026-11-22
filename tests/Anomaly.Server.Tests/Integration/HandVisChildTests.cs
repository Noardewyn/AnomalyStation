using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Protocol version 12 (docs/ai/specs/protocol.md section 21, interaction.md section 2): tg draws the item in a hand or a slot through the slot
/// object's vis_contents (inventory_slot.dm:55, update_slot_icon_contents), and BYOND sends a click on the item's pixels to the item itself.
/// Through the real server on the default test room: the hand entry lists the held item as its child and follows it, a screen click on the
/// held toolbox with the other, empty hand opens its storage (tg's datum/storage on_attack: "parent.loc == user"), and a vis child of another
/// player's screen object is refused.
/// </summary>
public class HandVisChildTests
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static ClickTarget Screen(long id) => new(TargetKind.Screen, id, 0, 0, 0, 16, 16);

    // The screen entries whose children list the object.
    private static List<ScreenEntry> HoldersOf(ReplicaWorld replica, long id)
        => replica.Screen.Values.Where(e => e.VisChildren != null && e.VisChildren.Contains(id)).ToList();

    private static Session SessionOf(RealHeadlessWorld w, mob m) => w.Host.Sessions.Single(s => s.Client != null && ReferenceEquals(s.Client.mob, m));

    [Fact]
    public void A_screen_click_on_the_held_toolbox_with_the_other_empty_hand_opens_its_storage()
    {
        using var w = new RealHeadlessWorld();
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        w.Host.EnqueueConsoleLine("give Engineer /obj/item/storage/toolbox/mechanical");
        w.Run(5);
        var toolbox = In(w, () => me.get_active_held_item()) as obj_item_storage_toolbox_mechanical;
        Assert.NotNull(toolbox);
        var welder = In(w, () => Dm.each_cast<atom>(toolbox.contents).OfType<obj_item_weldingtool>().Single());

        // The client finds the toolbox among the children of its hand entry, which is tg's hand slot object.
        ScreenEntry hand = Assert.Single(HoldersOf(replica, toolbox.dm_id));
        Assert.True(In(w, () => w.World.Objects.TryGet(hand.Id, out object o) && o is atom_movable_screen_inventory_hand));
        ScreenChild child = Assert.Single(hand.VisChildren.Items);
        Assert.True(replica.TryGetAppearance(child.Appearance, out AppearanceEntry look));
        Assert.Equal(In(w, () => toolbox.icon_state), replica.ResolveString(look.IconState)); // the toolbox drawn alone
        Assert.True(replica.TryGetAppearance(hand.Appearance, out AppearanceEntry slot));
        Assert.Contains(child.Appearance, slot.Overlays); // the same row the slot's picture is flattened from

        // The other hand (X), then a screen click on the toolbox's own id: tg's item Click, ClickOn, attack_hand, the storage opens.
        p.Session.Keybinding(KeybindingCode.SwapHands);
        w.Run(5);
        Assert.Null(In(w, () => me.get_active_held_item()));
        Session session = SessionOf(w, me);
        int rejected = session.ClicksRejected;
        Assert.True(p.Session.Click(Screen(toolbox.dm_id), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Equal(rejected, session.ClicksRejected);
        Assert.True(replica.Screen.TryGetValue(welder.dm_id, out ScreenEntry inWindow), "the welder shows in the storage window");
        Assert.Equal(HudGroup.Storage, inWindow.Group);
        Assert.Same(toolbox, In(w, () => me.get_inactive_held_item()));
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }

    [Fact]
    public void The_hand_entry_follows_its_item_in_and_out_and_a_change_of_the_items_look()
    {
        using var w = new RealHeadlessWorld();
        RealHeadlessWorld.Player p = w.Join("Welder");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        w.Host.EnqueueConsoleLine("give Welder /obj/item/weldingtool");
        w.Run(5);
        var welder = (obj_item_weldingtool)In(w, () => me.get_active_held_item());
        ScreenEntry hand = Assert.Single(HoldersOf(replica, welder.dm_id));
        TableRef unlit = hand.VisChildren.Items[0].Appearance;

        // Lit (attack_self): the item's picture changes, and so does the child's appearance in the same hand entry.
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(welder.welding)));
        ScreenEntry lit = replica.Screen[hand.Id];
        Assert.Equal(welder.dm_id, Assert.Single(lit.VisChildren.Items).Id);
        Assert.NotEqual(unlit, lit.VisChildren.Items[0].Appearance);

        // Dropped (Q): the hand entry lists no child any more.
        p.Session.Keybinding(KeybindingCode.DropItem);
        w.Run(5);
        Assert.Null(In(w, () => me.get_active_held_item()));
        Assert.Null(replica.Screen[hand.Id].VisChildren);
        Assert.Empty(HoldersOf(replica, welder.dm_id));
    }

    [Fact]
    public void A_vis_child_of_another_players_screen_object_is_refused()
    {
        using var w = new RealHeadlessWorld();
        RealHeadlessWorld.Player owner = w.Join("Owner");
        RealHeadlessWorld.Player other = w.Join("Other");
        var ownerMob = (mob_living_carbon_human)w.MobOf(owner);
        var otherMob = (mob_living_carbon_human)w.MobOf(other);
        w.Host.EnqueueConsoleLine("give Owner /obj/item/storage/toolbox/mechanical");
        w.Run(5);
        var toolbox = (obj_item_storage_toolbox_mechanical)In(w, () => ownerMob.get_active_held_item());
        Assert.Single(HoldersOf(owner.Session.Replica, toolbox.dm_id));
        Assert.Empty(HoldersOf(other.Session.Replica, toolbox.dm_id)); // the private block is the owner's only

        Session session = SessionOf(w, otherMob);
        int rejected = session.ClicksRejected;
        Assert.True(other.Session.Click(Screen(toolbox.dm_id), ClickButton.Left, ClickModifiers.None));
        w.Run(5);
        Assert.Equal(rejected + 1, session.ClicksRejected);
        Assert.True(other.Session.Hover(Screen(toolbox.dm_id), w.Now));
        w.Run(5);
        Assert.Equal(rejected + 2, session.ClicksRejected);
        Assert.Same(toolbox, In(w, () => ownerMob.get_active_held_item()));
        Assert.Null(In(w, () => otherMob.get_active_held_item()));
        Assert.DoesNotContain(other.Session.Replica.Screen.Values, e => e.Group == HudGroup.Storage);

        // The owner's own hover on it is a screen hover (tg's MouseEntered on the item), not refused.
        Session own = SessionOf(w, ownerMob);
        int ownRejected = own.ClicksRejected, hovers = own.HoversApplied;
        Assert.True(owner.Session.Hover(Screen(toolbox.dm_id), w.Now));
        w.Run(5);
        Assert.Equal(ownRejected, own.ClicksRejected);
        Assert.Equal(hovers + 1, own.HoversApplied);
    }
}
