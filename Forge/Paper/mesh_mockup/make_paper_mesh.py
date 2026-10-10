"""Paper mesh mockup for RenameIt.

Run:  blender -b -P make_paper_mesh.py
Outputs (next to this script):
  paper_albedo.png, paper_albedo_written.png, paper_normal.png - generated 128x166 textures
  ../../../Paper/Mesh/textures/paper_mesh_*.png                  - the same, embedded in the mod
  paper_mesh.blend                     - scene with mesh, materials, lights
  paper_mesh.glb                       - mesh only, modifiers applied, for Unity
  render_hero.png, render_top.png, render_edge.png
"""
import math
import os
import random

import bpy
import bmesh
import numpy as np
from mathutils import Vector

OUT = os.path.dirname(os.path.abspath(__file__))
GAME_TEX = os.path.normpath(os.path.join(OUT, "..", "..", "..", "Paper", "Mesh", "textures"))
rng = np.random.default_rng(7)
random.seed(7)

# US Letter at scale 1 (matches the sprite pack).
W, H = 0.216, 0.279
THICK = 0.0007          # exaggerated vs. real paper (0.1 mm) so the edge reads in game
TEX_W, TEX_H = 128, 166   # small on purpose: Valheim props are low-res + point filtered


# ---------------------------------------------------------------- textures
def blur(a, sigma):
    """Gaussian blur via FFT (wraps, which keeps the texture tileable)."""
    h, w = a.shape
    fy = np.fft.fftfreq(h)[:, None]
    fx = np.fft.fftfreq(w)[None, :]
    k = np.exp(-2 * (math.pi ** 2) * (sigma ** 2) * (fx ** 2 + fy ** 2))
    return np.real(np.fft.ifft2(np.fft.fft2(a) * k))


def norm(a):
    a = a - a.min()
    return a / (a.max() + 1e-9)


def fbm(h, w, octaves):
    out = np.zeros((h, w))
    amp = 1.0
    for sigma in octaves:
        out += amp * norm(blur(rng.standard_normal((h, w)), sigma))
        amp *= 0.55
    return norm(out)


def fibers(h, w, count, length, sigma):
    """Short random strokes — the pulp fibres you see in handmade paper."""
    img = np.zeros((h, w))
    for _ in range(count):
        x, y = rng.uniform(0, w), rng.uniform(0, h)
        ang = rng.uniform(0, math.pi)
        ln = rng.uniform(0.4, 1.0) * length
        steps = int(ln)
        bend = rng.uniform(-0.03, 0.03)
        strength = rng.uniform(0.3, 1.0)
        for s in range(steps):
            ang += bend
            x += math.cos(ang)
            y += math.sin(ang)
            img[int(y) % h, int(x) % w] += strength
    return norm(blur(img, sigma))


def posterize(a, levels):
    return np.round(np.clip(a, 0, 1) * (levels - 1)) / (levels - 1)


