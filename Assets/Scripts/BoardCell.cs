using UnityEngine;

public class BoardCell : MonoBehaviour
{
    public int row;
    public int col;
    public bool isFixed;
    public bool isOccupied;
    public TileDrag tile;

    [HideInInspector] public Renderer highlight;
    public Material hlValid;
    public Material hlInvalid;

    public Vector3 TopCenter => transform.position;

    public void SetOccupied(TileDrag t)
    {
        tile = t;
        isOccupied = true;
    }

    public void Clear()
    {
        tile = null;
        isOccupied = false;
    }

    public void ShowHighlight(bool valid)
    {
        if (highlight == null) return;
        highlight.sharedMaterial = valid ? hlValid : hlInvalid;
        highlight.enabled = true;
    }

    public void HideHighlight()
    {
        if (highlight != null) highlight.enabled = false;
    }
}
