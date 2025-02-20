using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SelectionBoxUsingCollider : MonoBehaviour
{
    // The UI Image used as the selection rectangle.
    [Header("Selection Box")]
    public RectTransform selectionRect;
    public Canvas canvas;

    [Header("Selection Pointer")]
    public RectTransform pointerRect;

    // The plane used for conversion (e.g., ground plane at y = 0).
    public Plane selectionPlane = new Plane(Vector3.up, Vector3.zero);

    // The starting screen and canvas local positions.
    private Vector2 startScreenPos;
    private Vector2 startLocalPos;

    // Track selected objects and their original colors.
    private HashSet<GameObject> currentSelectedObjects = new HashSet<GameObject>();
    private Dictionary<GameObject, Color> originalColors = new Dictionary<GameObject, Color>();

    // --- Fields for drawing the oriented overlap box gizmo ---
    private Vector3 orientedGizmoCenter = Vector3.zero;
    private Vector3 orientedGizmoHalfExtents = Vector3.zero;
    private Quaternion orientedGizmoRotation = Quaternion.identity;

    void Start()
    {
        if (this.selectionRect != null)
            this.selectionRect.gameObject.SetActive(false);
    }

    void Update()
    {
        UpdatePointerPosition();

        // On mouse button down: record starting position and enable the selection rectangle.
        if (Input.GetMouseButtonDown(0))
        {
            startScreenPos = Input.mousePosition;
            RectTransform canvasRect = this.canvas.GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, startScreenPos,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : this.canvas.worldCamera,
                out startLocalPos);

            if (this.selectionRect != null)
            {
                this.selectionRect.gameObject.SetActive(true);
                // Set pivot to top‑left so that anchoredPosition represents the top‑left corner.
                this.selectionRect.pivot = new Vector2(0, 1);
                this.selectionRect.anchoredPosition = startLocalPos;
                this.selectionRect.sizeDelta = Vector2.zero;
            }
        }

        // While dragging the mouse.
        if (Input.GetMouseButton(0))
        {
            Vector2 currentScreenPos = Input.mousePosition;
            Vector2 currentLocalPos;
            RectTransform canvasRect = this.canvas.GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, currentScreenPos,
                this.canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : this.canvas.worldCamera,
                out currentLocalPos);

            // Calculate bounds in canvas local space (works regardless of drag direction).
            float left   = Mathf.Min(startLocalPos.x, currentLocalPos.x);
            float right  = Mathf.Max(startLocalPos.x, currentLocalPos.x);
            float top    = Mathf.Max(startLocalPos.y, currentLocalPos.y);
            float bottom = Mathf.Min(startLocalPos.y, currentLocalPos.y);

            float width  = right - left;
            float height = top - bottom;

            if (this.selectionRect != null)
            {
                // With a top‑left pivot, the anchored position is the top‑left corner.
                this.selectionRect.anchoredPosition = new Vector2(left, top);
                this.selectionRect.sizeDelta = new Vector2(width, height);
            }

            // Update the oriented overlap box (gizmo) in real time.
            UpdateOrientedGizmo(currentScreenPos);
        }

        // On mouse button up: disable the selection rectangle and perform selection.
        if (Input.GetMouseButtonUp(0))
        {
            if (this.selectionRect != null)
                this.selectionRect.gameObject.SetActive(false);

            // Final update with the last mouse position.
            UpdateOrientedGizmo(Input.mousePosition);

            SelectObjects();
        }
    }

    private void UpdatePointerPosition()
    {
        if (this.pointerRect == null)
            return;

        RectTransform canvasRect = this.canvas.GetComponent<RectTransform>();
        Vector2 pointerLocalPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            Input.mousePosition,
            this.canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : this.canvas.worldCamera,
            out pointerLocalPos);

        this.pointerRect.anchoredPosition = pointerLocalPos;
    }

    /// <summary>
    /// Updates the oriented gizmo box parameters based on the current mouse position.
    /// This method computes the world-space box from the start and current screen points.
    /// </summary>
    /// <param name="currentScreenPos">The current screen position of the mouse.</param>
    private void UpdateOrientedGizmo(Vector2 currentScreenPos)
    {
        float xMin = Mathf.Min(startScreenPos.x, currentScreenPos.x);
        float xMax = Mathf.Max(startScreenPos.x, currentScreenPos.x);
        float yMin = Mathf.Min(startScreenPos.y, currentScreenPos.y);
        float yMax = Mathf.Max(startScreenPos.y, currentScreenPos.y);

        // Convert three corners of the screen rectangle to world positions using the plane.
        Vector3 bl = GetWorldPointFromScreen(new Vector2(xMin, yMin)); // bottom-left
        Vector3 br = GetWorldPointFromScreen(new Vector2(xMax, yMin)); // bottom-right
        Vector3 tl = GetWorldPointFromScreen(new Vector2(xMin, yMax)); // top-left

        // Calculate the center of the selection box.
        Vector3 centerWorld = bl + 0.5f * ((br - bl) + (tl - bl));

        // Calculate the edges of the projected rectangle.
        Vector3 horizontal = br - bl; // width direction
        Vector3 vertical = tl - bl;   // height direction

        // Get half-extents along these directions.
        float halfWidth = horizontal.magnitude * 0.5f;
        float halfHeight = vertical.magnitude * 0.5f;
        // Use a small thickness for the box along the normal (up) direction.
        float thickness = 0.1f;

        // Compute local axes for the overlap box.
        Vector3 right = horizontal.normalized;
        Vector3 forward = (vertical - Vector3.Dot(vertical, right) * right).normalized;
        Vector3 up = Vector3.Cross(right, forward);

        // Create a rotation from these axes.
        Quaternion boxRotation = Quaternion.LookRotation(forward, up);

        // Build the half-extents vector (x: halfWidth, y: thickness, z: halfHeight).
        Vector3 boxHalfExtents = new Vector3(halfWidth, thickness, halfHeight);

        // Update the gizmo parameters.
        orientedGizmoCenter = centerWorld;
        orientedGizmoHalfExtents = boxHalfExtents;
        orientedGizmoRotation = boxRotation;
    }

    /// <summary>
    /// Computes the selection box parameters, uses an oriented Physics.OverlapBox to find objects within the selection,
    /// and updates object colors: selected objects become blue while deselected objects revert to their original color.
    /// Also stores the oriented box parameters for gizmo drawing.
    /// </summary>
    private void SelectObjects()
    {
        // Use Physics.OverlapBox with the current gizmo parameters.
        Collider[] hits = Physics.OverlapBox(orientedGizmoCenter, orientedGizmoHalfExtents, orientedGizmoRotation);
        HashSet<GameObject> newSelected = new HashSet<GameObject>();
        foreach (Collider col in hits)
        {
            newSelected.Add(col.gameObject);
        }

        // Deselect objects that were previously selected but are not in the new selection.
        List<GameObject> toDeselect = new List<GameObject>();
        foreach (GameObject obj in currentSelectedObjects)
        {
            if (!newSelected.Contains(obj))
            {
                Renderer rend = obj.GetComponent<Renderer>();
                if (rend != null && originalColors.ContainsKey(obj))
                {
                    // Revert to the original color.
                    rend.material.color = originalColors[obj];
                }
                toDeselect.Add(obj);
            }
        }
        foreach (GameObject obj in toDeselect)
        {
            currentSelectedObjects.Remove(obj);
            originalColors.Remove(obj);
        }

        // For each newly selected object, change its color to blue.
        foreach (GameObject obj in newSelected)
        {
            if (!currentSelectedObjects.Contains(obj))
            {
                Renderer rend = obj.GetComponent<Renderer>();
                if (rend != null)
                {
                    // Store the original color if not already stored.
                    if (!originalColors.ContainsKey(obj))
                    {
                        originalColors[obj] = rend.material.color;
                    }
                    rend.material.color = Color.blue;
                }
                currentSelectedObjects.Add(obj);
            }
        }

        // (Optional) Debug output.
        foreach (GameObject obj in currentSelectedObjects)
        {
            Debug.Log("Selected object: " + obj.name);
        }
    }

    /// <summary>
    /// Converts a screen point to a world point by intersecting a ray (from Camera.main)
    /// with the defined selection plane.
    /// </summary>
    private Vector3 GetWorldPointFromScreen(Vector2 screenPoint)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPoint);
        float enter;
        if (selectionPlane.Raycast(ray, out enter))
        {
            return ray.GetPoint(enter);
        }
        return Vector3.zero;
    }

    // Draw the oriented overlap box as a green wireframe in the Scene view while dragging.
    void OnDrawGizmos()
    {
        // Only draw when the selection rectangle is active (i.e., during dragging).
        if (Application.isPlaying && selectionRect != null && selectionRect.gameObject.activeSelf)
        {
            Gizmos.color = Color.green;
            Matrix4x4 cubeTransform = Matrix4x4.TRS(orientedGizmoCenter, orientedGizmoRotation, Vector3.one);
            Gizmos.matrix = cubeTransform;
            // Draw a wire cube with full extents (half-extents * 2).
            Gizmos.DrawWireCube(Vector3.zero, orientedGizmoHalfExtents * 2f);
            // Reset the Gizmos matrix.
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}