def make_textures():
    """Valheim-style: tiny texture, banded tones, crisp pixel detail. Use Point filtering in Unity."""
    h, w = TEX_H, TEX_W
    mottle = posterize(fbm(h, w, [14, 6, 2]), 5)   # broad painterly blotches, few tone steps
    fib = fibers(h, w, 160, 7, 0.0)                # 1-px strokes, no blur = crisp under Point filter
    fib = (fib > 0.15).astype(float)
    speck = (rng.random((h, w)) > 0.985).astype(float)

    # Edge band: a ragged darker rim, stepped rather than a smooth gradient.
    yy, xx = np.mgrid[0:h, 0:w]
    ex = np.minimum(xx, w - 1 - xx) / w
    ey = np.minimum(yy, h - 1 - yy) / h
    edge = np.clip(1 - np.minimum(ex, ey) / 0.06, 0, 1) ** 1.5
    edge = posterize(np.clip(edge * (0.4 + 1.0 * fbm(h, w, [4, 1.5])), 0, 1), 4)

    light = np.array([0.78, 0.69, 0.51])          # warm parchment
    mid = np.array([0.66, 0.55, 0.37])
    dark = np.array([0.55, 0.42, 0.26])
    t = np.clip(0.55 * mottle + 0.6 * edge, 0, 1)[..., None]
    col = light * (1 - t) + mid * t
    col = np.where((edge > 0.7)[..., None], dark * 0.5 + col * 0.5, col)
    col = col * (1 - 0.06 * fib[..., None]) * (1 - 0.07 * speck[..., None])
    col = np.clip(col, 0, 1)

    height = 0.6 * mottle + 0.4 * fib
    gy, gx = np.gradient(height)
    s = 1.5
    nx, ny, nz = -gx * s, gy * s, np.ones_like(height)
    ln = np.sqrt(nx ** 2 + ny ** 2 + nz ** 2)
    nrm = np.stack([nx / ln, ny / ln, nz / ln], -1) * 0.5 + 0.5

    def save(name, rgb, non_color=False):
        img = bpy.data.images.new(name, w, h, alpha=False)
        # Set colorspace BEFORE pixels: changing it afterwards reloads the image and wipes the buffer.
        if non_color:
            img.colorspace_settings.name = "Non-Color"
        rgba = np.concatenate([rgb, np.ones((h, w, 1))], -1)
        # numpy row 0 = top; Blender row 0 = bottom
        img.pixels.foreach_set(rgba[::-1].astype(np.float32).ravel())
        img.filepath_raw = os.path.join(OUT, name + ".png")
        img.file_format = "PNG"
        img.save()
        return img

    written = scribble(col)
    albedo = save("paper_albedo", col)
    normal = save("paper_normal", nrm, non_color=True)
    save("paper_albedo_written", written)
    # Game copies (embedded in RenameIt's DLL; unique leaf names because embedded lookup matches by file name).
    for src, dst in (("paper_albedo", "paper_mesh_sheet"), ("paper_albedo_written", "paper_mesh_sheet_written"),
                     ("paper_normal", "paper_mesh_normal")):
        os.makedirs(GAME_TEX, exist_ok=True)
        with open(os.path.join(OUT, src + ".png"), "rb") as fi, open(os.path.join(GAME_TEX, dst + ".png"), "wb") as fo:
            fo.write(fi.read())
    return albedo, normal


def scribble(col):
    """Pixel 'handwriting': wobbly 2-px ink rows inside the margins, like the sprite's written page.
    2 px and near-opaque so the lines survive mipmapping on a dropped page seen from a few metres."""
    out = col.copy()
    h, w = col.shape[:2]
    ink = np.array([0.14, 0.09, 0.05])
    top, bottom, left, right = int(h * 0.12), int(h * 0.88), int(w * 0.13), int(w * 0.87)
    y = top
    while y < bottom:
        x = left + (rng.integers(0, 6) if y == top else 0)
        end = right - rng.integers(0, 14) if rng.random() > 0.15 else left + rng.integers(15, 45)
        while x < end:
            word = rng.integers(4, 12)
            for k in range(word):
                if x + k >= end:
                    break
                yy = y + (1 if rng.random() < 0.2 else 0) - (1 if rng.random() < 0.1 else 0)
                a = rng.uniform(0.85, 1.0)
                for row in (yy, yy + 1):
                    out[row, x + k] = out[row, x + k] * (1 - a) + ink * a
            x += word + rng.integers(2, 4)
        y += 8 + (5 if rng.random() < 0.12 else 0)   # occasional paragraph gap
    return out


