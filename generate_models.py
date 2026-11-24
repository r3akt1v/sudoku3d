"""
Blender script: Generate Sudoku 3D board and number tiles.
Run with: blender --background --python generate_models.py
"""
import bpy
import bmesh
import os
import sys

OUTPUT_DIR = os.path.join(os.path.dirname(bpy.data.filepath) if bpy.data.filepath else os.getcwd(), "Models")
os.makedirs(OUTPUT_DIR, exist_ok=True)


def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for block in list(bpy.data.meshes):
        bpy.data.meshes.remove(block)
    for block in list(bpy.data.materials):
        bpy.data.materials.remove(block)


def add_bevel(obj, width=0.02, segments=2):
    # Use data API instead of bpy.ops for headless reliability
    mod = obj.modifiers.new(name="Bevel", type='BEVEL')
    mod.width = width
    mod.segments = segments
    mod.limit_method = 'ANGLE'
    # Apply modifier via data API
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=mod.name)


def create_board_cell(name, x, y, size=1.0, height=0.15):
    """Create a single cell of the Sudoku board."""
    bpy.ops.mesh.primitive_cube_add(size=size, location=(x, y, 0))
    cell = bpy.context.active_object
    cell.name = f"Cell_{name}"
    cell.scale.z = height / size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    add_bevel(cell, width=0.03, segments=2)
    return cell


def create_board_frame(size=11.0, frame_width=0.5, height=0.2):
    """Create the outer frame of the board."""
    half = size / 2
    fw = frame_width
    
    # Bottom plate
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, -height/2))
    plate = bpy.context.active_object
    plate.name = "BoardPlate"
    plate.scale = (size, size, height)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    add_bevel(plate, width=0.05, segments=3)
    return plate


def create_sudoku_board():
    """Create full 9x9 Sudoku board with 3x3 sector divisions."""
    cell_size = 1.0
    gap = 0.05
    sector_gap = 0.15
    total_per_row = 9 * cell_size + 8 * gap + 2 * sector_gap
    
    cells = []
    for row in range(9):
        for col in range(9):
            # Position with sector gaps
            x = (col - 4) * (cell_size + gap)
            y = (row - 4) * (cell_size + gap)
            # Add extra gap between 3x3 sectors
            if col >= 3 and col < 6:
                pass
            if col == 2 or col == 5:
                x += sector_gap
            if row == 2 or row == 5:
                y += sector_gap
            
            cell = create_board_cell(f"{row}_{col}", x, y, cell_size)
            cells.append(cell)
    
    # Join all cells into one object
    bpy.ops.object.select_all(action='DESELECT')
    for c in cells:
        c.select_set(True)
    bpy.context.view_layer.objects.active = cells[0]
    bpy.ops.object.join()
    board = bpy.context.active_object
    board.name = "SudokuBoard"
    
    # Create outer frame
    frame = create_board_frame(total_per_row + 1.0)
    
    # Join frame with board
    bpy.ops.object.select_all(action='DESELECT')
    board.select_set(True)
    frame.select_set(True)
    bpy.context.view_layer.objects.active = frame
    bpy.ops.object.join()
    board = bpy.context.active_object
    board.name = "SudokuBoard"
    
    return board


def create_number_tile(number, size=0.8, height=0.2):
    """Create a tile with a raised number on top."""
    # Tile base
    bpy.ops.mesh.primitive_cube_add(size=size, location=(0, 0, height/2))
    tile = bpy.context.active_object
    tile.name = f"Tile_{number}"
    tile.scale.z = height / size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    add_bevel(tile, width=0.04, segments=3)
    
    # Add text on top
    bpy.ops.object.text_add(location=(0, 0, height + 0.001))
    text_obj = bpy.context.active_object
    text_obj.data.body = str(number)
    text_obj.data.size = size * 0.55
    text_obj.data.align_x = 'CENTER'
    text_obj.data.align_y = 'CENTER'
    text_obj.data.extrude = 0.05
    text_obj.name = f"TileText_{number}"
    
    # Convert text to mesh
    bpy.ops.object.convert(target='MESH')
    
    # Join text with tile
    bpy.ops.object.select_all(action='DESELECT')
    tile.select_set(True)
    text_obj.select_set(True)
    bpy.context.view_layer.objects.active = tile
    bpy.ops.object.join()
    
    tile.name = f"Tile_{number}"
    return tile


def create_material(name, color, roughness=0.5, metallic=0.0):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    # Find Principled BSDF by type (robust across Blender versions)
    bsdf = None
    for node in mat.node_tree.nodes:
        if node.type == 'BSDF_PRINCIPLED':
            bsdf = node
            break
    if bsdf is None:
        # Create one if missing
        bsdf = mat.node_tree.nodes.new(type='ShaderNodeBsdfPrincipled')
        output = mat.node_tree.nodes.get("Material Output")
        if output is None:
            output = mat.node_tree.nodes.new(type='ShaderNodeOutputMaterial')
        mat.node_tree.links.new(bsdf.outputs[0], output.inputs[0])
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return mat


def export_fbx(obj, filepath):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=filepath, use_selection=True, mesh_smooth_type='FACE')


def main():
    clear_scene()
    
    # --- Create Sudoku Board ---
    print("Creating Sudoku board...")
    board = create_sudoku_board()
    
    # Apply wood-like material
    board_mat = create_material("BoardMaterial", (0.45, 0.32, 0.20), roughness=0.7)
    board.data.materials.append(board_mat)
    
    export_path = os.path.join(OUTPUT_DIR, "SudokuBoard.fbx")
    export_fbx(board, export_path)
    print(f"Exported board to: {export_path}")
    
    # --- Create Number Tiles (1-9) ---
    print("Creating number tiles 1-9...")
    for n in range(1, 10):
        clear_scene()
        tile = create_number_tile(n)
        
        # Tile material (light wood/plastic)
        tile_mat = create_material(f"TileMaterial_{n}", (0.9, 0.82, 0.65), roughness=0.4)
        tile.data.materials.append(tile_mat)
        
        export_path = os.path.join(OUTPUT_DIR, f"Tile_{n}.fbx")
        export_fbx(tile, export_path)
        print(f"Exported tile {n} to: {export_path}")
    
    # --- Create empty cell placeholder ---
    clear_scene()
    bpy.ops.mesh.primitive_cube_add(size=0.7, location=(0, 0, 0.1))
    placeholder = bpy.context.active_object
    placeholder.name = "CellHighlight"
    placeholder.scale.z = 0.05
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    add_bevel(placeholder, width=0.02, segments=2)
    
    export_path = os.path.join(OUTPUT_DIR, "CellHighlight.fbx")
    export_fbx(placeholder, export_path)
    print(f"Exported cell highlight to: {export_path}")
    
    print("\n=== All models generated successfully! ===")
    print(f"Output directory: {OUTPUT_DIR}")


if __name__ == "__main__":
    main()
