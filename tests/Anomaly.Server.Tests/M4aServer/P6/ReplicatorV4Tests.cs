using System.Text;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Replication;
using Anomaly.Server.Tests.Replication;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.M4aServer.P6;

/// <summary>
/// The replicator of protocol version 4 (docs/ai/specs/replication.md section 14): the screen entries' HUD group, the private
/// images of the own client, and the Thrown flag; and the secret test (D40): nothing private to one session reaches another.
/// </summary>
public class ReplicatorV4Tests
{
    private static mob_living_carbon_human NewHuman(V2Run run, int x, int y) => run.In(() => Dm.@new<mob_living_carbon_human>(run.Turf(x, y)));

    private static obj_item NewItem(V2Run run, int x, int y, string state)
        => run.In(() =>
        {
            var o = Dm.@new<obj_item>(run.Turf(x, y));
            o.icon = Dm.resource("icons/obj/items_and_weapons.dmi");
            o.icon_state = state;
            return o;
        });

    private static void Group(V2Run run, SessionView s, int group, params atom_movable[] members)
        => run.In(() =>
        {
            datum_hud hud = s.Avatar.hud_used;
            hud.screen_groups[(float)group] = members.Length == 0 ? null : Dm.list(members.Cast<object>().ToArray());
        });

    // ---- the group byte ----

    [Fact]
    public void AScreenEntrysGroupIsTheGroupOfTheMobsHudThatListsIt()
    {
        V2Run run = V2Run.Quiet();
        SessionView me = run.Connect("Me", NewHuman(run, 4, 4));
        run.Step();
        // tg's own hands and slots are in the static group (inventory_slot.dm), the health doll in the info group.
        Assert.NotEmpty(me.Replica.ScreenGroup(HudGroup.Static));
        Assert.All(me.Replica.ScreenGroup(HudGroup.Static), e => Assert.Equal(HudGroup.Static, e.Group));
        Assert.Contains(me.Replica.Screen.Values, e => e.Group == HudGroup.Info);

        // An open storage: the items and the cells in the storage group. They are screen objects of the client and in the group's list.
        obj_item a = NewItem(run, 9, 9, "wrench"), b = NewItem(run, 9, 10, "screwdriver");
        run.In(() =>
        {
            me.Client.screen.Add(a);
            me.Client.screen.Add(b);
            a.screen_loc = "CENTER-1,SOUTH+2";
            b.screen_loc = "CENTER,SOUTH+2";
        });
        Group(run, me, HUD_GROUP_STORAGE, a, b);
        run.Step();
        Assert.Equal(new[] { a.dm_id, b.dm_id }, me.Replica.ScreenGroup(HudGroup.Storage).Select(e => e.Id));
        Assert.Equal("CENTER-1,SOUTH+2", me.Replica.Screen[a.dm_id].ScreenLoc);

        // A change of group alone is a changed entry; leaving every group is group none.
        var changed = new List<ScreenEntry>();
        me.Replica.ScreenEntryChanged += changed.Add;
        Group(run, me, HUD_GROUP_STORAGE, b);
        Group(run, me, HUD_GROUP_TOGGLEABLE_INVENTORY, a);
        run.Step();
        Assert.Equal(new[] { (a.dm_id, HudGroup.ToggleableInventory) }, changed.Select(e => (e.Id, e.Group)));
        changed.Clear();
        Group(run, me, HUD_GROUP_STORAGE);
        Group(run, me, HUD_GROUP_TOGGLEABLE_INVENTORY);
        run.Step();
        Assert.Equal(new[] { (a.dm_id, HudGroup.None), (b.dm_id, HudGroup.None) }, changed.OrderBy(e => e.Id).Select(e => (e.Id, e.Group)));
        run.Step();
        Assert.Equal(2, changed.Count); // nothing changed: nothing sent
    }

    // ---- the private images ----

    private static image ProgressBar(atom target, string state = "prog_bar_0")
        => new() { icon = Dm.resource("icons/effects/progressbar.dmi"), icon_state = state, loc = target, plane = 19, layer = 1 };

