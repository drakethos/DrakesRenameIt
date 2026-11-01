"""Book + shelf meshes for RenameIt (Valheim look, low poly).

Run:  blender -b -P make_book_meshes.py
Outputs (next to this script):
  book_<size>.glb, wall_shelf.glb, bookcase.glb   - one mesh each, modifiers applied, for Unity
  books_scene.blend, render_*.png                  - preview scene (books on both shelves)
  ../../../Books/Mesh/textures/book_cover.png      - grey leather texture, tinted by the cover colour in game
  ../../../Books/Mesh/textures/book_icon.png       - inventory icon rendered from the mesh
Pages reuse RenameIt's paper texture (Paper/Mesh/textures/paper_mesh_sheet.png). Wood in game comes from a vanilla piece's
material, so the wood here is only a preview stand-in.

Axes (Blender, Z up): books stand upright, spine facing -Y, width (thickness) along X. Shelves face -Y, wall at +Y.
Mirror these numbers in Books/Mesh/BookMesh.cs.
"""
import math
import os

import bpy
import bmesh
import numpy as np
from mathutils import Vector

OUT = os.path.dirname(os.path.abspath(__file__))
GAME_TEX = os.path.normpath(os.path.join(OUT, "..", "..", "..", "Books", "Mesh", "textures"))
PAPER_TEX = os.path.normpath(os.path.join(OUT, "..", "..", "..", "Paper", "Mesh", "textures", "paper_mesh_sheet.png"))
os.makedirs(GAME_TEX, exist_ok=True)
rng = np.random.default_rng(11)

# (cover depth, height, thickness) in metres. Paper sheet is 0.216 x 0.279, so medium is about 1.25x.
SIZES = {"small": (0.21, 0.29, 0.040), "medium": (0.26, 0.36, 0.050), "large": (0.33, 0.45, 0.065)}
BOARD = 0.005          # cover board thickness
PAGE_INSET = 0.006     # pages sit this far in from the cover edges

# Shelf icons use these (no brown or gold: they read as wooden dividers between the books).
ICON_COLORS = [(0.55, 0.16, 0.16), (0.16, 0.29, 0.55), (0.16, 0.42, 0.23), (0.40, 0.16, 0.46), (0.12, 0.40, 0.42)]
COLORS = [(0.48, 0.16, 0.16), (0.16, 0.29, 0.48), (0.16, 0.42, 0.23), (0.35, 0.23, 0.10), (0.35, 0.16, 0.42), (0.78, 0.64, 0.23)]


# ---------------------------------------------------------------- textures
def cover_texture():
    """Grey leather, 64x64, banded and point filtered; the game multiplies it by the cover colour."""
    n = 64
    base = rng.standard_normal((n, n))
    fy, fx = np.fft.fftfreq(n)[:, None], np.fft.fftfreq(n)[None, :]
    k = np.exp(-2 * (math.pi ** 2) * 1.6 ** 2 * (fx ** 2 + fy ** 2))
    a = np.real(np.fft.ifft2(np.fft.fft2(base) * k))
    a = (a - a.min()) / (a.max() - a.min() + 1e-9)
    a = np.round(a * 4) / 4
    grey = 0.62 + a * 0.38
    px = np.dstack([grey, grey, grey, np.ones_like(grey)]).astype(np.float32)
    img = bpy.data.images.new("book_cover", n, n, alpha=True)
    img.pixels = px.ravel().tolist()
    img.filepath_raw = os.path.join(GAME_TEX, "book_cover.png")
    img.file_format = "PNG"
    img.save()
    return img


def image_mat(name, image, tint=(1, 1, 1), rough=0.85):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = image
    tex.interpolation = "Closest"
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.blend_type = "MULTIPLY"
    mix.inputs[0].default_value = 1.0
    mix.inputs[7].default_value = (*tint, 1)
    nt.links.new(tex.outputs["Color"], mix.inputs[6])
    nt.links.new(mix.outputs[2], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = rough
    return m


def flat_mat(name, color, rough=0.9):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*color, 1)
    b.inputs["Roughness"].default_value = rough
    return m


