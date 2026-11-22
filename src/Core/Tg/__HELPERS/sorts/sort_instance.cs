// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/sorts/sort_instance.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

//These are macros used to reduce on proc calls
// tg: code/__HELPERS/sorts/sort_instance.dm:2  #define fetchElement(L, i) (associative) ? L[L[i]] : L[i]
// A file-local macro that reads src.associative (rules 11.1 (d)): expanded inline at each use as
// (Dm.truthy(associative) ? L[L[i]] : L[i]). Its argument appears in both branches, of which only one runs, so it
// is evaluated once in DM and in the expansion.

// tg: code/__HELPERS/sorts/sort_instance.dm:12  GLOBAL_DATUM_INIT(sortInstance, /datum/sort_instance, new())
//This is a global instance to allow much of this code to be reused. The interfaces are kept separately
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/__HELPERS/sorts/sort_instance.dm:12  (GLOBAL_RAW(/datum/sort_instance/sortInstance), __DEFINES/__globals.dm:32)
    public datum_sort_instance sortInstance;

    // tg: code/__HELPERS/sorts/sort_instance.dm:12  (GLOBAL_MANAGED(sortInstance, new()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalsortInstance()
    {
        sortInstance = Dm.@new<datum_sort_instance>();
        Dm.append(ref gvars_datum_init_order, "sortInstance");
    }
}

// tg: code/__HELPERS/sorts/sort_instance.dm:13
[DmPath("/datum/sort_instance")]
public partial class datum_sort_instance : datum
{
    // tg: code/__HELPERS/sorts/sort_instance.dm:6  #define MIN_MERGE 32 (file-local, #undef at :651)
    //Minimum sized sequence that will be merged. Anything smaller than this will use binary-insertion sort.
    //Should be a power of 2
    private const int MIN_MERGE = 32;

    // tg: code/__HELPERS/sorts/sort_instance.dm:9  #define MIN_GALLOP 7 (file-local, #undef at :650)
    //When we get into galloping mode, we stay there until both runs win less often than MIN_GALLOP consecutive times.
    private const int MIN_GALLOP = 7;

    // tg: code/__HELPERS/sorts/sort_instance.dm:15
    //The array being sorted.
    public DmList L;

    // tg: code/__HELPERS/sorts/sort_instance.dm:18
    //The comparator proc-reference
    public string cmp = "/proc/" + nameof(GlobalProcs.cmp_numeric_asc);

    // tg: code/__HELPERS/sorts/sort_instance.dm:21
    //whether we are sorting list keys (0: L[i]) or associated values (1: L[L[i]])
    public float associative = 0;

    // tg: code/__HELPERS/sorts/sort_instance.dm:25
    //This controls when we get *into* galloping mode.  It is initialized to MIN_GALLOP.
    //The mergeLo and mergeHi methods nudge it higher for random data, and lower for highly structured data.
    public float minGallop = MIN_GALLOP;

    // tg: code/__HELPERS/sorts/sort_instance.dm:30
    //Stores information regarding runs yet to be merged.
    //Run i starts at runBase[i] and extends for runLen[i] elements.
    //runBase[i] + runLen[i] == runBase[i+1]
    public DmList runBases;
    // tg: code/__HELPERS/sorts/sort_instance.dm:31
    public DmList runLens;

