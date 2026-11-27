using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Server;

// Protocol version 99 (DV685): photos. tg's camera asks the photographer's client for the shot (PhotoSeam writes
// client.dm_photo_requests); after the tick the host sends each as a PhotoRequest. The client answers with the PNG in PhotoPart messages,
// in order, as a bug report's parts come; the whole image goes to the world (PhotoSeam.Develop), which checks it, keeps it as a runtime
// resource (every client is sent it once, version 79) and makes it the picture's image. A refused image is logged and leaves the picture
// blank; a part that breaks the order, or an image that answers no request of the client (a late answer to its own expired one aside), is
// a strike.
public sealed partial class ServerHost
{
    private void SendPhotoRequests(Session s)
    {
        if (s.Client == null || s.Client.dm_photo_requests.Count == 0)
            return;
        foreach (PhotoRequest request in s.Client.dm_photo_requests)
        {
            Send(s, Channel.State, Codec.Encode(new PhotoRequestMessage(request.Id, request.X, request.Y, request.Z,
                (byte)request.TilesX, (byte)request.TilesY, (ushort)request.Width, (ushort)request.Height)));
            s.PhotoRequestsSent++;
        }
        s.Client.dm_photo_requests.Clear();
    }

    private void ReceivePhotoPart(Session s, ReadOnlySpan<byte> message)
    {
        if (s.State != SessionState.Joined) { Strike(s, "a command before Welcome"); return; }
        if (!Codec.TryDecode(message, out PhotoPart part, out ProtocolError error)) { Strike(s, "a malformed PhotoPart (" + error + ")"); return; }
        if (part.Offset == 0)
        {
            s.PhotoBody = new System.IO.MemoryStream();
            s.PhotoId = part.Id;
            s.PhotoTotal = part.Total;
        }
        if (s.PhotoBody == null || part.Id != s.PhotoId || part.Total != s.PhotoTotal || part.Offset != s.PhotoBody.Length) { Strike(s, "a PhotoPart out of order"); return; }
        s.PhotoBody.Write(part.Bytes);
        if (s.PhotoBody.Length < s.PhotoTotal)
            return;
        byte[] png = s.PhotoBody.ToArray();
        s.PhotoBody = null;
        using (World.Enter())
        {
            if (s.Client == null)
                return;
            string refused = PhotoSeam.Develop(s.Client, part.Id, png);
            if (refused == null)
                s.PhotosTaken++;
            else
            {
                s.PhotosRefused++;
                // an image for no request of this client is its violation (an honest client answers each request it was sent once): a
                // strike, so a flood of them ends in a kick; a late answer to its own expired request is not. Both are logged once a
                // session, so neither floods the log.
                bool noRequest = refused == PhotoSeam.NoRequest;
                if (!(noRequest || refused == PhotoSeam.Late) || s.PhotoRefusalsLogged.Add(refused))
                    _err.WriteLine("warning: the photo " + part.Id + " of " + s.Ckey + " (" + png.Length + " bytes) was refused: " + refused);
                if (noRequest)
                    Strike(s, "a photo that answers no request");
            }
        }
    }
}
