namespace Anomaly.Protocol;

// Protocol version 9 (M4v slice 1, D143): the entity's animate() chains (docs/ai/specs/protocol.md section 18).
public static partial class Codec
{
    // [u8 tracks 0..4] { [u8 track flags] [varuint ticks since start] [varuint loop] [vars start] [u8 steps 1..36 (version 32; 16 before)] { [step] } }
    // [step] = [f32 time] [f32 delay] [u8 easing] [u8 step flags] [vars]; [vars] = [u8 mask] then the set vars in mask order.
    // 0 tracks is "no animation" (a change that stops or ends one); an entity record never carries it. Version 26: a track flag bit 1,
    // a filter's track: [u8 filter position 1..16] after the flags, and [filter vars] in the place of every [vars] (section 35.2).
    // A track flag bit 2: a screen entry's child's, [varuint appearance (index << 1 | private)] after the position (version 74: also a
    // track of the child's own vars, a world entity's child's too). Version 74: a vars mask bit 128, maptext_x, after the transform.
    private const byte TrackParallel = 1, TrackFilter = 2, TrackChild = 4;

    private static void WriteAnimation(ref WireWriter w, EntityAnimation animation, long tick)
    {
        if (animation == null)
        {
            w.WriteByte(0);
            return;
        }
        AnimationTrack[] tracks = animation.Tracks;
        if (tracks == null || tracks.Length == 0 || tracks.Length > Limits.MaxAnimationTracks)
            throw Bad("an animation of " + (tracks?.Length ?? 0) + " tracks (1 to " + Limits.MaxAnimationTracks + ")");
        w.WriteByte((byte)tracks.Length);
        foreach (AnimationTrack t in tracks)
        {
            if (t == null)
                throw Bad("an animation track that is null");
            if (t.Filter > Limits.MaxAppearanceFilters)
                throw Bad("an animation of filter " + t.Filter + " (1 to " + Limits.MaxAppearanceFilters + ")");
            if (!t.Child.IsNone && t.Child.Index > (uint)Limits.MaxAppearanceTableEntries)
                throw Bad("an animation of a child that is outside its table");
            w.WriteByte((byte)((t.Parallel ? TrackParallel : 0) | (t.Filter != 0 ? TrackFilter : 0) | (!t.Child.IsNone ? TrackChild : 0)));
            if (t.Filter != 0)
                w.WriteByte(t.Filter);
            if (!t.Child.IsNone)
                w.WriteVarUInt(((ulong)t.Child.Index << 1) | (t.Child.Private ? 1UL : 0UL));
            if (t.StartTick < 0 || t.StartTick > tick)
                throw Bad("an animation's start tick " + t.StartTick + " is outside 0 to the message's tick " + tick);
            w.WriteVarUInt((ulong)(tick - t.StartTick));
            if (t.Loop < 0 || t.Loop > Limits.MaxAnimationLoop)
                throw Bad("an animation loop of " + t.Loop + " (0 for ever, or 1 to " + Limits.MaxAnimationLoop + ")");
            w.WriteVarUInt((ulong)t.Loop);
            if (t.Filter != 0) WriteFilterVars(ref w, t.FilterStart);
            else WriteVars(ref w, t.Start);
            AnimationStep[] steps = t.Steps;
            if (steps == null || steps.Length == 0 || steps.Length > Limits.MaxAnimationSteps)
                throw Bad("an animation track of " + (steps?.Length ?? 0) + " steps (1 to " + Limits.MaxAnimationSteps + ")");
            w.WriteByte((byte)steps.Length);
            foreach (AnimationStep step in steps)
            {
                CheckTime(step.Time, "time");
                CheckTime(step.Delay, "delay");
                if (!AnimationEasing.IsValid(step.Easing))
                    throw Bad("an animation easing " + step.Easing + " BYOND does not define");
                if ((step.Flags & ~AnimationStepFlags.All) != 0)
                    throw Bad("an animation step sets flag bits that are not defined");
                w.WriteSingle(step.Time);
                w.WriteSingle(step.Delay);
                w.WriteByte(step.Easing);
                w.WriteByte((byte)step.Flags);
                if (t.Filter != 0) WriteFilterVars(ref w, step.Filter);
                else WriteVars(ref w, step.Vars);
            }
        }
    }

