# Photos as client snapshots (gaps MT3; the owner, 2026-11-26; DV685)

tg's camera (`modules/photography/camera/camera.dm`) builds a photo's picture on the server with `camera_get_icon`, a top-down
composite of the turfs and atoms it captured. The port's world is drawn in 3D by the clients, so such a composite would not show what
the player saw. The owner chose (2026-11-26, option 2 of three): the photographer's client renders the shot in 3D and uploads it; the
server stores it and sends it to the clients that view the photo. tg's camera mechanics stay on the server: the range and view checks,
what is captured (the turfs, the mobs spotted, the dead, the names, the blueprints, the revenants revealed), the description, the
picture's size, the photo item and its data (`/datum/picture`).

## 1. The flow

1. `capture_image` runs tg's code; in place of `camera_get_icon` the picture is made with no image, and once it exists
   `PhotoSeam.Request` gives it a request id (per world, increasing) and writes a `PhotoRequest` into the photographer's
   `client.dm_photo_requests` (presentation output, not world state). No client (a camera with no one behind it): no request, a
   blank picture.
2. After the tick the host sends each request to its client (protocol version 99, `PhotoRequest`, State channel).
3. The client renders one frame into an offscreen viewport of the asked size (`godot/scripts/Render/PhotoCamera.cs`): it shares the
   world and takes the player camera's layers, environment and attributes, so it draws what the player's camera draws and no HUD. It
   reads the frame back as a PNG and sends it in `PhotoPart`s.
4. The host reassembles the parts and hands the PNG to `PhotoSeam.Develop` (3), which keeps it as a runtime resource (`rsc/MD5.png`,
   silicons SI4d's cache, D326) and sets `picture_image` to an icon of it; `regenerate_small_icon` puts tg's small picture on the
   photo, and the photos of that picture and their frames redraw. Every client is sent the resource once, as a `RuntimeResource`,
   before the look that names it.
5. `show` is tg's: `browse_rsc(picture_image, "tmp_photo.png")` and the `photo_showing` page. The port's `browse_rsc` maps the name to
   the image's resource for that client, and the client's Html node draws the `<img>` (`HtmlImages`).

The photo item and its data exist at once, as in tg; only the image comes a moment later. A picture whose image does not come (no
answer within the deadline, or a refused one) stays blank: `show` says it "seems to be blank".

## 2. The pose and the frame

The client computes the pose (`PhotoPose`), from where its camera is: the server knows the photographer's tile, the client its exact
eye.

- The direction: from the eye to the target tile's middle at half the wall height. A shot of the photographer's own tile looks along
  the view, 30 degrees down.
- The field of view (horizontal): wide enough that the frame's width (tg's `APERTURE_TO_METERS(picture_size_x)`, 1 to 7 tiles) fits
  at the target's distance, from 30 to 90 degrees.
- The pixel size: 64 pixels a tile of the frame (tg's 32, doubled for the 3D view), at most 448 by 448.

## 3. Limits and trust

- An image answers a request the server sent that client, by id, within 10 seconds (`PhotoSeam.DeadlineTicks`), once. Any other is
  refused.
- At most 512 KiB (`Limits.MaxPhotoBytes`), in parts of 4 KiB sent in order (a part out of order is a strike).
- The server decodes it with its own PNG decoder (`DmPng`): an image it cannot read is refused, and its size must be 16 pixels to the
  requested size on each side (a client shrinks a PNG over the cap by a quarter until it fits).
- The image's content cannot be checked: a modified client can upload any picture of the right size. tg's `author_ckey` stays on the
  picture, and a refused upload is logged with the key, the id and the byte count.
- The images live in the world's resource cache for the round (keeping photos between rounds is owner-deferred, D332).

## 4. What is left

`not-translated.md` section 257: `camera_get_icon` itself (no caller), the persistence and the pictures' log to disk (D332), the
booth's screentips (D264), and a shot taken in the orbit view or through a camera console is rendered from where that client's camera
is.
