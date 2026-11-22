using Anomaly.Core.DmShim;

namespace Anomaly.Core.Tests.DmValues;

/// <summary>
/// M3 I1d: <c>L -= x</c> and <c>L.Remove(x)</c> on a list with its occurrence index (8 or more items) find an item that occurs
/// once from both ends, and keep the backward scan for duplicates. The result is BYOND's in every case: the last occurrence
/// goes (BYOND ref <c>/list/proc/Remove</c>, "Removal starts at the end of the list"), numbers compare by value.
/// </summary>
public class ListRemoveLastTests
{
    private static object[] Items(DmList L)
    {
        var items = new object[(int)L.len];
        for (int i = 1; i <= L.len; i++) items[i - 1] = L[i];
        return items;
    }

    [Fact]
    public void UniqueItemsAreRemovedOldestFirstNewestFirstAndFromTheMiddle()
    {
        var things = new object[40];
        for (int i = 0; i < things.Length; i++) things[i] = new object();
        DmList L = Dm.list(things);
        Assert.Equal(1f, L.Remove(things[0]));                 // the oldest (SSarea_contents' order)
        Assert.Equal(1f, L.Remove(things[39]));                // the newest
        Assert.Equal(1f, L.Remove(things[20]));                // the middle
        Assert.Equal(0f, L.Remove(things[20]));                // gone
        var expected = things.Where((_, i) => i is not 0 and not 39 and not 20).ToArray();
        Assert.Equal(expected, Items(L));
        for (int i = 1; i < 39; i++)
            if (i != 20) L.Remove(things[i]);
        Assert.Equal(0f, L.len);
    }

    [Fact]
    public void ADuplicatedItemLosesItsLastOccurrence()
    {
        object a = new object(), b = new object();
        DmList L = Dm.list(a, b, b, b, b, b, b, b, a, b);
        L.Remove(a);
        Assert.Same(a, L[1]);                                    // the one at the end went
        Assert.Equal(9f, L.len);
        L.Remove(a);                                             // now unique: the first one goes
        Assert.Equal(8f, L.len);
        Assert.DoesNotContain(a, Items(L));
    }

    [Fact]
    public void NumbersCompareByValueAndTextIsNotANumber()
    {
        DmList L = Dm.list(1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 1.0d, "1");
        L.Remove(1f);                                            // 1 and 1.0 are equal: the later one (index 9) goes
        Assert.Equal(new object[] { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, "1" }, Items(L));
        L.Remove(1.0d);                                          // the remaining 1, unique now
        Assert.Equal(new object[] { 2f, 3f, 4f, 5f, 6f, 7f, 8f, "1" }, Items(L));
        L.Remove("1");
        Assert.Equal(new object[] { 2f, 3f, 4f, 5f, 6f, 7f, 8f }, Items(L));
    }
}