# ---------------------------------------------------------------- mesh
def make_paper_mesh():
    # Low-poly: the bow is the only shape left, so a coarse grid is enough (~390 tris after Solidify).
    nx, ny = 8, 10
    bm = bmesh.new()
    verts = {}
    for j in range(ny + 1):
        for i in range(nx + 1):
            u, v = i / nx, j / ny
            verts[i, j] = bm.verts.new(((u - 0.5) * W, (v - 0.5) * H, 0.0))
    uv_layer = bm.loops.layers.uv.new("UVMap")
    for j in range(ny):
        for i in range(nx):
            f = bm.faces.new((verts[i, j], verts[i + 1, j], verts[i + 1, j + 1], verts[i, j + 1]))
            for loop, (a, b) in zip(f.loops, ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))):
                loop[uv_layer].uv = (a / nx, b / ny)

    # Deckled edge: wobble border verts in-plane (keeps UVs, so texture edge toning still lines up).
    edge_noise = rng.standard_normal(4 * (nx + ny))
    edge_noise = np.convolve(edge_noise, np.ones(3) / 3, mode="same")
    k = 0
    for (i, j), vtx in verts.items():
        if i in (0, nx) or j in (0, ny):
            n = edge_noise[k % len(edge_noise)] * 0.0015
            k += 1
            if i in (0, nx):
                vtx.co.x += n * (1 if i == nx else -1)
            if j in (0, ny):
                vtx.co.y += n * (1 if j == ny else -1)
    # Slightly soften the four corners.
    for (i, j) in ((0, 0), (nx, 0), (0, ny), (nx, ny)):
        c = verts[i, j].co
        c.x *= 0.985
        c.y *= 0.99

    # Gentle bow along the long axis + faint ripple, so it never looks like a flat card.
    for vtx in bm.verts:
        x, y = vtx.co.x, vtx.co.y
        vtx.co.z += 0.0035 * (1 - (2 * y / H) ** 2) * 0.5
        vtx.co.z += 0.0006 * math.sin(x * 22 + y * 9)   # low frequency so the coarse grid can carry it

    me = bpy.data.meshes.new("Paper")
    bm.to_mesh(me)
    bm.free()
    for poly in me.polygons:
        poly.use_smooth = True
    obj = bpy.data.objects.new("Paper", me)
    bpy.context.collection.objects.link(obj)

    sol = obj.modifiers.new("Thickness", "SOLIDIFY")
    sol.thickness = THICK
    sol.offset = -1.0
    sol.use_even_offset = True
    sol.material_offset_rim = 1
    sol.use_rim = True
    obj.location.z = THICK
    return obj


def make_materials(obj, albedo, normal):
    m = bpy.data.materials.new("PaperSheet")
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = albedo
    ntex = nt.nodes.new("ShaderNodeTexImage"); ntex.image = normal
    tex.interpolation = ntex.interpolation = "Closest"   # = Unity Filter Mode: Point (no filter)
    nmap = nt.nodes.new("ShaderNodeNormalMap"); nmap.inputs["Strength"].default_value = 0.5
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(ntex.outputs["Color"], nmap.inputs["Color"])
    nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    bsdf.inputs["Roughness"].default_value = 0.85
    obj.data.materials.append(m)

    rim = bpy.data.materials.new("PaperEdge")
    rim.use_nodes = True
    rb = rim.node_tree.nodes["Principled BSDF"]
    rb.inputs["Base Color"].default_value = (0.42, 0.32, 0.20, 1)
    rb.inputs["Roughness"].default_value = 0.95
    obj.data.materials.append(rim)


# ---------------------------------------------------------------- scene
def make_scene(paper):
    sc = bpy.context.scene
    # Quick previews: EEVEE, small frame. Falls back to low-sample Cycles if EEVEE isn't available.
    try:
        sc.render.engine = "BLENDER_EEVEE"
        sc.eevee.taa_render_samples = 16
    except (TypeError, AttributeError):
        sc.render.engine = "CYCLES"
        sc.cycles.samples = 16
        sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = 900, 640
    sc.view_settings.view_transform = "AgX"

    # Dark plank table so the cream paper pops (rough Valheim-ish vibe).
    bpy.ops.mesh.primitive_plane_add(size=1.2, location=(0, 0, 0))
    table = bpy.context.object
    tm = bpy.data.materials.new("Table"); tm.use_nodes = True
    tnt = tm.node_tree; tb = tnt.nodes["Principled BSDF"]
    wave = tnt.nodes.new("ShaderNodeTexWave")
    wave.inputs["Scale"].default_value = 3.0
    wave.inputs["Distortion"].default_value = 6.0
    wave.inputs["Detail"].default_value = 4.0
    ramp = tnt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (0.05, 0.028, 0.014, 1)
    ramp.color_ramp.elements[1].color = (0.16, 0.09, 0.045, 1)
    tnt.links.new(wave.outputs["Fac"], ramp.inputs["Fac"])
    tnt.links.new(ramp.outputs["Color"], tb.inputs["Base Color"])
    tb.inputs["Roughness"].default_value = 0.9
    table.data.materials.append(tm)

    paper.rotation_euler.z = math.radians(-8)

    sun = bpy.data.lights.new("Sun", "SUN"); sun.energy = 3.0; sun.angle = math.radians(4)
    sun.color = (1.0, 0.88, 0.72)
    so = bpy.data.objects.new("Sun", sun); sc.collection.objects.link(so)
    so.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))

    fill = bpy.data.lights.new("Fill", "AREA"); fill.energy = 25; fill.size = 1.0
    fill.color = (0.75, 0.85, 1.0)
    fo = bpy.data.objects.new("Fill", fill); sc.collection.objects.link(fo)
    fo.location = (-0.5, 0.4, 0.6); fo.rotation_euler = (math.radians(-40), math.radians(-40), 0)

    sc.world = sc.world or bpy.data.worlds.new("World")
    sc.world.use_nodes = True
    sc.world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.15

    cam = bpy.data.cameras.new("Cam"); cam.lens = 60
    co = bpy.data.objects.new("Cam", cam); sc.collection.objects.link(co)
    sc.camera = co
    return co