    // tg: code/__HELPERS/sorts/sort_instance.dm:30-31 (object-creating defaults, rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        runBases = Dm.list();
        runLens = Dm.list();
    }

    // tg: code/__HELPERS/sorts/sort_instance.dm:34
    public virtual object timSort(object start = null, object end = null)
    {
        runBases.Cut();
        runLens.Cut();

        object remaining = Dm.sub(end, start);

        //If array is small, do a 'mini-TimSort' with no merges
        if (Dm.lt(remaining, MIN_MERGE))
        {
            object initRunLen = countRunAndMakeAscending(start, end);
            binarySort(start, end, Dm.add(start, initRunLen));
            return null;
        }

        //March over the array finding natural runs
        //Extend any short natural runs to runs of length minRun
        object minRun = minRunLength(remaining);

        do
        {
            //identify next run
            object runLen = countRunAndMakeAscending(start, end);

            //if run is short, extend to min(minRun, remaining)
            if (Dm.lt(runLen, minRun))
            {
                object force = (Dm.le(remaining, minRun) ? remaining : minRun);

                binarySort(start, Dm.add(start, force), Dm.add(start, runLen));
                runLen = force;
            }

            //add data about run to queue
            runBases.Add(start);
            runLens.Add(runLen);

            //maybe merge
            mergeCollapse();

            //Advance to find next run
            Dm.append(ref start, runLen);
            Dm.remove(ref remaining, runLen);
        }
        while (Dm.gt(remaining, 0));

        //Merge all remaining runs to complete sort
        //ASSERT(start == end)
        mergeForceCollapse();
        //ASSERT(runBases.len == 1)

        //reset minGallop, for successive calls
        minGallop = MIN_GALLOP;

        return L;
    }

    /*
    Sorts the specified portion of the specified array using a binary
    insertion sort.  This is the best method for sorting small numbers
    of elements.  It requires O(n log n) compares, but O(n^2) data
    movement (worst case).

    If the initial part of the specified range is already sorted,
    this method can take advantage of it: the method assumes that the
    elements in range [lo,start) are already sorted

    lo the index of the first element in the range to be sorted
    hi the index after the last element in the range to be sorted
    start the index of the first element in the range that is not already known to be sorted
    */
    // tg: code/__HELPERS/sorts/sort_instance.dm:99
    public virtual void binarySort(object lo = null, object hi = null, object start = null)
    {
        //ASSERT(lo <= start && start <= hi)
        if (Dm.le(start, lo))
            start = Dm.add(lo, 1);

        DmList L = this.L;
        for (float start__it = Dm.num(start), start__end = Dm.num(Dm.sub(hi, 1)); start__it <= start__end; start__it++)
        {
            start = start__it;
            object pivot = (Dm.truthy(associative) ? L[L[start]] : L[start]);

            //set left and right to the index where pivot belongs
            object left = lo;
            object right = start;
            //ASSERT(left <= right)

            //[lo, left) elements <= pivot < [right, start) elements
            //in other words, find where the pivot element should go using bisection search
            while (Dm.lt(left, right))
            {
                float mid = Dm.shr(Dm.add(left, right), 1); //round((left+right)/2)
                if (Dm.gt(Waiting.NoSleep(Dm.Call(null, cmp, (Dm.truthy(associative) ? L[L[mid]] : L[mid]), pivot)), 0))
                    right = mid;
                else
                    left = mid + 1;
            }

            //ASSERT(left == right)
            move_element(L, start, left); //move pivot element to correct location in the sorted range
        }
    }

    /*
    Returns the length of the run beginning at the specified position and reverses the run if it is back-to-front

    A run is the longest ascending sequence with:
        a[lo] <= a[lo + 1] <= a[lo + 2] <= ...
    or the longest descending sequence with:
        a[lo] >  a[lo + 1] >  a[lo + 2] >  ...

    For its intended use in a stable mergesort, the strictness of the
    definition of "descending" is needed so that the call can safely
    reverse a descending sequence without violating stability.
    */
    // tg: code/__HELPERS/sorts/sort_instance.dm:137
    public virtual object countRunAndMakeAscending(object lo = null, object hi = null)
    {
        //ASSERT(lo < hi)

        object runHi = Dm.add(lo, 1);
        if (Dm.ge(runHi, hi))
            return 1;

        DmList L = this.L;
        object last = (Dm.truthy(associative) ? L[L[lo]] : L[lo]);
        object current = (Dm.truthy(associative) ? L[L[Dm.postinc(ref runHi)]] : L[Dm.postinc(ref runHi)]);

        if (Dm.lt(Waiting.NoSleep(Dm.Call(null, cmp, current, last)), 0))
        {
            while (Dm.lt(runHi, hi))
            {
                last = current;
                current = (Dm.truthy(associative) ? L[L[runHi]] : L[runHi]);
                if (Dm.ge(Waiting.NoSleep(Dm.Call(null, cmp, current, last)), 0))
                    break;
                Dm.inc(ref runHi);
            }
            reverse_range(L, Dm.num_or_null(lo), Dm.num_or_null(runHi));
        }
        else
        {
            while (Dm.lt(runHi, hi))
            {
                last = current;
                current = (Dm.truthy(associative) ? L[L[runHi]] : L[runHi]);
                if (Dm.lt(Waiting.NoSleep(Dm.Call(null, cmp, current, last)), 0))
                    break;
                Dm.inc(ref runHi);
            }
        }

        return Dm.sub(runHi, lo);
    }

    //Returns the minimum acceptable run length for an array of the specified length.
    //Natural runs shorter than this will be extended with binarySort
    // tg: code/__HELPERS/sorts/sort_instance.dm:168
    public virtual object minRunLength(object n = null)
    {
        //ASSERT(n >= 0)
        float r = 0; //becomes 1 if any bits are shifted off
        while (Dm.ge(n, MIN_MERGE))
        {
            Dm.combine(ref r, Dm.bitand(n, 1));
            n = Dm.shr(Dm.num(n), 1);
        }
        return Dm.add(n, r);
    }

    //Examines the stack of runs waiting to be merged and merges adjacent runs until the stack invariants are reestablished:
    // runLen[i-3] > runLen[i-2] + runLen[i-1]
    // runLen[i-2] > runLen[i-1]
    //This method is called each time a new run is pushed onto the stack.
    //So the invariants are guaranteed to hold for i<stackSize upon entry to the method
    // tg: code/__HELPERS/sorts/sort_instance.dm:181
    public virtual void mergeCollapse()
    {
        while (runBases.len >= 2)
        {
            float n = runBases.len - 1;
            if (n > 1 && Dm.le(runLens[n - 1], Dm.add(runLens[n], runLens[n + 1])))
            {
                if (Dm.lt(runLens[n - 1], runLens[n + 1]))
                    --n;
                mergeAt(n);
            }
            else if (Dm.le(runLens[n], runLens[n + 1]))
            {
                mergeAt(n);
            }
            else
            {
                break; //Invariant is established
            }
        }
    }

    //Merges all runs on the stack until only one remains.
    //Called only once, to finalise the sort
    // tg: code/__HELPERS/sorts/sort_instance.dm:196
    public virtual void mergeForceCollapse()
    {
        while (runBases.len >= 2)
        {
            float n = runBases.len - 1;
            if (n > 1 && Dm.lt(runLens[n - 1], runLens[n + 1]))
                --n;
            mergeAt(n);
        }
    }

    //Merges the two consecutive runs at stack indices i and i+1
    //Run i must be the penultimate or antepenultimate run on the stack
    //In other words, i must be equal to stackSize-2 or stackSize-3
    // tg: code/__HELPERS/sorts/sort_instance.dm:207
    public virtual void mergeAt(object i = null)
    {
        //ASSERT(runBases.len >= 2)
        //ASSERT(i >= 1)
        //ASSERT(i == runBases.len - 1 || i == runBases.len - 2)

        object base1 = runBases[i];
        object base2 = runBases[Dm.add(i, 1)];
        object len1 = runLens[i];
        object len2 = runLens[Dm.add(i, 1)];

        //ASSERT(len1 > 0 && len2 > 0)
        //ASSERT(base1 + len1 == base2)

        //Record the legth of the combined runs. If i is the 3rd last run now, also slide over the last run
        //(which isn't involved in this merge). The current run (i+1) goes away in any case.
        Dm.append_at(runLens, i, runLens[Dm.add(i, 1)]);
        runLens.Cut(Dm.num(Dm.add(i, 1)), Dm.num(Dm.add(i, 2)));
        runBases.Cut(Dm.num(Dm.add(i, 1)), Dm.num(Dm.add(i, 2)));

        //Find where the first element of run2 goes in run1.
        //Prior elements in run1 can be ignored (because they're already in place)
        object k = gallopRight((Dm.truthy(associative) ? L[L[base2]] : L[base2]), base1, len1, 0);
        //ASSERT(k >= 0)
        Dm.append(ref base1, k);
        Dm.remove(ref len1, k);
        if (Dm.eq(len1, 0))
            return;

        //Find where the last element of run1 goes in run2.
        //Subsequent elements in run2 can be ignored (because they're already in place)
        len2 = gallopLeft((Dm.truthy(associative) ? L[L[Dm.sub(Dm.add(base1, len1), 1)]] : L[Dm.sub(Dm.add(base1, len1), 1)]), base2, len2, Dm.sub(len2, 1));
        //ASSERT(len2 >= 0)
        if (Dm.eq(len2, 0))
            return;

        //Merge remaining runs, using tmp array with min(len1, len2) elements
        if (Dm.le(len1, len2))
            mergeLo(base1, len1, base2, len2);
        else
            mergeHi(base1, len1, base2, len2);
    }

    /*
        Locates the position to insert key within the specified sorted range
        If the range contains elements equal to key, this will return the index of the LEFTMOST of those elements

        key the element to be inserted into the sorted range
        base the index of the first element of the sorted range
        len the length of the sorted range, must be greater than 0
        hint the offset from base at which to begin the search, such that 0 <= hint < len; i.e. base <= hint < base+hint

        Returns the index at which to insert element 'key'
    */
    // tg: code/__HELPERS/sorts/sort_instance.dm:260
    public virtual object gallopLeft(object key = null, object @base = null, object len = null, object hint = null)
    {
        //ASSERT(len > 0 && hint >= 0 && hint < len)

        DmList L = this.L;
        object lastOffset = 0;
        object offset = 1;
        if (Dm.gt(Waiting.NoSleep(Dm.Call(null, cmp, key, (Dm.truthy(associative) ? L[L[Dm.add(@base, hint)]] : L[Dm.add(@base, hint)]))), 0))
        {
            object maxOffset = Dm.sub(len, hint);
            while (Dm.lt(offset, maxOffset) && Dm.gt(Waiting.NoSleep(Dm.Call(null, cmp, key, (Dm.truthy(associative) ? L[L[Dm.add(Dm.add(@base, hint), offset)]] : L[Dm.add(Dm.add(@base, hint), offset)]))), 0))
            {
                lastOffset = offset;
                offset = Dm.shl(offset, 1) + 1;
            }

            if (Dm.gt(offset, maxOffset))
                offset = maxOffset;

            Dm.append(ref lastOffset, hint);
            Dm.append(ref offset, hint);
        }
        else
        {
            object maxOffset = Dm.add(hint, 1);
            while (Dm.lt(offset, maxOffset) && Dm.le(Waiting.NoSleep(Dm.Call(null, cmp, key, (Dm.truthy(associative) ? L[L[Dm.sub(Dm.add(@base, hint), offset)]] : L[Dm.sub(Dm.add(@base, hint), offset)]))), 0))
            {
                lastOffset = offset;
                offset = Dm.shl(offset, 1) + 1;
            }

            if (Dm.gt(offset, maxOffset))
                offset = maxOffset;

            object temp = lastOffset;
            lastOffset = Dm.sub(hint, offset);
            offset = Dm.sub(hint, temp);

            //ASSERT(-1 <= lastOffset && lastOffset < offset && offset <= len)
        }

        //Now L[base+lastOffset] < key <= L[base+offset], so key belongs somewhere to the right of lastOffset but no farther than
        //offset. Do a binary search with invariant L[base+lastOffset-1] < key <= L[base+offset]
        Dm.inc(ref lastOffset);
        while (Dm.lt(lastOffset, offset))
        {
            object m = Dm.add(lastOffset, Dm.shr(Dm.sub(offset, lastOffset), 1));

            if (Dm.gt(Waiting.NoSleep(Dm.Call(null, cmp, key, (Dm.truthy(associative) ? L[L[Dm.add(@base, m)]] : L[Dm.add(@base, m)]))), 0))
                lastOffset = Dm.add(m, 1);
            else
                offset = m;
        }

        //ASSERT(lastOffset == offset)
        return offset;
    }

    /**
     * Like gallopLeft, except that if the range contains an element equal to
     * key, gallopRight returns the index after the rightmost equal element.
     *
     * @param key the key whose insertion point to search for
     * @param a the array in which to search
     * @param base the index of the first element in the range
     * @param len the length of the range; must be > 0
     * @param hint the index at which to begin the search, 0 <= hint < n.
     *  The closer hint is to the result, the faster this method will run.
     * @param c the comparator used to order the range, and to search
     * @return the int k,  0 <= k <= n such that `a[b + k - 1] <= key < a[b + k]`
     */
    // tg: code/__HELPERS/sorts/sort_instance.dm:320
    public virtual object gallopRight(object key = null, object @base = null, object len = null, object hint = null)
    {
        //ASSERT(len > 0 && hint >= 0 && hint < len)

        DmList L = this.L;
        object offset = 1;
        object lastOffset = 0;
        if (Dm.lt(Waiting.NoSleep(Dm.Call(null, cmp, key, (Dm.truthy(associative) ? L[L[Dm.add(@base, hint)]] : L[Dm.add(@base, hint)]))), 0)) //key <= L[base+hint]
        {
            object maxOffset = Dm.add(hint, 1); //therefore we want to insert somewhere in the range [base,base+hint] = [base+,base+(hint+1))
            while (Dm.lt(offset, maxOffset) && Dm.lt(Waiting.NoSleep(Dm.Call(null, cmp, key, (Dm.truthy(associative) ? L[L[Dm.sub(Dm.add(@base, hint), offset)]] : L[Dm.sub(Dm.add(@base, hint), offset)]))), 0)) //we are iterating backwards
            {
                lastOffset = offset;
                offset = Dm.shl(offset, 1) + 1; //1 3 7 15
            }

            if (Dm.gt(offset, maxOffset))
                offset = maxOffset;

            object temp = lastOffset;
            lastOffset = Dm.sub(hint, offset);
            offset = Dm.sub(hint, temp);
        }
        else //key > L[base+hint]
        {
            object maxOffset = Dm.sub(len, hint); //therefore we want to insert somewhere in the range (base+hint,base+len) = [base+hint+1, base+hint+(len-hint))
            while (Dm.lt(offset, maxOffset) && Dm.ge(Waiting.NoSleep(Dm.Call(null, cmp, key, (Dm.truthy(associative) ? L[L[Dm.add(Dm.add(@base, hint), offset)]] : L[Dm.add(Dm.add(@base, hint), offset)]))), 0))
            {
                lastOffset = offset;
                offset = Dm.shl(offset, 1) + 1;
            }

            if (Dm.gt(offset, maxOffset))
                offset = maxOffset;

            Dm.append(ref lastOffset, hint);
            Dm.append(ref offset, hint);
        }

        //ASSERT(-1 <= lastOffset && lastOffset < offset && offset <= len)

        Dm.inc(ref lastOffset);
        while (Dm.lt(lastOffset, offset))
        {
            object m = Dm.add(lastOffset, Dm.shr(Dm.sub(offset, lastOffset), 1));

            if (Dm.lt(Waiting.NoSleep(Dm.Call(null, cmp, key, (Dm.truthy(associative) ? L[L[Dm.add(@base, m)]] : L[Dm.add(@base, m)]))), 0)) //key <= L[base+m]
                offset = m;
            else //key > L[base+m]
                lastOffset = Dm.add(m, 1);
        }

        //ASSERT(lastOffset == offset)

        return offset;
    }

    //Merges two adjacent runs in-place in a stable fashion.
    //For performance this method should only be called when len1 <= len2!
    // tg: code/__HELPERS/sorts/sort_instance.dm:369
    public virtual void mergeLo(object base1 = null, object len1 = null, object base2 = null, object len2 = null)
    {
        //ASSERT(len1 > 0 && len2 > 0 && base1 + len1 == base2)

        DmList L = this.L;
        object cursor1 = base1;
        object cursor2 = base2;

        //degenerate cases
        if (Dm.eq(len2, 1))
        {
            move_element(L, cursor2, cursor1);
            return;
        }

        if (Dm.eq(len1, 1))
        {
            move_element(L, cursor1, Dm.add(cursor2, len2));
            return;
        }

        //Move first element of second run
        move_element(L, Dm.postinc(ref cursor2), Dm.postinc(ref cursor1));
        Dm.dec(ref len2);

        // tg: outer:
        while (true)
        {
            object count1 = 0; //# of times in a row that first run won
            object count2 = 0; // " " " " " "  second run won

            //do the straightforward thin until one run starts winning consistently

            do
            {
                //ASSERT(len1 > 1 && len2 > 0)
                if (Dm.lt(Waiting.NoSleep(Dm.Call(null, cmp, (Dm.truthy(associative) ? L[L[cursor2]] : L[cursor2]), (Dm.truthy(associative) ? L[L[cursor1]] : L[cursor1]))), 0))
                {
                    move_element(L, Dm.postinc(ref cursor2), Dm.postinc(ref cursor1));
                    Dm.dec(ref len2);

                    Dm.inc(ref count2);
                    count1 = 0;

                    if (Dm.eq(len2, 0))
                        goto outer__break;
                }
                else
                {
                    Dm.inc(ref cursor1);

                    Dm.inc(ref count1);
                    count2 = 0;

                    if (Dm.predec(ref len1) == 1)
                        goto outer__break;
                }
            }
            while (Dm.lt(Dm.bitor(count1, count2), minGallop));

            //one run is winning consistently so galloping may provide huge benefits
            //so try galloping, until such time as the run is no longer consistently winning
            do
            {
                //ASSERT(len1 > 1 && len2 > 0)

                count1 = gallopRight((Dm.truthy(associative) ? L[L[cursor2]] : L[cursor2]), cursor1, len1, 0);
                if (Dm.truthy(count1))
                {
                    Dm.append(ref cursor1, count1);
                    Dm.remove(ref len1, count1);

                    if (Dm.le(len1, 1))
                        goto outer__break;
                }

                move_element(L, cursor2, cursor1);
                Dm.inc(ref cursor2);
                Dm.inc(ref cursor1);
                if (Dm.predec(ref len2) == 0)
                    goto outer__break;

                count2 = gallopLeft((Dm.truthy(associative) ? L[L[cursor1]] : L[cursor1]), cursor2, len2, 0);
                if (Dm.truthy(count2))
                {
                    move_range(L, cursor2, cursor1, Dm.num_or_null(count2));

                    Dm.append(ref cursor2, count2);
                    Dm.append(ref cursor1, count2);
                    Dm.remove(ref len2, count2);

                    if (Dm.eq(len2, 0))
                        goto outer__break;
                }

                Dm.inc(ref cursor1);
                if (Dm.predec(ref len1) == 1)
                    goto outer__break;

                --minGallop;
            }
            while (Dm.gt(Dm.bitor(count1, count2), MIN_GALLOP));

            if (minGallop < 0)
                minGallop = 0;
            minGallop += 2; // Penalize for leaving gallop mode
        }
    outer__break:

        if (Dm.eq(len1, 1))
        {
            //ASSERT(len2 > 0)
            move_element(L, cursor1, Dm.add(cursor2, len2));
        }

        //else
        //ASSERT(len2 == 0)
        //ASSERT(len1 > 1)
    }

    // tg: code/__HELPERS/sorts/sort_instance.dm:472
    public virtual void mergeHi(object base1 = null, object len1 = null, object base2 = null, object len2 = null)
    {
        //ASSERT(len1 > 0 && len2 > 0 && base1 + len1 == base2)

        DmList L = this.L;
        object cursor1 = Dm.sub(Dm.add(base1, len1), 1); //start at end of sublists
        object cursor2 = Dm.sub(Dm.add(base2, len2), 1);

        //degenerate cases
        if (Dm.eq(len2, 1))
        {
            move_element(L, base2, base1);
            return;
        }

        if (Dm.eq(len1, 1))
        {
            move_element(L, base1, Dm.add(cursor2, 1));
            return;
        }

        move_element(L, Dm.postdec(ref cursor1), Dm.postdec(ref cursor2) + 1);
        Dm.dec(ref len1);

        // tg: outer:
        while (true)
        {
            object count1 = 0; //# of times in a row that first run won
            object count2 = 0; // " " " " " "  second run won

            //do the straightforward thing until one run starts winning consistently
            do
            {
                //ASSERT(len1 > 0 && len2 > 1)
                if (Dm.lt(Waiting.NoSleep(Dm.Call(null, cmp, (Dm.truthy(associative) ? L[L[cursor2]] : L[cursor2]), (Dm.truthy(associative) ? L[L[cursor1]] : L[cursor1]))), 0))
                {
                    move_element(L, Dm.postdec(ref cursor1), Dm.postdec(ref cursor2) + 1);
                    Dm.dec(ref len1);

                    Dm.inc(ref count1);
                    count2 = 0;

                    if (Dm.eq(len1, 0))
                        goto outer__break;
                }
                else
                {
                    Dm.dec(ref cursor2);
                    Dm.dec(ref len2);

                    Dm.inc(ref count2);
                    count1 = 0;

                    if (Dm.eq(len2, 1))
                        goto outer__break;
                }
            }
            while (Dm.lt(Dm.bitor(count1, count2), minGallop));

            //one run is winning consistently so galloping may provide huge benefits
            //so try galloping, until such time as the run is no longer consistently winning
            do
            {
                //ASSERT(len1 > 0 && len2 > 1)

                count1 = Dm.sub(len1, gallopRight((Dm.truthy(associative) ? L[L[cursor2]] : L[cursor2]), base1, len1, Dm.sub(len1, 1))); //should cursor1 be base1?
                if (Dm.truthy(count1))
                {
                    Dm.remove(ref cursor1, count1);

                    move_range(L, Dm.add(cursor1, 1), Dm.add(cursor2, 1), Dm.num_or_null(count1)); //cursor1+1 == cursor2 by definition

                    Dm.remove(ref cursor2, count1);
                    Dm.remove(ref len1, count1);

                    if (Dm.eq(len1, 0))
                        goto outer__break;
                }

                Dm.dec(ref cursor2);

                if (Dm.predec(ref len2) == 1)
                    goto outer__break;

                count2 = Dm.sub(len2, gallopLeft((Dm.truthy(associative) ? L[L[cursor1]] : L[cursor1]), Dm.add(cursor1, 1), len2, Dm.sub(len2, 1)));
                if (Dm.truthy(count2))
                {
                    Dm.remove(ref cursor2, count2);
                    Dm.remove(ref len2, count2);

                    if (Dm.le(len2, 1))
                        goto outer__break;
                }

                move_element(L, Dm.postdec(ref cursor1), Dm.postdec(ref cursor2) + 1);
                Dm.dec(ref len1);

                if (Dm.eq(len1, 0))
                    goto outer__break;

                --minGallop;
            }
            while (Dm.gt(Dm.bitor(count1, count2), MIN_GALLOP));

            if (minGallop < 0)
                minGallop = 0;
            minGallop += 2; // Penalize for leaving gallop mode
        }
    outer__break:

        if (Dm.eq(len2, 1))
        {
            //ASSERT(len1 > 0)

            Dm.remove(ref cursor1, len1);
            move_range(L, Dm.add(cursor1, 1), Dm.add(cursor2, 1), Dm.num_or_null(len1));
        }

        //else
        //ASSERT(len1 == 0)
        //ASSERT(len2 > 0)
    }

    // tg: code/__HELPERS/sorts/sort_instance.dm:573
    public virtual object mergeSort(object start = null, object end = null)
    {
        object remaining = Dm.sub(end, start);

        //If array is small, do an insertion sort
        if (Dm.lt(remaining, MIN_MERGE))
        {
            binarySort(start, end, start/*+initRunLen*/);
            return null;
        }

        object minRun = minRunLength(remaining);

        do
        {
            object runLen = (Dm.le(remaining, minRun) ? remaining : minRun);

            binarySort(start, Dm.add(start, runLen), start);

            //add data about run to queue
            runBases.Add(start);
            runLens.Add(runLen);

            //Advance to find next run
            Dm.append(ref start, runLen);
            Dm.remove(ref remaining, runLen);
        }
        while (Dm.gt(remaining, 0));

        while (runBases.len >= 2)
        {
            float n = runBases.len - 1;
            if (n > 1 && Dm.le(runLens[n - 1], Dm.add(runLens[n], runLens[n + 1])))
            {
                if (Dm.lt(runLens[n - 1], runLens[n + 1]))
                    --n;
                mergeAt2(n);
            }
            else if (Dm.le(runLens[n], runLens[n + 1]))
            {
                mergeAt2(n);
            }
            else
            {
                break; //Invariant is established
            }
        }

        while (runBases.len >= 2)
        {
            float n = runBases.len - 1;
            if (n > 1 && Dm.lt(runLens[n - 1], runLens[n + 1]))
                --n;
            mergeAt2(n);
        }

        return L;
    }

    // tg: code/__HELPERS/sorts/sort_instance.dm:617
    public virtual void mergeAt2(object i = null)
    {
        DmList L = this.L;
        object cursor1 = runBases[i];
        object cursor2 = runBases[Dm.add(i, 1)];

        object end1 = Dm.add(cursor1, runLens[i]);
        object end2 = Dm.add(cursor2, runLens[Dm.add(i, 1)]);

        object val1 = (Dm.truthy(associative) ? L[L[cursor1]] : L[cursor1]);
        object val2 = (Dm.truthy(associative) ? L[L[cursor2]] : L[cursor2]);

        while (true)
        {
            if (Dm.le(Waiting.NoSleep(Dm.Call(null, cmp, val1, val2)), 0))
            {
                if (Dm.ge(Dm.preinc(ref cursor1), end1))
                    break;
                val1 = (Dm.truthy(associative) ? L[L[cursor1]] : L[cursor1]);
            }
            else
            {
                move_element(L, cursor2, cursor1);

                if (Dm.ge(Dm.preinc(ref cursor2), end2))
                    break;
                Dm.inc(ref end1);
                Dm.inc(ref cursor1);

                val2 = (Dm.truthy(associative) ? L[L[cursor2]] : L[cursor2]);
            }
        }

        //Record the legth of the combined runs. If i is the 3rd last run now, also slide over the last run
        //(which isn't involved in this merge). The current run (i+1) goes away in any case.
        Dm.append_at(runLens, i, runLens[Dm.add(i, 1)]);
        runLens.Cut(Dm.num(Dm.add(i, 1)), Dm.num(Dm.add(i, 2)));
        runBases.Cut(Dm.num(Dm.add(i, 1)), Dm.num(Dm.add(i, 2)));
    }
}
