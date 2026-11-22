// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/heap.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1: the open list of the jump point search). The var block is generated.

//////////////////////
//datum/heap object
//////////////////////

// tg: code/__HELPERS/heap.dm:5
public partial class datum_heap : datum
{
    // tg: code/__HELPERS/heap.dm:9
    [DmParams("compare")]
    public override async DmTask New(params object[] __extra)
    {
        object compare = Dm.arg(__extra, 0);
        L = Dm.list();
        cmp = compare;
        await base.New(__extra);
    }

    // tg: code/__HELPERS/heap.dm:13
    public override object Destroy(float? force = null)
    {
        foreach (object i in Dm.each(L)) // because this is before the list helpers are loaded
            qdel(i);
        L = null;
        return base.Destroy(force);
    }

    // tg: code/__HELPERS/heap.dm:19
    public virtual object is_empty()
    {
        return Dm.num(!Dm.truthy(length(L)));
    }

    //insert and place at its position a new node in the heap
    // tg: code/__HELPERS/heap.dm:23
    public virtual void insert(object A = null)
    {
        L.Add(A);
        swim(length(L));
    }

    //removes and returns the first element of the heap
    //(i.e the max or the min dependent on the comparison function)
    // tg: code/__HELPERS/heap.dm:30
    public virtual object pop()
    {
        if (!Dm.truthy(length(L)))
            return 0;
        object dot = L[1];

        L[1] = L[length(L)];
        L.Cut(length(L));
        if (Dm.truthy(length(L)))
            sink(1);
        return dot;
    }

    // call(cmp)(a, b) as a number
    private float compare(object a, object b) => Dm.num(Waiting.NoSleep(Dm.Call(null, (string)cmp, a, b)));

    //Get a node up to its right position in the heap
    // tg: code/__HELPERS/heap.dm:41
    public virtual void swim(object index = null)
    {
        float i = Dm.num(index);
        float parent = round(i * 0.5f);

        while (parent > 0 && compare(L[i], L[parent]) > 0)
        {
            L.Swap(i, parent);
            i = parent;
            parent = round(i * 0.5f);
        }
    }

    //Get a node down to its right position in the heap
    // tg: code/__HELPERS/heap.dm:50
    public virtual void sink(object index = null)
    {
        float i = Dm.num(index);
        float g_child = Dm.num(get_greater_child(i));

        while (g_child > 0 && compare(L[i], L[g_child]) < 0)
        {
            L.Swap(i, g_child);
            i = g_child;
            g_child = Dm.num(get_greater_child(i));
        }
    }

    //Returns the greater (relative to the comparison proc) of a node children
    //or 0 if there's no child
    // tg: code/__HELPERS/heap.dm:60
    public virtual object get_greater_child(object index = null)
    {
        float i = Dm.num(index);
        if (i * 2 > length(L))
            return 0;

        if (i * 2 + 1 > length(L))
            return i * 2;

        if (compare(L[i * 2], L[i * 2 + 1]) < 0)
            return i * 2 + 1;
        else
            return i * 2;
    }

    //Replaces a given node so it verify the heap condition
    // tg: code/__HELPERS/heap.dm:72
    public virtual void resort(object A = null)
    {
        float index = L.Find(A);

        swim(index);
        sink(index);
    }

    // tg: code/__HELPERS/heap.dm:78
    public virtual object List()
    {
        return L.Copy();
    }
}
