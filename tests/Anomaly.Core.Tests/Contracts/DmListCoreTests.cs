using Anomaly.Core.DmShim;

namespace Anomaly.Core.Tests.Contracts;

/// <summary>
/// The DmList core storage written by the contracts package (docs/ai/translation-rules.md 6.1, 6.2; BYOND ref
/// <c>/list</c>, "list associations", <c>/list/var/len</c>, <c>/proc/list</c>, <c>/proc/alist</c>).
/// </summary>
public class DmListCoreTests
{
    [Fact]
    public void ListKeepsItemsInOrderOneBased()
    {
        DmList L = Dm.list("a", 2f, null);
        Assert.Equal(3f, L.len);
        Assert.Equal(3, L.Count);
        Assert.Equal("a", L[1]);
        Assert.Equal(2f, L[2]);
        Assert.Null(L[3]);
        L[2] = "b";
        Assert.Equal("b", L[(object)2]);   // a boxed number index is a position too
        L[(object)3] = "c";
        Assert.Equal("c", L[3]);
    }

    [Fact]
    public void AListArgumentIsNestedNotFlattened()
    {
        DmList inner = Dm.list(1f, 2f);
        DmList L = Dm.list(inner, 3f);
        Assert.Equal(2f, L.len);
        Assert.Same(inner, L[1]);
    }

    [Fact]
    public void ANullParamsArrayIsOneNullItem()
    {
        DmList L = Dm.list(null);
        Assert.Equal(1f, L.len);
        Assert.Null(L[1]);
        Assert.Equal(0f, Dm.list().len);
    }

    [Fact]
    public void OutOfRangeIndexesAreRuntimeErrors()
    {
        DmList L = Dm.list("a");
        Assert.Throws<DmRuntimeException>(() => L[0]);
        Assert.Throws<DmRuntimeException>(() => L[2]);
        Assert.Throws<DmRuntimeException>(() => L[-1]);
        Assert.Throws<DmRuntimeException>(() => L[float.NaN]);
        Assert.Throws<DmRuntimeException>(() => L[2] = "x");
        Assert.Equal("a", L[1.9f]);                    // truncated, as OpenDream's DreamList.GetValue
    }

    [Fact]
    public void AssociationsByKey()
    {
        DmList L = Dm.list(Dm.kv("a", 1f), "b", Dm.kv(typeof(DmList), "type"));
        Assert.Equal(3f, L.len);
        Assert.Equal("a", L[1]);
        Assert.Equal("b", L[2]);
        Assert.Same(typeof(DmList), L[3]);
        Assert.Equal(1f, L["a"]);
        Assert.Null(L["b"]);                          // an item without an associated value
        Assert.Null(L["missing"]);                    // a missing key reads null
        Assert.Equal("type", L[typeof(DmList)]);

        L["b"] = 2f;                                  // an existing item gets a value, not a second item
        Assert.Equal(3f, L.len);
        Assert.Equal(2f, L["b"]);
        L["c"] = 3f;                                  // a missing key is appended
        Assert.Equal(4f, L.len);
        Assert.Equal("c", L[4]);
        Assert.Equal(3f, L["c"]);
        Assert.Null(L["C"]);                          // text keys are case-sensitive (rules 5.6)
    }

    [Fact]
    public void OverwritingAKeyItemByPositionTakesItsAssociationAlong()
    {
        // BYOND ref "list associations": an index item that is not in the list reads null, and assigning a value to
        // it adds the item.
        DmList L = Dm.list(Dm.kv("a", 1f));
        L[1] = "b";
        Assert.Null(L["a"]);
        Assert.Null(L["b"]);
        L["a"] = 2f;
        Assert.Equal(2f, L.len);
        Assert.Equal("a", L[2]);
        Assert.Equal(2f, L["a"]);
    }

    [Fact]
    public void OverwritingOneOfTwoCopiesOfAKeyKeepsItsAssociation()
    {
        // "Each unique text string or object in a list may be associated with another value" (BYOND ref "list
        // associations"): the association belongs to the item, which still occurs at position 2.
        DmList L = Dm.list("a", "a");
        L["a"] = 5f;
        L[1] = "b";
        Assert.Equal(5f, L["a"]);
    }

    [Fact]
    public void ARepeatedKeyKeepsOneItemAndTheLastValue()
    {
        DmList L = Dm.list(Dm.kv("a", 1f), Dm.kv("a", 2f));
        Assert.Equal(1f, L.len);
        Assert.Equal(2f, L["a"]);
    }

    [Fact]
    public void ObjectAndNullKeys()
    {
        var key = new object();
        DmList L = Dm.list();
        L[key] = "v";
        L[(object)null] = "n";
        Assert.Equal(2f, L.len);
        Assert.Same(key, L[1]);
        Assert.Null(L[2]);
        Assert.Equal("v", L[key]);
        Assert.Equal("n", L[(object)null]);
        Assert.Null(L[new object()]);
    }

    [Fact]
    public void LenGrowsWithNullsAndShrinksDroppingAssociations()
    {
        DmList L = Dm.list(Dm.kv("a", 1f), Dm.kv("b", 2f), Dm.kv("c", 3f));
        L.len = 5;
        Assert.Equal(5f, L.len);
        Assert.Null(L[5]);
        L.len = 1;
        Assert.Equal(1f, L.len);
        Assert.Equal(1f, L["a"]);
        Assert.Null(L["b"]);                          // its item is gone, so is the association
        L.len--;
        Assert.Equal(0f, L.len);
        L.len = 2.7f;
        Assert.Equal(2f, L.len);
        Assert.Throws<DmRuntimeException>(() => L.len = -1);
        Assert.Throws<DmRuntimeException>(() => L.len = float.NaN);
    }

    [Fact]
    public void ShrinkingKeepsAnAssociationWhoseKeyStillOccurs()
    {
        DmList L = Dm.list("a", "b", "a");
        L["a"] = 5f;
        L.len = 2;
        Assert.Equal(5f, L["a"]);
    }

    [Fact]
    public void ListNIsNNulls()
    {
        DmList L = Dm.list_n(3);
        Assert.Equal(3f, L.len);
        Assert.Null(L[1]);
        Assert.Null(L[3]);
    }

    [Fact]
    public void InAnAlistANumberIsAKey()
    {
        DmList A = Dm.alist(Dm.kv(10f, "ten"), Dm.kv("x", 1f));
        Assert.Equal(2f, A.len);
        Assert.Equal("ten", A[10]);
        Assert.Equal("ten", A[(object)10]);           // any numeric box finds the same key
        Assert.Null(A[1]);                            // not a position
        A[1] = "one";
        Assert.Equal(3f, A.len);
        Assert.Equal("one", A[1f]);
        A[10] = "TEN";                                // keys are unique
        Assert.Equal(3f, A.len);
        Assert.Equal("TEN", A[10]);
    }
}
