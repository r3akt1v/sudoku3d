"""
Blender script v3: Table + Sudoku boards for 3x3 / 6x6 / 9x9 + tiles with baked digits.
Run: blender --background --python generate_models_v3.py
Output: FBX files into Assets/Models

Layout constants MUST match BoardMetrics.cs:
  CELL=0.9 GAP=0.07 PITCH=0.97 SECTOR=0.24
  PLATE_T=0.22 CELL_H=0.12 FRAME_W=0.6 RAIL_H=0.42
  Tile: 0.8 x 0.8 x 0.22
Boxes: 3x3 -> 3x3 (no inner sectors), 6x6 -> 2 cols x 3 rows, 9x9 -> 3x3
"""
import bpy
import os

OUT_DIR = "/Users/aleksandrsmirnov/Documents/sudoku/Sudoku3D/Assets/Models"
os.makedirs(OUT_DIR, exist_ok=True)

CELL = 0.9
GAP = 0.07
PITCH = CELL + GAP          # 0.97
SECTOR = 0.24
PLATE_T = 0.22
CELL_H = 0.12
RAIL_H = 0.42
FRAME_W = 0.6

# (N, box_cols, box_rows, filename)
BOARDS = [
    (3, 3, 3, "SudokuBoard_3"),
    (6, 2, 3, "SudokuBoard_6"),
    (9, 3, 3, "SudokuBoard_9"),
]


def axis_raw(i, box_dim):
    return i * PITCH + SECTOR * (i // box_dim)


def axis_center(n, box_dim):
    return axis_raw(n - 1, box_dim) * 0.5


def axis_off(i, box_dim, n):
    return axis_raw(i, box_dim) - axis_center(n, box_dim)


def inner_span(n, box_dim):
    return axis_raw(n - 1, box_dim) + CELL


def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for block in list(bpy.data.meshes):
        bpy.data.meshes.remove(block)
    for block in list(bpy.data.curves):
        bpy.data.curves.remove(block)
    for block in list(bpy.data.materials):
        bpy.data.materials.remove(block)


def cube(name, sx, sy, sz, loc, bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc)
    o = bpy.context.active_object
    o.name = name
    o.scale = (sx, sy, sz)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0:
        mod = o.modifiers.new(name="Bevel", type='BEVEL')
        mod.width = bevel
        mod.segments = 2
        mod.limit_method = 'ANGLE'
        bpy.context.view_layer.objects.active = o
        o.select_set(True)
        bpy.ops.object.modifier_apply(modifier=mod.name)
        o.select_set(False)
    return o


def make_mat(name, color, rough=0.6):
    m = bpy.data.materials.new(name=name)
    m.use_nodes = True
    bsdf = None
    for node in m.node_tree.nodes:
        if node.type == 'BSDF_PRINCIPLED':
            bsdf = node
            break
    if bsdf is None:
        bsdf = m.node_tree.nodes.new(type='ShaderNodeBsdfPrincipled')
        out = m.node_tree.nodes.get("Material Output") or m.node_tree.nodes.new(type='ShaderNodeOutputMaterial')
        m.node_tree.links.new(bsdf.outputs[0], out.inputs[0])
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = 0.0
    return m


def assign_mat(obj, mat):
    obj.data.materials.append(mat)


def join_selected(name):
    bpy.ops.object.select_all(action='DESELECT')
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = name
    return obj


def export(obj, filename):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    path = os.path.join(OUT_DIR, filename)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True,
                             axis_forward='-Z', axis_up='Y',
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_NONE',
                             mesh_smooth_type='FACE')
    print(f"Exported {filename}")


# ================= TABLE =================
def build_table():
    clear_scene()
    wood = make_mat("TableWood", (0.72, 0.60, 0.47), rough=0.75)

    parts = []
    lx = 17.0 / 2 - 1.1
    ly = 16.0 / 2 - 1.1
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(cube("Leg", 0.55, 0.55, 1.7, (sx * lx, sy * ly, 0.85), bevel=0.05))
    parts.append(cube("TableTop", 17.0, 16.0, 0.5, (0, 0, 1.95), bevel=0.06))
    for p in parts:
        assign_mat(p, wood)
    table = join_selected("Table")
    export(table, "Table.fbx")


