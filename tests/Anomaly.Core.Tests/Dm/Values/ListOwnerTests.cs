using Anomaly.Core.DmShim;
using Xunit;

namespace Anomaly.Core.Tests.DmValues;

/// <summary>DmList.dm_owner: the engine owner of a builtin list is told after every write, by any path.</summary>
public class ListOwnerTests
{
    private sealed class Owner : IDmListOwner
    {
        public int Calls;
        public DmList Last;
        public void ListChanged(DmList list) { Calls++; Last = list; }
    }

    private static (DmList, Owner) Owned(params object[] items)
    {
        DmList L = Dm.list(items);
        var o = new Owner();
        L.dm_owner = o;
        return (L, o);
    }

    public static TheoryData<string> Writes => new()
    {
        "append", "remove", "Add", "Remove", "Cut", "Insert", "Swap", "Splice", "index", "assoc", "len grow", "len shrink", "or",
    };

    [Theory]
    [MemberData(nameof(Writes))]
    public void EveryWriteTellsTheOwner(string write)
    {
        (DmList L, Owner o) = Owned("a", "b", "c");
        switch (write)
        {
            case "append": Dm.append(ref L, "d"); break;
            case "remove": Dm.remove(ref L, "a"); break;
            case "Add": L.Add("d", "e"); break;
            case "Remove": L.Remove("b"); break;
            case "Cut": L.Cut(); break;
            case "Insert": L.Insert(1, "z"); break;
            case "Swap": L.Swap(1, 3); break;
            case "Splice": L.Splice(1, 2, "x", "y"); break;
            case "index": L[2] = "q"; break;
            case "assoc": L["a"] = 5; break;
            case "len grow": L.len = 5; break;
            case "len shrink": L.len = 1; break;
            case "or": Dm.combine(ref L, "d"); break;
        }
        Assert.True(o.Calls > 0, write);
        Assert.Same(L, o.Last);
    }

    [Fact]
    public void ReadsAndAnOrdinaryListTellNobody()
    {
        (DmList L, Owner o) = Owned("a", "b");
        _ = L[1];
        _ = L.Find("b");
        _ = L.Copy();
        foreach (object _ in Dm.each(L)) { }
        Assert.Equal(0, o.Calls);
        DmList copy = L.Copy();
        copy.Add("c");
        Assert.Equal(0, o.Calls);
        Assert.Null(copy.dm_owner);
    }
}