def aim(cam, loc, target):
    cam.location = loc
    cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()


def render(name):
    bpy.context.scene.render.filepath = os.path.join(OUT, name)
    bpy.ops.render.render(write_still=True)


def export_glb(paper):
    """Mesh only, modifiers applied, rotation reset — ready for the Forge/Unity side."""
    for o in bpy.context.scene.objects:
        o.select_set(False)
    paper.select_set(True)
    bpy.context.view_layer.objects.active = paper
    rot = paper.rotation_euler.copy()
    paper.rotation_euler = (0, 0, 0)
    bpy.ops.export_scene.gltf(
        filepath=os.path.join(OUT, "paper_mesh.glb"),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
    )
    paper.rotation_euler = rot


def render_icons(paper, cam):
    """Inventory icons from the mesh itself: top-down, transparent, blank + written. Copied into the mod."""
    sc = bpy.context.scene
    hidden = [o for o in sc.objects if o.type == "MESH" and o != paper]
    for o in hidden:
        o.hide_render = True
    sc.render.film_transparent = True
    sc.render.resolution_x = sc.render.resolution_y = 128
    rot = paper.rotation_euler.copy()
    paper.rotation_euler = (math.radians(12), 0, math.radians(-10))   # small tilt so the edge reads
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 0.31
    aim(cam, (0.0, -0.05, 0.6), (0.0, 0.0, 0.0))

    tex = next(n for n in paper.data.materials[0].node_tree.nodes if n.type == "TEX_IMAGE" and n.image.name == "paper_albedo")
    blank_img = tex.image
    for name, img in (("paper_mesh_icon", blank_img), ("paper_mesh_written_icon", bpy.data.images["paper_albedo_written"])):
        tex.image = img
        render(name + ".png")
        with open(os.path.join(OUT, name + ".png"), "rb") as fi, open(os.path.join(GAME_TEX, name + ".png"), "wb") as fo:
            fo.write(fi.read())
    tex.image = blank_img

    paper.rotation_euler = rot
    cam.data.type = "PERSP"
    sc.render.film_transparent = False
    for o in hidden:
        o.hide_render = False


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    albedo, normal = make_textures()
    paper = make_paper_mesh()
    make_materials(paper, albedo, normal)
    export_glb(paper)
    cam = make_scene(paper)

    aim(cam, (0.30, -0.42, 0.34), (0.0, -0.01, 0.0)); render("render_hero.png")
    aim(cam, (0.0, -0.02, 0.58), (0.0, 0.0, 0.0)); bpy.context.scene.camera.data.lens = 50
    render("render_top.png")
    bpy.context.scene.camera.data.lens = 70
    aim(cam, (0.20, -0.26, 0.06), (0.06, -0.10, 0.0)); render("render_edge.png")
    render_icons(paper, cam)

    dg = bpy.context.evaluated_depsgraph_get()
    ev = paper.evaluated_get(dg).to_mesh()
    ev.calc_loop_triangles()
    print(f"PAPER TRIS: {len(ev.loop_triangles)}  VERTS: {len(ev.vertices)}")
    paper.evaluated_get(dg).to_mesh_clear()

    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "paper_mesh.blend"))


main()
