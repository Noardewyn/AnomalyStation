"""Build the runtime photo-slot quad using the frame GLB's unchanged original atlas."""
import json
import struct
from pathlib import Path

FOLDER = Path(__file__).resolve().parents[2] / 'godot/assets/models/structures/picture_frame'


def build():
    data = (FOLDER / 'picture_frame_photo_empty.glb').read_bytes()
    length = struct.unpack_from('<I', data, 12)[0]
    source = json.loads(data[20:20 + length])
    binary = data[28 + length:]
    view = source['bufferViews'][source['images'][0]['bufferView']]
    start = view.get('byteOffset', 0)
    atlas = binary[start:start + view['byteLength']]
    width, height = struct.unpack_from('>II', atlas, 16)
    # Original signs atlas: frame-overlay at (64,384), with an 8 x 8 pixel photo opening.
    half = 4 * .9 / 32
    positions = [-half, -half, .010, half, -half, .010, half, half, .010, -half, half, .010]
    uvs = [value for x, y in [(12, 20), (20, 20), (20, 12), (12, 12)]
           for value in [(64 + x) / width, (384 + y) / height]]
    chunks = [struct.pack('<12f', *positions), struct.pack('<12f', *([0, 0, 1] * 4)),
              struct.pack('<8f', *uvs), struct.pack('<6H', 0, 1, 2, 0, 2, 3), atlas]
    views, binary = [], bytearray()
    for chunk in chunks:
        binary.extend(b'\0' * (-len(binary) % 4))
        views.append({'buffer': 0, 'byteOffset': len(binary), 'byteLength': len(chunk)})
        binary.extend(chunk)
    binary.extend(b'\0' * (-len(binary) % 4))
    document = {
        'asset': {'version': '2.0', 'generator': 'build_photo_surface.py'},
        'scene': 0, 'scenes': [{'nodes': [0]}], 'nodes': [{'name': 'Runtime_Photo_Surface', 'mesh': 0}],
        'meshes': [{'primitives': [{'attributes': {'POSITION': 0, 'NORMAL': 1, 'TEXCOORD_0': 2}, 'indices': 3, 'material': 0}]}],
        'materials': [{'name': 'Frame_Photo_Surface', 'doubleSided': True, 'alphaMode': 'BLEND',
                       'pbrMetallicRoughness': {'baseColorTexture': {'index': 0}, 'metallicFactor': 0, 'roughnessFactor': 1}}],
        'textures': [{'source': 0, 'sampler': 0}], 'samplers': [{'magFilter': 9728, 'minFilter': 9728}],
        'images': [{'bufferView': 4, 'mimeType': 'image/png'}],
        'buffers': [{'byteLength': len(binary)}], 'bufferViews': views,
        'accessors': [
            {'bufferView': 0, 'componentType': 5126, 'count': 4, 'type': 'VEC3', 'min': [-half, -half, .010], 'max': [half, half, .010]},
            {'bufferView': 1, 'componentType': 5126, 'count': 4, 'type': 'VEC3'},
            {'bufferView': 2, 'componentType': 5126, 'count': 4, 'type': 'VEC2'},
            {'bufferView': 3, 'componentType': 5123, 'count': 6, 'type': 'SCALAR'},
        ],
    }
    encoded = json.dumps(document, separators=(',', ':')).encode()
    encoded += b' ' * (-len(encoded) % 4)
    result = struct.pack('<III', 0x46546C67, 2, 28 + len(encoded) + len(binary))
    result += struct.pack('<II', len(encoded), 0x4E4F534A) + encoded
    result += struct.pack('<II', len(binary), 0x004E4942) + binary
    (FOLDER / 'picture_frame_photo_surface.glb').write_bytes(result)


if __name__ == '__main__':
    build()