    private static EntityAnimation ReadAnimation(ref WireReader r, long tick)
    {
        int count = r.ReadByte();
        if (count == 0)
            return null;
        if (count > Limits.MaxAnimationTracks)
            throw Bad("an animation of " + count + " tracks (at most " + Limits.MaxAnimationTracks + ")");
        var tracks = new AnimationTrack[count];
        for (int i = 0; i < count; i++)
        {
            byte flags = r.ReadByte();
            if ((flags & ~(TrackParallel | TrackFilter | TrackChild)) != 0)
                throw Bad("an animation track sets flag bits that are not defined");
            byte filter = 0;
            if ((flags & TrackFilter) != 0)
            {
                filter = r.ReadByte();
                if (filter == 0 || filter > Limits.MaxAppearanceFilters)
                    throw Bad("an animation of filter " + filter + " (1 to " + Limits.MaxAppearanceFilters + ")");
            }
            TableRef child = TableRef.None;
            if ((flags & TrackChild) != 0)
            {
                child = ReadPlainRef(ref r, true, Limits.MaxAppearanceTableEntries, "an animation's child");
                if (child.IsNone)
                    throw Bad("an animation of a child without an appearance");
            }
            ulong ago = r.ReadVarUInt();
            if (ago > (ulong)tick)
                throw Bad("an animation starts before tick 0");
            ulong loop = r.ReadVarUInt();
            if (loop > Limits.MaxAnimationLoop)
                throw Bad("an animation loop of " + loop + " (at most " + Limits.MaxAnimationLoop + ")");
            AnimatedVars start = filter == 0 ? ReadVars(ref r) : default;
            FilterVars filterStart = filter != 0 ? ReadFilterVars(ref r) : default;
            int stepCount = r.ReadByte();
            if (stepCount == 0 || stepCount > Limits.MaxAnimationSteps)
                throw Bad("an animation track of " + stepCount + " steps (1 to " + Limits.MaxAnimationSteps + ")");
            var steps = new AnimationStep[stepCount];
            for (int k = 0; k < stepCount; k++)
            {
                float time = r.ReadSingle();
                CheckTime(time, "time");
                float delay = r.ReadSingle();
                CheckTime(delay, "delay");
                byte easing = r.ReadByte();
                if (!AnimationEasing.IsValid(easing))
                    throw Bad("an animation easing " + easing + " BYOND does not define");
                var stepFlags = (AnimationStepFlags)r.ReadByte();
                if ((stepFlags & ~AnimationStepFlags.All) != 0)
                    throw Bad("an animation step sets flag bits that are not defined");
                steps[k] = filter == 0
                    ? new AnimationStep(time, delay, easing, stepFlags, ReadVars(ref r))
                    : new AnimationStep(time, delay, easing, stepFlags, default, ReadFilterVars(ref r));
            }
            tracks[i] = new AnimationTrack
            {
                StartTick = tick - (long)ago,
                Loop = (int)loop,
                Parallel = (flags & TrackParallel) != 0,
                Start = start,
                Steps = steps,
                Filter = filter,
                FilterStart = filterStart,
                Child = child,
            };
        }
        return new EntityAnimation { Tracks = tracks };
    }

    private static void WriteVars(ref WireWriter w, in AnimatedVars v)
    {
        if ((v.Mask & ~AnimatedVar.All) != 0)
            throw Bad("animated vars set mask bits that are not defined");
        w.WriteByte((byte)v.Mask);
        if (v.Has(AnimatedVar.PixelX)) WriteValue(ref w, v.PixelX);
        if (v.Has(AnimatedVar.PixelY)) WriteValue(ref w, v.PixelY);
        if (v.Has(AnimatedVar.PixelZ)) WriteValue(ref w, v.PixelZ);
        if (v.Has(AnimatedVar.PixelW)) WriteValue(ref w, v.PixelW);
        if (v.Has(AnimatedVar.Alpha)) WriteValue(ref w, v.Alpha);
        if (v.Has(AnimatedVar.Color)) w.WriteUInt32(v.Rgba);
        if (v.Has(AnimatedVar.Transform))
        {
            WriteValue(ref w, v.A);
            WriteValue(ref w, v.B);
            WriteValue(ref w, v.C);
            WriteValue(ref w, v.D);
            WriteValue(ref w, v.E);
            WriteValue(ref w, v.F);
        }
        if (v.Has(AnimatedVar.MaptextX)) WriteValue(ref w, v.MaptextX);
    }

