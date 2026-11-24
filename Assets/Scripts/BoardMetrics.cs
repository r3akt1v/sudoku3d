using UnityEngine;

// Shared layout constants. MUST match generate_models_v3.py.
public static class BoardMetrics
{
    // ---- Level config (set by SetLevel) ----
    public static int N = 9;        // grid size
    public static int BoxW = 3;     // layout box width in columns (sector gaps)
    public static int BoxH = 3;     // layout box height in rows
    public static int RuleBoxW = 3; // rule box width (validation)
    public static int RuleBoxH = 3; // rule box height
    public static int Clues = 42;   // givens on start
    public const int MaxHints = 3;
    public static int Digits => N;

    public const float CellSize = 0.9f;
    public const float Gap = 0.07f;
    public const float Sector = 0.24f;
    public static float Pitch => CellSize + Gap; // 0.97
    public const float FrameW = 0.6f;
    public const float CellTopLocalY = 0.34f;    // top of cells above board origin

    public const float SnapRadius = 0.62f;
    public const float DragLift = 0.85f;
    public const float TileTopY = 0.24f;

    // Tray layout (per-level, anchored under the board front edge)
    public const float TrayPadTopY = 2.27f;
    public const float TrayPitchX = 0.95f;
    public const float TrayPitchZ = 0.95f;
    public const float TrayGapFromBoard = 0.9f;
    public const float BoardCenterZ = 1.6f;
    public static int TrayColsL = 15;
    public static int TrayRowsL = 3;

    public static float TrayFirstZ => BoardCenterZ - OuterZ * 0.5f - TrayGapFromBoard;
    public static float TrayLastZ => TrayFirstZ - (TrayRowsL - 1) * TrayPitchZ;
    public static int TraySlotCount => TrayColsL * TrayRowsL;

    public static void SetLevel(int size)
    {
        switch (size)
        {
            case 3: N = 3; BoxW = 3; BoxH = 3; RuleBoxW = 3; RuleBoxH = 1; Clues = 6; TrayColsL = 3; TrayRowsL = 1; break;
            case 6: N = 6; BoxW = 2; BoxH = 3; RuleBoxW = 2; RuleBoxH = 3; Clues = 20; TrayColsL = 8; TrayRowsL = 2; break;
            default: N = 9; BoxW = 3; BoxH = 3; RuleBoxW = 3; RuleBoxH = 3; Clues = 42; TrayColsL = 15; TrayRowsL = 3; break;
        }
    }

    static float AxisRaw(int i, int boxDim) => i * Pitch + Sector * (i / boxDim);
    static float AxisCenter(int boxDim) => AxisRaw(N - 1, boxDim) * 0.5f;

    // centered offset of cell index i along an axis (uses current N)
    public static float AxisOffset(int i, int boxDim)
        => AxisRaw(i, boxDim) - AxisCenter(boxDim);

    public static float OffsetX(int c) => AxisOffset(c, BoxW);
    public static float OffsetZ(int r) => AxisOffset(r, BoxH);

    public static float InnerX => AxisRaw(N - 1, BoxW) + CellSize;
    public static float InnerZ => AxisRaw(N - 1, BoxH) + CellSize;
    public static float OuterX => InnerX + 2f * FrameW;
    public static float OuterZ => InnerZ + 2f * FrameW;
    public static float BoardSpan => Mathf.Max(OuterX, OuterZ);

    public static Vector3 TraySlotPos(int index)
    {
        int col = index % TrayColsL;
        int row = index / TrayColsL;
        float x = (col - (TrayColsL - 1) * 0.5f) * TrayPitchX;
        float z = TrayFirstZ - row * TrayPitchZ;
        return new Vector3(x, TrayPadTopY, z);
    }
}