# ================= BOARDS =================
def build_board(n, box_cols, box_rows, fname):
    clear_scene()
    board_mat = make_mat("BoardMat", (0.55, 0.62, 0.56), rough=0.65)

    inner_x = inner_span(n, box_cols)
    inner_z = inner_span(n, box_rows)
    outer_x = inner_x + 2 * FRAME_W
    outer_z = inner_z + 2 * FRAME_W

    parts = []
    # base plate: bottom at z=0
    parts.append(cube("Plate", outer_x, outer_z, PLATE_T,
                      (0, 0, PLATE_T / 2), bevel=0.04))

    # cells
    for r in range(n):
        for c in range(n):
            x = axis_off(c, box_cols, n)
            y = axis_off(r, box_rows, n)
            parts.append(cube(f"C{r}_{c}", CELL, CELL, CELL_H,
                              (x, y, PLATE_T + CELL_H / 2), bevel=0.025))

    # frame rails
    hx = outer_x / 2
    hz = outer_z / 2
    ix = inner_x / 2
    iz = inner_z / 2
    # rails along X (front/back in Blender y = Unity z)
    for sy in (-1, 1):
        parts.append(cube("RailY", outer_x, FRAME_W, RAIL_H,
                          (0, sy * (iz + FRAME_W / 2), RAIL_H / 2), bevel=0.04))
    for sx in (-1, 1):
        parts.append(cube("RailX", FRAME_W, inner_z, RAIL_H,
                          (sx * (ix + FRAME_W / 2), 0, RAIL_H / 2), bevel=0.04))
    for p in parts:
        assign_mat(p, board_mat)
    board = join_selected(fname)
    export(board, fname + ".fbx")


# ================= TILES (with baked digits) =================
TILE = 0.8
TILE_H = 0.22
DIGIT_SIZE = 0.5
DIGIT_EXTRUDE = 0.022
DIGIT_BEVEL = 0.006


def add_digit(obj_name, n, digit_mat):
    """Create text digit lying on top face (local XY plane, +Z up)."""
    cu = bpy.data.curves.new(name=f"DigitCurve_{n}", type='FONT')
    cu.body = str(n)
    cu.align_x = 'CENTER'
    cu.align_y = 'CENTER'
    cu.size = DIGIT_SIZE
    cu.extrude = DIGIT_EXTRUDE
    cu.bevel_depth = DIGIT_BEVEL
    cu.bevel_resolution = 2
    cu.resolution_u = 4

    dobj = bpy.data.objects.new(f"Digit_{n}", cu)
    bpy.context.scene.collection.objects.link(dobj)
    # center on tile top, slightly embedded so it looks 'written on'
    dobj.location = (0, 0, TILE_H - 0.006)
    # spin glyph 180 deg around vertical so tops point away from the game camera (-Z side)
    dobj.rotation_euler = (0, 0, 3.141592653589793)

    # convert text -> mesh
    bpy.ops.object.select_all(action='DESELECT')
    dobj.select_set(True)
    bpy.context.view_layer.objects.active = dobj
    bpy.ops.object.convert(target='MESH')
    mesh_obj = bpy.context.active_object
    mesh_obj.name = f"DigitMesh_{n}"

    # ensure outward normals (text->mesh conversion can flip winding)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    try:
        bpy.ops.mesh.normals_make_consistent(inside=False)
    except Exception as e:
        print(f"normals_make_consistent failed: {e}")
    bpy.ops.object.mode_set(mode='OBJECT')

    # sanity: recalc outside via bmesh-free flip check not needed; assign material
    assign_mat(mesh_obj, digit_mat)
    return mesh_obj


def build_tiles():
    tile_mat = make_mat("TileMat", (0.93, 0.89, 0.80), rough=0.45)
    digit_mat = make_mat("DigitMat", (0.30, 0.25, 0.20), rough=0.55)

    for n in range(1, 10):
        clear_scene()
        # recreate materials (clear_scene wipes them)
        tile_mat = make_mat("TileMat", (0.93, 0.89, 0.80), rough=0.45)
        digit_mat = make_mat("DigitMat", (0.30, 0.25, 0.20), rough=0.55)

        tile = cube(f"Tile_{n}", TILE, TILE, TILE_H, (0, 0, TILE_H / 2), bevel=0.045)
        assign_mat(tile, tile_mat)
        digit = add_digit(f"Tile_{n}", n, digit_mat)

        board = join_selected(f"Tile_{n}")
        export(board, f"Tile_{n}.fbx")


if __name__ == "__main__":
    build_table()
    for n, bw, bh, fname in BOARDS:
        build_board(n, bw, bh, fname)
    build_tiles()
    print("=== DONE ===")