    private static AnimatedVars ReadVars(ref WireReader r)
    {
        var v = new AnimatedVars { Mask = (AnimatedVar)r.ReadByte() };
        if ((v.Mask & ~AnimatedVar.All) != 0)
            throw Bad("animated vars set mask bits that are not defined");
        if (v.Has(AnimatedVar.PixelX)) v.PixelX = ReadValue(ref r);
        if (v.Has(AnimatedVar.PixelY)) v.PixelY = ReadValue(ref r);
        if (v.Has(AnimatedVar.PixelZ)) v.PixelZ = ReadValue(ref r);
        if (v.Has(AnimatedVar.PixelW)) v.PixelW = ReadValue(ref r);
        if (v.Has(AnimatedVar.Alpha)) v.Alpha = ReadValue(ref r);
        if (v.Has(AnimatedVar.Color)) v.Rgba = r.ReadUInt32();
        if (v.Has(AnimatedVar.Transform))
        {
            v.A = ReadValue(ref r);
            v.B = ReadValue(ref r);
            v.C = ReadValue(ref r);
            v.D = ReadValue(ref r);
            v.E = ReadValue(ref r);
            v.F = ReadValue(ref r);
        }
        if (v.Has(AnimatedVar.MaptextX)) v.MaptextX = ReadValue(ref r);
        return v;
    }

    // Version 26: [varuint mask] then the set numbers in FilterVar order as f32, the colour as a u32 0xRRGGBBAA, the matrix as 20 f32.
    private static void WriteFilterVars(ref WireWriter w, in FilterVars v)
    {
        if ((v.Mask & ~FilterVar.All) != 0)
            throw Bad("animated filter vars set mask bits that are not defined");
        w.WriteVarUInt((ulong)v.Mask);
        foreach (FilterVar bit in FilterVars.Numbers)
            if (v.Has(bit)) WriteValue(ref w, v.Get(bit));
        if (v.Has(FilterVar.Color)) w.WriteUInt32(v.Rgba);
        if (v.Has(FilterVar.ColorMatrix))
        {
            if (v.Matrix is not { Length: 20 })
                throw Bad("an animated colour matrix that has not 20 numbers");
            foreach (float n in v.Matrix)
                WriteValue(ref w, n);
        }
    }

    private static FilterVars ReadFilterVars(ref WireReader r)
    {
        ulong mask = r.ReadVarUInt();
        if ((mask & ~(ulong)FilterVar.All) != 0)
            throw Bad("animated filter vars set mask bits that are not defined");
        var v = new FilterVars();
        var bits = (FilterVar)mask;
        foreach (FilterVar bit in FilterVars.Numbers)
            if ((bits & bit) != 0) v.Set(bit, ReadValue(ref r));
        if ((bits & FilterVar.Color) != 0) v.Rgba = r.ReadUInt32();
        if ((bits & FilterVar.ColorMatrix) != 0)
        {
            v.Matrix = new float[20];
            for (int i = 0; i < 20; i++)
                v.Matrix[i] = ReadValue(ref r);
        }
        v.Mask = bits;
        return v;
    }

    private static void WriteValue(ref WireWriter w, float value)
    {
        CheckValue(value);
        w.WriteSingle(value);
    }

    private static float ReadValue(ref WireReader r)
    {
        float value = r.ReadSingle();
        CheckValue(value);
        return value;
    }

    private static void CheckValue(float value)
    {
        if (!float.IsFinite(value) || Math.Abs(value) > Limits.MaxAnimatedValue)
            throw Bad("an animated value " + value + " that is not finite or above " + Limits.MaxAnimatedValue);
    }

    private static void CheckTime(float value, string what)
    {
        if (!float.IsFinite(value) || value < 0 || value > Limits.MaxAnimationTime)
            throw Bad("an animation " + what + " of " + value + " (0 to " + Limits.MaxAnimationTime + " ds)");
    }

    // The most bytes an animation takes (the encoder's size estimate).
    private static int MaxAnimationBytes(EntityAnimation animation)
    {
        int size = 1;
        if (animation?.Tracks == null) return size;
        foreach (AnimationTrack t in animation.Tracks)
        {
            int vars = t?.Filter != 0 ? Limits.MaxAnimatedFilterVarsBytes : Limits.MaxAnimatedVarsBytes;
            size += 2 + 5 + 10 + 5 + vars + 1 + (t?.Steps?.Length ?? 0) * (4 + 4 + 1 + 1 + vars);
        }
        return size;
    }
}