def wood_mat():
    m = bpy.data.materials.new("Wood")
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    wave = nt.nodes.new("ShaderNodeTexWave")
    wave.inputs["Scale"].default_value = 4.0
    wave.inputs["Distortion"].default_value = 5.0
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (0.16, 0.09, 0.04, 1)
    ramp.color_ramp.elements[1].color = (0.32, 0.19, 0.09, 1)
    nt.links.new(wave.outputs["Fac"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.9
    return m


# ---------------------------------------------------------------- geometry helpers
def add_box(bm, x0, x1, y0, y1, z0, z1, mat):
    res = bmesh.ops.create_cube(bm, size=1.0, calc_uvs=True)
    for v in res["verts"]:
        v.co.x = x0 if v.co.x < 0 else x1
        v.co.y = y0 if v.co.y < 0 else y1
        v.co.z = z0 if v.co.z < 0 else z1
    for f in {f for v in res["verts"] for f in v.link_faces}:
        f.material_index = mat


def finish(name, bm, mats, bevel=0.002):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(obj)
    for m in mats:
        me.materials.append(m)
    if bevel:
        bv = obj.modifiers.new("Bevel", "BEVEL")
        bv.width = bevel
        bv.segments = 1
        bv.limit_method = "ANGLE"
    return obj


# ---------------------------------------------------------------- the book
def make_book(name, size, color, mats):
    d, h, t = SIZES[size]
    cover, pages = mats
    bm = bmesh.new()
    # Cover: back board, front board, spine board (facing -Y). Slot 0 = cover, 1 = pages.
    add_box(bm, -t / 2, -t / 2 + BOARD, -d / 2, d / 2, 0, h, 0)
    add_box(bm, t / 2 - BOARD, t / 2, -d / 2, d / 2, 0, h, 0)
    add_box(bm, -t / 2, t / 2, -d / 2, -d / 2 + BOARD, 0, h, 0)
    # Page block, set in from the cover edges so the boards overhang a little.
    add_box(bm, -t / 2 + BOARD, t / 2 - BOARD, -d / 2 + BOARD, d / 2 - PAGE_INSET, PAGE_INSET, h - PAGE_INSET, 1)
    obj = finish(name, bm, [cover, pages])
    return obj


# ---------------------------------------------------------------- shelves
# Variants: (id, width, shelf rows). The ids match the prefab names in Books/Pieces/BookShelfPiece.cs and the icon files.
WALL_VARIANTS = [("wall_shelf", 0.90), ("wall_shelf_long", 1.80)]
CASE_VARIANTS = [("bookcase_small", 0.80, 2), ("bookcase", 1.10, 3), ("bookcase_large", 1.40, 4), ("bookcase_wide", 2.00, 3)]


def make_wall_shelf(wood, w=0.90):
    """One board on brackets with a back rail. w wide, 0.26 deep; top of the board is z=0, wall at +Y."""
    d, th = 0.26, 0.03
    bm = bmesh.new()
    add_box(bm, -w / 2, w / 2, -d / 2, d / 2, -th, 0, 0)
    add_box(bm, -w / 2, w / 2, d / 2 - 0.02, d / 2, -th, 0.09, 0)
    xs = [-w / 2 + 0.10, w / 2 - 0.10]
    if w > 1.4:
        xs.insert(1, 0.0)
    for x in xs:
        add_box(bm, x - 0.015, x + 0.015, -d / 2 + 0.03, d / 2 - 0.02, -th - 0.03, -th, 0)
        add_box(bm, x - 0.015, x + 0.015, d / 2 - 0.05, d / 2 - 0.02, -th - 0.14, -th - 0.03, 0)
    return finish("WallShelf", bm, [wood], bevel=0.002)


def make_bookcase(wood, w=1.10, rows=3):
    """Open shelves with a left and a right side and a thin back panel: w wide, 0.36 deep, `rows` rows on a 0.50 pitch (tallest
    book is 0.45). Row r stands on z = plinth + pitch * r; wall at +Y. No dividers between: each row is one open run."""
    d, th = 0.36, 0.03
    plinth, pitch = 0.08, 0.50
    height = plinth + pitch * rows + th
    bm = bmesh.new()
    add_box(bm, -w / 2, w / 2, -d / 2, d / 2, plinth - th, plinth, 0)                       # bottom board
    for r in range(1, rows + 1):
        z = plinth + pitch * r
        add_box(bm, -w / 2, w / 2, -d / 2, d / 2, z - th, z, 0)                             # shelf boards
    add_box(bm, -w / 2 - 0.02, w / 2 + 0.02, -d / 2 - 0.02, d / 2, height, height + 0.03, 0)  # top cap, overhangs
    add_box(bm, -w / 2, -w / 2 + th, -d / 2, d / 2, 0, height, 0)                           # left side
    add_box(bm, w / 2 - th, w / 2, -d / 2, d / 2, 0, height, 0)                             # right side
    add_box(bm, -w / 2, w / 2, d / 2 - 0.015, d / 2, 0, height, 0)                          # back panel
    return finish("Bookcase", bm, [wood], bevel=0.002), [plinth + pitch * r for r in range(rows)]


# ---------------------------------------------------------------- scene
def make_scene():
    sc = bpy.context.scene
    try:
        sc.render.engine = "BLENDER_EEVEE"
        sc.eevee.taa_render_samples = 16
    except (TypeError, AttributeError):
        sc.render.engine = "CYCLES"
        sc.cycles.samples = 16
    sc.render.resolution_x, sc.render.resolution_y = 900, 700
    sc.view_settings.view_transform = "AgX"
    sun = bpy.data.lights.new("Sun", "SUN"); sun.energy = 3.0
    so = bpy.data.objects.new("Sun", sun); sc.collection.objects.link(so)
    so.rotation_euler = (math.radians(55), math.radians(10), math.radians(-30))
    fill = bpy.data.lights.new("Fill", "AREA"); fill.energy = 60; fill.size = 2.0
    fo = bpy.data.objects.new("Fill", fill); sc.collection.objects.link(fo)
    fo.location = (-1.2, -1.6, 1.4); fo.rotation_euler = (math.radians(60), 0, math.radians(-35))
    sc.world = sc.world or bpy.data.worlds.new("World")
    sc.world.use_nodes = True
    sc.world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.25
    cam = bpy.data.cameras.new("Cam"); cam.lens = 50
    co = bpy.data.objects.new("Cam", cam); sc.collection.objects.link(co)
    sc.camera = co
    return co


def aim(cam, loc, target):
    cam.location = loc
    cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()


def render(name):
    bpy.context.scene.render.filepath = os.path.join(OUT, name)
    bpy.ops.render.render(write_still=True)


def export(obj, filename):
    for o in bpy.context.scene.objects:
        o.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.gltf(filepath=os.path.join(OUT, filename), export_format="GLB", use_selection=True, export_apply=True)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    leather = cover_texture()
    paper = bpy.data.images.load(PAPER_TEX)
    pages = image_mat("Pages", paper)
    wood = wood_mat()
    cover_cache = {}

    def mats(color):
        if color not in cover_cache:
            cover_cache[color] = image_mat("Cover%d" % len(cover_cache), leather, tint=color)
        return [cover_cache[color], pages]

    cam = make_scene()

    # Meshes to ship (each at the origin, exported alone).
    for size in SIZES:
        b = make_book("book_" + size, size, None, mats(COLORS[0]))
        export(b, f"book_{size}.glb")
        bpy.data.objects.remove(b)
    for vid, vw in WALL_VARIANTS:
        o = make_wall_shelf(wood, vw)
        export(o, vid + ".glb")
        bpy.data.objects.remove(o)
    for vid, vw, vr in CASE_VARIANTS:
        o, _ = make_bookcase(wood, vw, vr)
        export(o, vid + ".glb")
        bpy.data.objects.remove(o)
    shelf = make_wall_shelf(wood)
    case, row_z = make_bookcase(wood)

    # Preview: bookcase with books, wall shelf beside it.
    shelf.location = (1.4, 0.18, 1.2)
    scn_col = bpy.context.collection
    wall = bpy.data.objects.new("Wall", bpy.data.meshes.new("Wall"))
    # (back wall plane for context)
    bm = bmesh.new()
    add_box(bm, -2.0, 2.4, 0.19, 0.22, 0, 2.2, 0)
    bm.to_mesh(wall.data); bm.free()
    wall.data.materials.append(flat_mat("Wall", (0.18, 0.15, 0.12)))
    scn_col.objects.link(wall)

    seqs = [["medium", "small", "large", "medium", "small", "medium"], ["large", "medium", "medium", "small", "large"], ["small", "medium", "large", "small", "medium"]]
    for r, z in enumerate(row_z):
        x = -0.52
        i = r
        while True:
            size = seqs[r][i % len(seqs[r])]
            t = SIZES[size][2]
            if x + t > 0.52:
                break
            b = make_book("B", size, None, mats(COLORS[(i + r) % len(COLORS)]))
            b.location = (x + t / 2, 0.0, z)
            x += t + 0.003
            i += 1
        if r == 2:
            break
    x = 1.4 - 0.40
    i = 0
    while True:
        size = ["medium", "small", "large", "medium"][i % 4]
        t = SIZES[size][2]
        if x + t > 1.4 + 0.40:
            break
        b = make_book("B", size, None, mats(COLORS[(i + 2) % len(COLORS)]))
        b.location = (x + t / 2, 0.14, 1.2)
        x += t + 0.003
        i += 1

    aim(cam, (0.7, -4.2, 1.5), (0.7, 0.0, 0.85)); render("render_shelves.png")
    aim(cam, (0.35, -0.9, 0.5), (0.0, 0.0, 0.35)); render("render_case_close.png")

    # Inventory icon: medium book, three-quarter view, transparent.
    for o in list(bpy.context.scene.objects):
        if o.type == "MESH":
            o.hide_render = True
    ic = make_book("icon_book", "medium", None, mats(COLORS[0]))
    ic.rotation_euler = (math.radians(-18), 0, math.radians(-62))
    sc = bpy.context.scene
    sc.render.film_transparent = True
    sc.render.resolution_x = sc.render.resolution_y = 128
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 0.55
    aim(cam, (0.0, -1.0, 0.18), (0.0, 0.0, 0.18))
    sc.render.filepath = os.path.join(GAME_TEX, "book_icon.png")
    bpy.ops.render.render(write_still=True)

    # Piece icons for the hammer menu: every shelf size with books on it, 3/4 view, transparent.
    def icon_of(name, objs, ortho, loc, target):
        for o in list(bpy.context.scene.objects):
            if o.type == "MESH":
                o.hide_render = o not in objs
        cam.data.ortho_scale = ortho
        aim(cam, loc, target)
        sc.render.filepath = os.path.join(GAME_TEX, name)
        bpy.ops.render.render(write_still=True)

    def stand_books(x0, x1, z, y, offset_x=0.0, offset_y=0.0, offset_z=0.0):
        out, x, i = [], x0, 0
        while True:
            size = ["medium", "small", "large", "medium"][i % 4]
            t = SIZES[size][2]
            if x + t > x1:
                break
            b = make_book("B", size, None, mats(ICON_COLORS[(i + 1) % len(ICON_COLORS)]))
            b.location = (x + t / 2 + offset_x, y + offset_y, z + offset_z)
            out.append(b)
            x += t + 0.003
            i += 1
        return out

    slot = 0
    for vid, vw in WALL_VARIANTS:
        slot += 1
        ox = slot * 4.0
        ws = make_wall_shelf(wood, vw)
        ws.location = (ox, 0, 0)
        books = stand_books(-vw / 2 + 0.06, vw / 2 - 0.06, 0.0, 0.0, offset_x=ox)
        icon_of(vid + "_icon.png", [ws] + books, vw * 1.15 + 0.1, (ox + vw * 0.35, -2.2, 0.45), (ox, 0.0, 0.12))
    for vid, vw, vr in CASE_VARIANTS:
        slot += 1
        ox = slot * 4.0
        c2, rz = make_bookcase(wood, vw, vr)
        c2.location = (ox, 0, 0)
        books = []
        for z in rz:
            books += stand_books(-vw / 2 + 0.06, vw / 2 - 0.06, z, 0.0, offset_x=ox)
        h = 0.11 + 0.5 * vr
        icon_of(vid + "_icon.png", [c2] + books, max(vw, h) * 1.2, (ox + vw * 0.45, -3.6, h * 0.62), (ox, 0.0, h * 0.5))

    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "books_scene.blend"))
    print("BOOKS DONE")


main()
