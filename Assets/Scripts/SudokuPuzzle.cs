using System;
using System.Collections.Generic;
using UnityEngine;

// Generic N x N sudoku generator with rectangular boxes.
public static class SudokuPuzzle
{
    static readonly System.Random rng = new System.Random();

    public static void Generate(int n, int boxW, int boxH, int clues,
        out int[,] puzzle, out int[,] solution)
    {
        solution = new int[n, n];
        FillSolution(solution, 0, n, boxW, boxH);
        puzzle = (int[,])solution.Clone();

        // remove cells while keeping the puzzle unique
        var positions = new List<int>(n * n);
        for (int i = 0; i < n * n; i++) positions.Add(i);

        int target = n * n - clues;
        int removed = 0;
        int guard = 0;
        while (removed < target && guard++ < 50)
        {
            // reshuffle each pass
            for (int i = positions.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (positions[i], positions[j]) = (positions[j], positions[i]);
            }
            foreach (int pos in positions)
            {
                if (removed >= target) break;
                int r = pos / n, c = pos % n;
                if (puzzle[r, c] == 0) continue;
                int backup = puzzle[r, c];
                puzzle[r, c] = 0;
                if (CountSolutions(puzzle, n, boxW, boxH, 2) == 1)
                {
                    removed++;
                }
                else
                {
                    puzzle[r, c] = backup;
                }
            }
        }
    }

    static bool IsValid(int[,] g, int r, int c, int num, int n, int boxW, int boxH)
    {
        for (int i = 0; i < n; i++)
            if (g[r, i] == num || g[i, c] == num) return false;
        int br = (r / boxH) * boxH;
        int bc = (c / boxW) * boxW;
        for (int i = 0; i < boxH; i++)
            for (int j = 0; j < boxW; j++)
                if (g[br + i, bc + j] == num) return false;
        return true;
    }

    static bool FillSolution(int[,] g, int pos, int n, int boxW, int boxH)
    {
        if (pos == n * n) return true;
        int r = pos / n, c = pos % n;
        if (g[r, c] != 0) return FillSolution(g, pos + 1, n, boxW, boxH);

        var nums = new List<int>();
        for (int v = 1; v <= n; v++) nums.Add(v);
        for (int i = nums.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (nums[i], nums[j]) = (nums[j], nums[i]);
        }
        foreach (int num in nums)
        {
            if (IsValid(g, r, c, num, n, boxW, boxH))
            {
                g[r, c] = num;
                if (FillSolution(g, pos + 1, n, boxW, boxH)) return true;
                g[r, c] = 0;
            }
        }
        return false;
    }

    static int CountSolutions(int[,] g, int n, int boxW, int boxH, int limit)
    {
        // find first empty cell
        int er = -1, ec = -1;
        for (int r = 0; r < n && er < 0; r++)
            for (int c = 0; c < n; c++)
                if (g[r, c] == 0) { er = r; ec = c; break; }
        if (er < 0) return 1;

        int count = 0;
        for (int num = 1; num <= n && count < limit; num++)
        {
            if (IsValid(g, er, ec, num, n, boxW, boxH))
            {
                g[er, ec] = num;
                count += CountSolutions(g, n, boxW, boxH, limit - count);
                g[er, ec] = 0;
            }
        }
        return count;
    }
}
