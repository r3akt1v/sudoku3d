"""
Blender script v2: Table + symmetric Sudoku board + plain number tiles.
Run: blender --background --python generate_models_v2.py
Output: FBX files into Assets/Models
"""
import bpy
import os

OUT_DIR = "/Users/aleksandrsmirnov/Documents/sudoku/Sudoku3D/Assets/Models"
os.makedirs(OUT_DIR, exist_ok=True)

# ---- Board layout constants (MUST match BoardMetrics.cs) ----
CELL = 0.9
GAP = 0.07
SECTOR = 0.24
PITCH = CELL + GAP          # 0.97
N = 9

def axis_off(i):
    return i * PITCH + (SECTOR if i >= 3 else 0.0) + (SECTOR if i >= 6 else 0.0)

CENTER = axis_off(8) / 2.0   # 4.12
INNER = axis_off(8) + CELL   # 9.14  (inner span of cells)
FRAME_W = 0.6
PLATE_T = 0.22
CELL_H = 0.12
RAIL_H = 0.42
OUTER = INNER + 2 * FRAME_W  # 10.34


def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for block in list(bpy.data.meshes):
        bpy.data.meshes.remove(block)
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
    for o in bpy.context.scene.objects:
        if o.type == 'MESH':
            o.select_set(True)
    bpy.context.view_layer.objects.active = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
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
    # legs: 0.55 x 0.55 x 1.7, corners inset 1.1
    lx = 17.0 / 2 - 1.1
    ly = 16.0 / 2 - 1.1
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(cube("Leg", 0.55, 0.55, 1.7, (sx * lx, sy * ly, 0.85), bevel=0.05))
    # top slab 17 x 16 x 0.5, z 1.7..2.2
    parts.append(cube("TableTop", 17.0, 16.0, 0.5, (0, 0, 1.95), bevel=0.06))
    for p in parts:
        assign_mat(p, wood)
    table = join_selected("Table")
    export(table, "Table.fbx")


# ================= BOARD =================
def build_board():
    clear_scene()
    board_mat = make_mat("BoardMat", (0.55, 0.62, 0.56), rough=0.65)

    parts = []
    # base plate: OUTER x OUTER x PLATE_T, bottom at z=0
    parts.append(cube("Plate", OUTER, OUTER, PLATE_T, (0, 0, PLATE_T / 2), bevel=0.04))

    # 81 cells
    for r in range(N):
        for c in range(N):
            x = axis_off(c) - CENTER
            y = axis_off(r) - CENTER
            parts.append(cube(f"C{r}_{c}", CELL, CELL, CELL_H,
                              (x, y, PLATE_T + CELL_H / 2), bevel=0.025))

    # frame rails (4), height RAIL_H, width FRAME_W, outer size OUTER
    half_o = OUTER / 2
    half_i = INNER / 2
    rail_len = OUTER
    # north/south rails (along X)
    for sy in (-1, 1):
        parts.append(cube("RailY", rail_len, FRAME_W, RAIL_H,
                          (0, sy * (half_i + FRAME_W / 2), RAIL_H / 2), bevel=0.04))
    # east/west rails (along Y), spanning the inner region between Y rails
    for sx in (-1, 1):
        parts.append(cube("RailX", FRAME_W, INNER, RAIL_H,
                          (sx * (half_i + FRAME_W / 2), 0, RAIL_H / 2), bevel=0.04))
    for p in parts:
        assign_mat(p, board_mat)
    board = join_selected("SudokuBoard")
    export(board, "SudokuBoard.fbx")


# ================= TILES =================
TILE = 0.8
TILE_H = 0.22

def build_tiles():
    for n in range(1, 10):
        clear_scene()
        mat = make_mat(f"TileMat{n}", (0.93, 0.89, 0.80), rough=0.45)
        tile = cube(f"Tile_{n}", TILE, TILE, TILE_H, (0, 0, TILE_H / 2), bevel=0.045)
        assign_mat(tile, mat)
        export(tile, f"Tile_{n}.fbx")


if __name__ == "__main__":
    build_table()
    build_board()
    build_tiles()
    print("=== DONE ===")
