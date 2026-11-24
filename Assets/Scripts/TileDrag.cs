using UnityEngine;

public class TileDrag : MonoBehaviour
{
    public int number;
    public bool isFixed;

    [HideInInspector] public bool isPlaced;
    [HideInInspector] public BoardCell currentCell;
    [HideInInspector] public int traySlot = -1;
    [HideInInspector] public BoardCell originCell;
    [HideInInspector] public int originSlot = -1;

    private bool dragging;
    private Vector3 grabOffset;
    private float dragY;
    private Camera cam;
    private Vector3 lastPos;

    void Start()
    {
        cam = Camera.main;
    }

    void OnMouseDown()
    {
        if (isFixed || GameManager.Instance == null
            || GameManager.Instance.CurrentState != GameManager.State.Playing) return;
        if (!dragging)
        {
            dragging = true;
            GameManager.Instance.Pickup(this);
            dragY = transform.position.y + BoardMetrics.DragLift;
            grabOffset = transform.position - MouseGroundPoint(dragY);
            lastPos = transform.position;
        }
    }

    void OnMouseDrag()
    {
        if (!dragging) return;
        Vector3 target = MouseGroundPoint(dragY) + grabOffset;
        target.y = dragY;
        // keep tile over the table area
        target.x = Mathf.Clamp(target.x, -7.4f, 7.4f);
        target.z = Mathf.Clamp(target.z, -7.5f, 6.9f);
        transform.position = target;
        lastPos = target;
        GameManager.Instance.UpdateHover(target);
    }

    void OnMouseUp()
    {
        if (!dragging) return;
        dragging = false;
        GameManager.Instance.Drop(this, lastPos);
    }

    private Vector3 MouseGroundPoint(float y)
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        float t = (y - ray.origin.y) / ray.direction.y;
        return ray.origin + ray.direction * t;
    }

    // Called by GameManager to animate/snap tile into a cell
    public void PlaceAt(Vector3 pos, BoardCell cell)
    {
        transform.position = pos;
        isPlaced = true;
        currentCell = cell;
        traySlot = -1;
    }

    public void ReturnHome(Vector3 pos, BoardCell cell, int slot)
    {
        transform.position = pos;
        isPlaced = cell != null;
        currentCell = cell;
        traySlot = slot;
    }
}
