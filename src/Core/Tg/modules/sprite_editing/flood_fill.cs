// Translated from tgstation (snapshot 1d99bd7f): code/modules/sprite_editing/flood_fill.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI4d (D326): the whole file, its file-local macros inline.

public static partial class GlobalProcs
{
    // tg: code/modules/sprite_editing/flood_fill.dm:11
    public static DmList flood_fill(DmList grid = null, object x = null, object y = null, object width = null, object height = null)
    {
        float w = Dm.num(width), h = Dm.num(height);
        object target_color = ((DmList)grid[y])[x];
        DmList coord_cache = Dm.list();
        DmList points = Dm.list();
        // #define CANVAS_COORD(x, y) "[x]:[y]"; IS_IN_BOUNDS; COLORS_ARE_EQUAL(a, b) ((a == b) || (endswith(a, "00") && endswith(b, "00")))
        bool should_add_point(float px, float py)
        {
            if (Dm.truthy(coord_cache[Dm.text("[]:[]", px, py)]) || !(px > 0 && px <= w && py > 0 && py <= h))
                return false;
            object color = ((DmList)grid[py])[px];
            return Dm.eq(color, target_color) || (Dm.truthy(endswith(color, "00")) && Dm.truthy(endswith(target_color, "00")));
        }
        void add_point(float px, float py)
        {
            points.Add(Dm.list(Dm.list(px - 1, py - 1, target_color)));
            coord_cache[Dm.text("[]:[]", px, py)] = TRUE;
        }
        float x0 = Dm.num(x), y0 = Dm.num(y);
        DmList coord_queue = Dm.list(x0, x0, y0, 1, x0, x0, y0 - 1, -1);
        while (Dm.length(coord_queue) > 0)
        {
            float span_start = Dm.num(coord_queue[1]);
            float column = span_start;
            float span_end = Dm.num(coord_queue[2]);
            float row = Dm.num(coord_queue[3]);
            float row_shift = Dm.num(coord_queue[4]);
            coord_queue.Cut(1, 5);
            if (should_add_point(column, row))
            {
                while (should_add_point(column - 1, row))
                {
                    add_point(column - 1, row);
                    column--;
                }
                if (column < span_start)
                    coord_queue.Add(column, span_start - 1, row - row_shift, -row_shift);
            }
            while (span_start <= span_end)
            {
                while (should_add_point(span_start, row))
                {
                    add_point(span_start, row);
                    span_start++;
                }
                if (span_start > column)
                    coord_queue.Add(column, span_start - 1, row + row_shift, row_shift);
                if (span_start - 1 > span_end)
                    coord_queue.Add(span_end + 1, span_start - 1, row - row_shift, -row_shift);
                span_start++;
                while (span_start < span_end && !should_add_point(span_start, row))
                    span_start++;
                column = span_start;
            }
        }
        return points;
    }
}
