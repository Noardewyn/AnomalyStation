using Anomaly.Client.Shared.World;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>
/// The M7 visual check: a cascade's crystals refilled chunk MultiMeshes every frame, a new MultiMesh each time, until Godot ran out of
/// their RIDs. The chunk batches and the chunk tiles now refill their MultiMeshes in place with one buffer each; its layout is Godot's
/// (MultiMesh.buffer for Transform3D with custom data, with or without colours).
/// </summary>
public class MultiMeshBufferTests
{
    [Fact]
    public void AnInstanceIsWrittenAsGodotsBufferLaysItOut()
    {
        // basis rows (1 2 3 / 4 5 6 / 7 8 9), origin (10 11 12), colour (13 .. 16), custom (17 .. 20)
        float[] values = Enumerable.Range(1, 20).Select(i => (float)i).ToArray();
        var buffer = new float[3 * MultiMeshBuffer.Stride];
        MultiMeshBuffer.Write(buffer, 1, values);
        Assert.All(buffer.Take(MultiMeshBuffer.Stride).Concat(buffer.Skip(2 * MultiMeshBuffer.Stride)), f => Assert.Equal(0f, f));
        Assert.Equal(new float[] { 1, 2, 3, 10, 4, 5, 6, 11, 7, 8, 9, 12, 13, 14, 15, 16, 17, 18, 19, 20 },
            buffer.Skip(MultiMeshBuffer.Stride).Take(MultiMeshBuffer.Stride));
    }

    [Fact]
    public void ATileWithoutColourIsWrittenWithTheShorterStride()
    {
        // a chunk's tiles (no colours): basis rows, origin, then the custom data (layer, part, side) right after the transform
        float[] values = { 1, 0, 0, 0, 1, 0, 0, 0, 1, 3, 0, -5, 7, 2, 1, 0 };
        var buffer = new float[2 * MultiMeshBuffer.StrideNoColor];
        MultiMeshBuffer.Write(buffer, 1, values);
        Assert.All(buffer.Take(MultiMeshBuffer.StrideNoColor), f => Assert.Equal(0f, f));
        Assert.Equal(new float[] { 1, 0, 0, 3, 0, 1, 0, 0, 0, 0, 1, -5, 7, 2, 1, 0 }, buffer.Skip(MultiMeshBuffer.StrideNoColor));
    }

    [Fact]
    public void AFullChunkOfCrystalsIsOneBuffer()
    {
        // a 16 x 16 chunk with a crystal on every tile: 256 instances, each moved to its tile, all in one buffer
        const int count = 16 * 16;
        var buffer = new float[count * MultiMeshBuffer.Stride];
        for (int i = 0; i < count; i++)
            MultiMeshBuffer.Write(buffer, i, new float[] { 1, 0, 0, 0, 1, 0, 0, 0, 1, i % 16, 0, i / 16, 1, 1, 1, 1, i, 0, 0, 0 });
        Assert.Equal(5120, buffer.Length);
        Assert.Equal((15f, 15f, 255f), (buffer[255 * 20 + 3], buffer[255 * 20 + 11], buffer[255 * 20 + 16]));
        Assert.Equal((1f, 1f, 1f), (buffer[100 * 20 + 0], buffer[100 * 20 + 5], buffer[100 * 20 + 10]));
    }
}
