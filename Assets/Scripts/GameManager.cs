using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public enum State { LevelSelect, Playing, Paused, Won }

    [Header("Prefabs")]
    public TileDrag[] tilePrefabs;      // index 0 -> number 1 ... (uses first N)
    public GameObject board3, board6, board9;

    [Header("Materials")]
    public Material hlValid, hlInvalid, boardMaterial;

    // board placement (matches scene builder)
    const float TableTopY = 2.2f;
    const float BoardBaseY = TableTopY + 0.01f;
    const float BoardZ = 1.6f;
    // camera base framing
    static readonly Vector3 CamBasePos = new Vector3(0f, 11.8f, -14.2f);
    static readonly Vector3 CamTarget = new Vector3(0f, 1.2f, 0.9f);

    public State CurrentState { get; private set; } = State.LevelSelect;
    public int Moves { get; private set; }
    public int HintsLeft { get; private set; }
    public string LevelLabel { get; private set; } = "";
    public bool IsGameOver => CurrentState == State.Won;

    private int levelSize = 9;
    private BoardCell[,] cells;
    private int[,] grid;
    private int[,] solution;
    private readonly List<TileDrag> tiles = new List<TileDrag>();
    private Transform tilesRoot;
    private Transform cellsRoot;
    private GameObject boardInstance;

    private TileDrag draggingTile;
    private BoardCell lastHighlight;
    private int lastHighlightValid = -1;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        tilesRoot = new GameObject("Tiles").transform;
        tilesRoot.SetParent(transform, false);
    }

    void Start()
    {
        BoardMetrics.SetLevel(9);
        UIManager.Instance?.ShowLevelSelect();
    }

    void Update()
    {
        if (CurrentState == State.Playing && Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
        else if (CurrentState == State.Paused && Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
    }

    // ---------- Level flow ----------

    public void SelectLevel(int size)
    {
        levelSize = size;
        BoardMetrics.SetLevel(size);
        LevelLabel = $"{size}×{size}";
        CurrentState = State.Playing;
        Time.timeScale = 1f;
        BuildLevel();
        NewGame();
        UIManager.Instance?.ShowHUD();
        AudioManager.Instance?.PlayMusic();
    }

    public void TogglePause()
    {
        if (CurrentState == State.Playing)
        {
            CurrentState = State.Paused;
            Time.timeScale = 0f;
            AudioManager.Instance?.PauseMusic();
            UIManager.Instance?.ShowPause(true);
        }
        else if (CurrentState == State.Paused)
        {
            CurrentState = State.Playing;
            Time.timeScale = 1f;
            AudioManager.Instance?.ResumeMusic();
            UIManager.Instance?.ShowPause(false);
        }
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        CurrentState = State.Playing;
        UIManager.Instance?.ShowPause(false);
        UIManager.Instance?.HideWin();
        NewGame();
        UIManager.Instance?.ShowHUD();
        AudioManager.Instance?.ResumeMusic();
    }

    public void QuitToMenu()
    {
        Time.timeScale = 1f;
        CurrentState = State.LevelSelect;
        ClearDynamicObjects();
        UIManager.Instance?.ShowLevelSelect();
        AudioManager.Instance?.PauseMusic();
    }

    // ---------- Level construction ----------

    void BuildLevel()
    {
        ClearDynamicObjects();

        // board model
        GameObject prefab = levelSize == 3 ? board3 : levelSize == 6 ? board6 : board9;
        if (prefab != null)
        {
            boardInstance = Instantiate(prefab);
            boardInstance.name = $"Board_{levelSize}x{levelSize}";
            // align bottom to table, center on board anchor
            var b = ComputeLocalBounds(boardInstance);
            boardInstance.transform.position = new Vector3(
                -b.center.x,
                BoardBaseY - b.min.y,
                BoardZ - b.center.z);
            foreach (var r in boardInstance.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = boardMaterial;
        }

        // cells
        cellsRoot = new GameObject("Cells").transform;
        cellsRoot.SetParent(transform, false);
        cells = new BoardCell[BoardMetrics.N, BoardMetrics.N];

        var quadProto = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quadProto.name = "HL_Proto";

        for (int r = 0; r < BoardMetrics.N; r++)
        {
            for (int c = 0; c < BoardMetrics.N; c++)
            {
                var cellGo = new GameObject($"Cell_{r}_{c}");
                cellGo.transform.SetParent(cellsRoot, false);
                cellGo.transform.position = new Vector3(
                    BoardMetrics.OffsetX(c),
                    BoardBaseY + BoardMetrics.CellTopLocalY,
                    BoardZ + BoardMetrics.OffsetZ(r));

                var cell = cellGo.AddComponent<BoardCell>();
                cell.row = r;
                cell.col = c;
                cell.hlValid = hlValid;
                cell.hlInvalid = hlInvalid;

                var hl = Instantiate(quadProto, cellGo.transform);
                hl.name = "HL";
                hl.transform.localPosition = new Vector3(0, 0.015f, 0);
                hl.transform.localRotation = Quaternion.Euler(-90, 0, 0);
                hl.transform.localScale = new Vector3(
                    BoardMetrics.CellSize - 0.05f, BoardMetrics.CellSize - 0.05f, 1f);
                var hlRend = hl.GetComponent<MeshRenderer>();
                hlRend.sharedMaterial = hlValid;
                hlRend.enabled = false;
                var hlCol = hl.GetComponent<MeshCollider>();
                if (hlCol != null) Destroy(hlCol);
                cell.highlight = hlRend;

                cells[r, c] = cell;
            }
        }
        Destroy(quadProto);

        // resize tray pad to fit this level's slot grid
        var pad = GameObject.Find("TrayPad");
        if (pad != null)
        {
            float w = BoardMetrics.TrayColsL * BoardMetrics.TrayPitchX + 0.7f;
            float d = BoardMetrics.TrayRowsL * BoardMetrics.TrayPitchZ + 0.7f;
            float zNear = BoardMetrics.TrayFirstZ + 0.45f;   // tile half-depth + margin
            float zFar = BoardMetrics.TrayLastZ - 0.45f;
            float zc = (zNear + zFar) * 0.5f;
            pad.transform.localScale = new Vector3(w, 0.07f, d);
            pad.transform.position = new Vector3(0f, TableTopY + 0.035f, zc);
        }

        // camera framing per board size
        FrameCamera();
    }

    void FrameCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        float factor = Mathf.Clamp(BoardMetrics.BoardSpan / 10.34f, 0.55f, 1f);
        Vector3 offset = (CamBasePos - CamTarget) * factor;
        cam.transform.position = CamTarget + offset;
        cam.transform.rotation = Quaternion.LookRotation(CamTarget - cam.transform.position);
    }

    void ClearDynamicObjects()
    {
        foreach (var t in tiles) if (t != null) Destroy(t.gameObject);
        tiles.Clear();
        if (cellsRoot != null) Destroy(cellsRoot.gameObject);
        cellsRoot = null;
        cells = null;
        if (boardInstance != null) Destroy(boardInstance);
        boardInstance = null;
        draggingTile = null;
        lastHighlight = null;
    }

    static Bounds ComputeLocalBounds(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
        Bounds b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);
        return b;
    }

    // ---------- Game ----------

    public void NewGame()
    {
        int n = BoardMetrics.N;
        grid = new int[n, n];
        foreach (var t in tiles) if (t != null) Destroy(t.gameObject);
        tiles.Clear();
        if (cells != null)
            foreach (var cell in cells)
                if (cell != null) { cell.Clear(); cell.HideHighlight(); cell.isFixed = false; }

        CurrentState = State.Playing;
        Moves = 0;
        HintsLeft = BoardMetrics.MaxHints;
        draggingTile = null;
        lastHighlight = null;

        int[,] puzzle;
        SudokuPuzzle.Generate(n, BoardMetrics.RuleBoxW, BoardMetrics.RuleBoxH, BoardMetrics.Clues,
            out puzzle, out solution);

        int[] remaining = new int[n + 1];
        for (int num = 1; num <= n; num++)
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++)
                    if (solution[r, c] == num) remaining[num]++;

        for (int r = 0; r < n; r++)
            for (int c = 0; c < n; c++)
                if (puzzle[r, c] != 0)
                {
                    var cell = cells[r, c];
                    var tile = SpawnTile(puzzle[r, c], cell.TopCenter, cell);
                    tile.isFixed = true;
                    cell.isFixed = true;
                    cell.SetOccupied(tile);
                    grid[r, c] = puzzle[r, c];
                    remaining[puzzle[r, c]]--;
                }

        int slot = 0;
        for (int num = 1; num <= n; num++)
            for (int k = 0; k < remaining[num]; k++)
            {
                Vector3 pos = BoardMetrics.TraySlotPos(slot);
                var tile = SpawnTile(num, pos, null);
                tile.traySlot = slot;
                slot++;
            }

        UIManager.Instance?.RefreshHUD();
    }

    private TileDrag SpawnTile(int number, Vector3 pos, BoardCell cell)
    {
        var prefab = tilePrefabs[number - 1];
        var tile = Instantiate(prefab, pos, Quaternion.identity, tilesRoot);
        tile.number = number;
        tile.isFixed = false;
        tile.isPlaced = cell != null;
        tile.currentCell = cell;
        tiles.Add(tile);
        if (cell != null) cell.SetOccupied(tile);
        return tile;
    }

    // ---------- Hint ----------

    public void UseHint()
    {
        if (CurrentState != State.Playing || HintsLeft <= 0 || IsGameOver) return;
        int n = BoardMetrics.N;
        var options = new List<Vector2Int>();
        for (int r = 0; r < n; r++)
            for (int c = 0; c < n; c++)
                if (grid[r, c] == 0) options.Add(new Vector2Int(r, c));
        if (options.Count == 0) return;

        var pick = options[Random.Range(0, options.Count)];
        int r0 = pick.x, c0 = pick.y;
        int num = solution[r0, c0];

        var cell = cells[r0, c0];
        var tile = SpawnTile(num, cell.TopCenter, cell);
        tile.isFixed = true;
        cell.isFixed = true;
        cell.SetOccupied(tile);
        grid[r0, c0] = num;
        HintsLeft--;
        Moves++;

        AudioManager.Instance?.PlaySfx("hint");
        UIManager.Instance?.RefreshHUD();
        CheckWin();
    }

    // ---------- Drag & drop ----------

    public void Pickup(TileDrag tile)
    {
        draggingTile = tile;
        if (tile.currentCell != null)
        {
            tile.originCell = tile.currentCell;
            tile.originSlot = -1;
            grid[tile.currentCell.row, tile.currentCell.col] = 0;
            tile.currentCell.Clear();
        }
        else
        {
            tile.originCell = null;
            tile.originSlot = tile.traySlot;
        }
        tile.isPlaced = false;
        tile.currentCell = null;
        AudioManager.Instance?.PlaySfx("pickup");
    }

    public void UpdateHover(Vector3 pos)
    {
        if (CurrentState != State.Playing) return;
        var cell = NearestCell(pos);
        int valid = -1;
        if (cell != null && !cell.isFixed && !cell.isOccupied && IsValidMove(cell.row, cell.col, draggingTile.number))
            valid = 1;
        else if (cell != null)
            valid = 0;

        if (cell != lastHighlight || valid != lastHighlightValid)
        {
            if (lastHighlight != null) lastHighlight.HideHighlight();
            lastHighlight = cell;
            lastHighlightValid = valid;
            if (cell != null) cell.ShowHighlight(valid == 1);
        }
    }

    public void Drop(TileDrag tile, Vector3 pos)
    {
        HideAllHighlights();
        draggingTile = null;

        if (CurrentState != State.Playing)
        {
            ReturnTileToOrigin(tile);
            return;
        }

        var cell = NearestCell(pos);
        bool placed = false;
        int n = BoardMetrics.N;

        if (cell != null && !cell.isFixed)
        {
            if (cell.isOccupied)
            {
                var occupant = cell.tile;
                if (occupant != null && !occupant.isFixed && occupant != tile
                    && occupant.number != tile.number && IsValidMove(cell.row, cell.col, tile.number))
                {
                    grid[cell.row, cell.col] = tile.number;
                    cell.SetOccupied(tile);
                    tile.PlaceAt(cell.TopCenter, cell);
                    placed = true;

                    if (tile.originCell != null && !tile.originCell.isOccupied && !tile.originCell.isFixed)
                    {
                        occupant.PlaceAt(tile.originCell.TopCenter, tile.originCell);
                        tile.originCell.SetOccupied(occupant);
                        grid[tile.originCell.row, tile.originCell.col] = occupant.number;
                        occupant.isPlaced = true;
                    }
                    else
                    {
                        int slot = FindFreeTraySlot(occupant);
                        occupant.ReturnHome(BoardMetrics.TraySlotPos(slot), null, slot);
                        occupant.isPlaced = false;
                        occupant.currentCell = null;
                    }
                }
            }
            else if (IsValidMove(cell.row, cell.col, tile.number))
            {
                grid[cell.row, cell.col] = tile.number;
                cell.SetOccupied(tile);
                tile.PlaceAt(cell.TopCenter, cell);
                placed = true;
            }
        }

        if (placed)
        {
            Moves++;
            tile.isPlaced = true;
            AudioManager.Instance?.PlaySfx("place");
            UIManager.Instance?.RefreshHUD();
            CheckWin();
        }
        else
        {
            AudioManager.Instance?.PlaySfx("error");
            ReturnTileToOrigin(tile);
        }
    }

    private void ReturnTileToOrigin(TileDrag tile)
    {
        if (tile.originCell != null && !tile.originCell.isOccupied && !tile.originCell.isFixed)
        {
            var cell = tile.originCell;
            grid[cell.row, cell.col] = tile.number;
            tile.PlaceAt(cell.TopCenter, cell);
            cell.SetOccupied(tile);
            tile.isPlaced = true;
        }
        else
        {
            int slot = (tile.originSlot >= 0 && !TraySlotUsed(tile.originSlot, tile))
                ? tile.originSlot
                : FindFreeTraySlot(tile);
            tile.ReturnHome(BoardMetrics.TraySlotPos(slot), null, slot);
            tile.isPlaced = false;
            tile.currentCell = null;
        }
    }

    private bool TraySlotUsed(int slot, TileDrag self)
    {
        foreach (var t in tiles)
            if (t != self && t.traySlot == slot && !t.isPlaced) return true;
        return false;
    }

    private int FindFreeTraySlot(TileDrag self)
    {
        var used = new HashSet<int>();
        foreach (var t in tiles)
            if (t != self && t.traySlot >= 0 && !t.isPlaced) used.Add(t.traySlot);
        for (int i = 0; i < BoardMetrics.TraySlotCount; i++)
            if (!used.Contains(i)) return i;
        return 0;
    }

    private BoardCell NearestCell(Vector3 pos)
    {
        if (cells == null) return null;
        BoardCell best = null;
        float bestDist = BoardMetrics.SnapRadius;
        int n = BoardMetrics.N;
        for (int r = 0; r < n; r++)
            for (int c = 0; c < n; c++)
            {
                var cell = cells[r, c];
                if (cell == null) continue;
                float dx = pos.x - cell.transform.position.x;
                float dz = pos.z - cell.transform.position.z;
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = cell;
                }
            }
        return best;
    }

    private void HideAllHighlights()
    {
        if (lastHighlight != null) lastHighlight.HideHighlight();
        lastHighlight = null;
        lastHighlightValid = -1;
    }

    // ---------- Rules ----------

    public bool IsValidMove(int row, int col, int num)
    {
        int n = BoardMetrics.N;
        for (int i = 0; i < n; i++)
            if (grid[row, i] == num || grid[i, col] == num) return false;
        int br = (row / BoardMetrics.RuleBoxH) * BoardMetrics.RuleBoxH;
        int bc = (col / BoardMetrics.RuleBoxW) * BoardMetrics.RuleBoxW;
        for (int i = 0; i < BoardMetrics.RuleBoxH; i++)
            for (int j = 0; j < BoardMetrics.RuleBoxW; j++)
                if (grid[br + i, bc + j] == num) return false;
        return true;
    }

    private void CheckWin()
    {
        int n = BoardMetrics.N;
        for (int r = 0; r < n; r++)
            for (int c = 0; c < n; c++)
                if (grid[r, c] == 0) return;
        CurrentState = State.Won;
        draggingTile = null;
        HideAllHighlights();
        AudioManager.Instance?.PlaySfx("win");
        UIManager.Instance?.ShowWin(Moves);
        Debug.Log("SUDOKU SOLVED!");
    }
}
