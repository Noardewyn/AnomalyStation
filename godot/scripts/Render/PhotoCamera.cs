using Godot;
using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Client.Render;

/// <summary>
/// DV685 (protocol version 99): the photographer's side of tg's camera. For each picture the server asks for, one frame of the game's world
/// from the own eye (<see cref="PhotoPose"/>) into an offscreen viewport of the asked size, which shares the world and draws what the player's
/// camera draws (its layers, environment and attributes: no HUD, which is 2D), read back as a PNG and sent. A PNG over the cap is shrunk
/// until it fits.
/// </summary>
public partial class PhotoCamera : Node
{
    /// <summary>Renders <paramref name="request"/> from <paramref name="eye"/> and hands the PNG to <paramref name="send"/> (nothing when it cannot be made).</summary>
    public async void Take(PhotoRequestMessage request, Camera3D eye, Action<uint, byte[]> send)
    {
        if (eye == null || !IsInsideTree())
            return;
        Vector3 at = eye.GlobalPosition;
        (float yaw, float pitch, float fov) = PhotoPose.Of(at.X, at.Y, at.Z, eye.GlobalRotation.Y, request.X, request.Y, request.Z, request.TilesX);
        var viewport = new SubViewport
        {
            Size = new Vector2I(request.Width, request.Height),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            HandleInputLocally = false,
            Msaa3D = eye.GetViewport().Msaa3D,
        };
        var camera = new Camera3D
        {
            Current = true,
            KeepAspect = Camera3D.KeepAspectEnum.Width,
            Fov = fov,
            Near = eye.Near,
            Far = eye.Far,
            CullMask = eye.CullMask,
            Environment = eye.Environment,
            Attributes = eye.Attributes,
            Position = at,
            Rotation = new Vector3(pitch, yaw, 0f),
        };
        viewport.AddChild(camera);
        AddChild(viewport);
        // two drawn frames: the first after the viewport is made, the second with its picture in the texture
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Image image = viewport.GetTexture()?.GetImage();
        viewport.QueueFree();
        if (image == null || image.IsEmpty())
            return;
        image.Convert(Image.Format.Rgb8);
        byte[] png = image.SavePngToBuffer();
        while (png.Length > Limits.MaxPhotoBytes && PhotoPose.Shrunk(image.GetWidth(), image.GetHeight()) is (int w, int h))
        {
            image.Resize(w, h, Image.Interpolation.Bilinear);
            png = image.SavePngToBuffer();
        }
        if (png.Length is > 0 and <= Limits.MaxPhotoBytes)
            send(request.Id, png);
    }
}