    [Fact]
    public void ADistantPointingArrowReplicatesItsTargetOffset()
    {
        V2Run run = V2Run.Quiet();
        SessionView me = run.Connect("Me", NewHuman(run, 4, 4));
        obj arrow = run.In(() => Anomaly.Core.Kernel.Waiting.NoSleep(me.Avatar.point_at(run.Turf(10, 4), 1)) as obj);
        run.Step();
        Assert.True(me.Replica.TryGet(arrow.dm_id, out ReplicaEntity seen));
        Assert.True(me.Replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry appearance));
        Assert.Equal((10f, 4f), Anomaly.Client.Shared.PointArrowPlacement.Target(seen.State.X, seen.State.Y, appearance));
    }

    [Fact]
    public void AnEmptyOverrideImageReachesOnlyItsOwnerWithoutDisconnecting()
    {
        V2Run run = V2Run.Quiet();
        SessionView me = run.Connect("Me", NewHuman(run, 4, 4));
        SessionView other = run.Connect("Other", NewHuman(run, 8, 8));
        var hidden = new image { loc = other.Avatar, @override = 1 };
        run.In(() => me.Client.images.Add(hidden));
        run.Step(); // Real replicator -> encoded message -> client replica.
        ImageEntry entry = Assert.Single(me.Replica.Images.Values);
        Assert.True(entry.Override);
        Assert.True(entry.Appearance.IsNone);
        Assert.Equal(other.Avatar.dm_id, entry.AttachedTo);
        Assert.Empty(other.Replica.Images);

        run.In(() => me.Client.images.Remove(hidden));
        run.Step();
        Assert.Empty(me.Replica.Images);
    }

    [Fact]
    public void AnImageOfTheClientsImagesListIsSentToItsOwnerWithItsAttachment()
    {
        V2Run run = V2Run.Quiet();
        SessionView me = run.Connect("Me", NewHuman(run, 4, 4));
        SessionView other = run.Connect("Other", NewHuman(run, 8, 8));
        obj_item target = NewItem(run, 5, 4, "welder");
        image bar = ProgressBar(target);
        run.In(() => me.Client.images.Add(bar));
        run.Step();
        ImageEntry entry = me.Replica.Images.Values.Single();
        Assert.Equal(target.dm_id, entry.AttachedTo);
        Assert.True(entry.Appearance.Private);
        Assert.Equal(AppearanceText.Of(bar, true, keepDir: true), AppearanceText.Of(me.Replica, entry.Appearance));
        Assert.Contains("prog_bar_0", me.Replica.PrivateStrings);

        // The bar's state changes each tick (tg's progress bar): a changed entry with the same id.
        var seen = new List<ImageEntry>();
        me.Replica.ImageChanged += seen.Add;
        run.In(() => bar.icon_state = "prog_bar_40");
        run.Step();
        Assert.Equal(entry.Id, seen.Single().Id);
        Assert.Equal(AppearanceText.Of(bar, true, keepDir: true), AppearanceText.Of(me.Replica, me.Replica.Images[entry.Id].Appearance));
        run.Step();
        Assert.Single(seen); // nothing changed: nothing sent

        // Hanging on a turf, a nullspace atom or a deleted one: attached to nothing (drawn under the crosshair).
        run.In(() => bar.loc = run.Turf(5, 4));
        run.Step();
        Assert.Equal(0, me.Replica.Images[entry.Id].AttachedTo);
        run.In(() => bar.loc = target);
        run.Step();
        Assert.Equal(target.dm_id, me.Replica.Images[entry.Id].AttachedTo);

        var gone = new List<long>();
        me.Replica.ImageRemoved += gone.Add;
        run.In(() => me.Client.images.Remove(bar));
        run.Step();
        Assert.Equal(new[] { entry.Id }, gone);
        Assert.Empty(me.Replica.Images);

        // A second image gets a new number: the session never reuses one.
        image second = ProgressBar(target, "prog_bar_10");
        run.In(() => me.Client.images.Add(second));
        run.Step();
        Assert.NotEqual(entry.Id, me.Replica.Images.Keys.Single());
        Assert.Empty(other.Replica.Images);

        // A rejoin lists the images afresh in its Snapshot.
        run.Leave(me);
        SessionView again = run.Connect("Me2", NewHuman(run, 6, 6));
        run.In(() => again.Client.images.Add(second));
        run.Step();
        Assert.Single(again.Replica.Images);
    }

    [Fact]
    public void ThereAreAtMostTheCapOfImagesAndTheFirstOnesWin()
    {
        V2Run run = V2Run.Quiet();
        SessionView me = run.Connect("Me", NewHuman(run, 4, 4));
        run.In(() =>
        {
            for (int i = 0; i < Limits.MaxPrivateImages + 20; i++)
                me.Client.images.Add(ProgressBar(null, "prog_bar_" + i));
        });
        run.Step();
        Assert.Equal(Limits.MaxPrivateImages, me.Replica.Images.Count);
        Assert.Equal(Enumerable.Range(1, Limits.MaxPrivateImages).Select(i => (long)i), me.Replica.Images.Keys.OrderBy(k => k));
        Assert.Contains("prog_bar_0", me.Replica.PrivateStrings);
        Assert.DoesNotContain("prog_bar_" + Limits.MaxPrivateImages, me.Replica.PrivateStrings);
    }

    // ---- the secret test (D40) ----

    [Fact]
    public void NothingPrivateToOneSessionReachesAnother()
    {
        V2Run run = V2Run.Quiet();
        SessionView me = run.Connect("Me", NewHuman(run, 4, 4));
        SessionView other = run.Connect("Other", NewHuman(run, 8, 8));
        // My open storage's contents are in the container, not on a turf: they are screen objects of my client and no entity.
        obj_item secretItem = null, cell = null;
        run.In(() =>
        {
            secretItem = Dm.@new<obj_item>(null);
            secretItem.icon = Dm.resource("icons/obj/secret_storage_item.dmi");
            secretItem.icon_state = "secret_state_of_the_item";
            secretItem.screen_loc = "CENTER-1,SOUTH+2";
            cell = Dm.@new<obj_item>(null);
            cell.icon = Dm.resource("icons/hud/storage.dmi");
            cell.icon_state = "storage_cell_state";
            me.Client.screen.Add(secretItem);
            me.Client.screen.Add(cell);
            me.Client.images.Add(ProgressBar(secretItem, "prog_bar_secret"));
        });
        Group(run, me, HUD_GROUP_STORAGE, secretItem, cell);
        for (int i = 0; i < 5; i++) run.Step();

        // They are mine ...
        Assert.Contains(secretItem.dm_id, me.Replica.Screen.Keys);
        Assert.Equal(new[] { secretItem.dm_id, cell.dm_id }, me.Replica.ScreenGroup(HudGroup.Storage).Select(e => e.Id));
        Assert.Single(me.Replica.Images);
        Assert.Contains("secret_state_of_the_item", me.Replica.PrivateStrings);
        Assert.False(me.Replica.TryGet(secretItem.dm_id, out _));
        // ... and not the other session's: not in its replica, and not in a byte it was sent (nor in the public tables every session gets).
        Assert.Empty(other.Replica.Images);
        Assert.DoesNotContain(secretItem.dm_id, other.Replica.Screen.Keys);
        Assert.DoesNotContain(cell.dm_id, other.Replica.Screen.Keys);
        Assert.Empty(other.Replica.ScreenGroup(HudGroup.Storage));
        Assert.False(other.Replica.TryGet(secretItem.dm_id, out _));
        Assert.DoesNotContain("prog_bar_secret", other.Replica.PrivateStrings);
        foreach (string secret in new[] { "prog_bar_secret", "progressbar.dmi", "CENTER-1,SOUTH+2", "storage_cell_state", "secret_state_of_the_item", "secret_storage_item.dmi", "hud/storage.dmi" })
        {
            Assert.DoesNotContain(secret, me.Replica.Strings);
            Assert.DoesNotContain(secret, me.Replica.Resources);
            byte[] needle = Encoding.UTF8.GetBytes(secret);
            foreach (byte[] message in other.Bytes)
                Assert.False(message.AsSpan().IndexOf(needle) >= 0, $"the other session was sent '{secret}'");
            // The owner's own bytes do hold them (in its private block), so the search finds what it looks for.
        }

        Assert.Contains(me.Bytes, m => m.AsSpan().IndexOf(Encoding.UTF8.GetBytes("secret_state_of_the_item")) >= 0);
    }

    // ---- the Thrown flag ----

    [Fact]
    public void TheThrownFlagFollowsTgsThrowingAndIsPolledSoItsEndIsNotMissed()
    {
        // A sweep of one entity a tick: only the flagged poll can find the end of a throw quickly.
        V2Run run = V2Run.Quiet(sweepDivisor: 100000, minSweep: 1);
        SessionView me = run.Connect("Me", NewHuman(run, 4, 4));
        obj_item item = NewItem(run, 6, 6, "baseball_bat");
        run.Step();
        me.Replica.TryGet(item.dm_id, out ReplicaEntity seen);
        Assert.False(seen.IsThrown);

        run.In(() => item.throwing = Dm.@new<datum_thrownthing>());
        for (int i = 0; i < 40 && !seen.IsThrown; i++) run.Step(); // found by the sweep or a hint
        Assert.True(seen.IsThrown);
        Assert.Equal(EntityFlags.Thrown, seen.Flags);

        run.In(() => item.throwing = null);
        run.Step();
        Assert.False(seen.IsThrown); // the polled entity is read in the tick it ends

        // A newcomer's Snapshot carries the flag of an entity in flight.
        run.In(() => item.throwing = Dm.@new<datum_thrownthing>());
        for (int i = 0; i < 40 && !seen.IsThrown; i++) run.Step();
        SessionView late = run.Connect("Late", NewHuman(run, 8, 8));
        late.Replica.TryGet(item.dm_id, out ReplicaEntity lateSeen);
        Assert.True(lateSeen.IsThrown);

        // A thrown entity that is deleted leaves the polled set (no stale poll of a gone id).
        run.In(() => GlobalProcs.qdel(item));
        for (int i = 0; i < 4; i++) run.Step();
        Assert.False(me.Replica.TryGet(item.dm_id, out _));
    }

    [Fact]
    public void TheWhitelistHasTheFlagsRow()
    {
        Assert.Contains(ReplicatedFields.All, f => f.Field == NetField.Flags);
        Assert.Equal(EntityFlags.None, ReplicatedFields.WireFlags(new obj_item()));
    }
}
