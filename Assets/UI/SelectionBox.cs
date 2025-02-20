using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SelectionBox : MonoBehaviour
{
    // The UI Image used as the selection rectangle.
    [Header("Selection Box")]
    public RectTransform selectionRect;
    public Canvas canvas;

    [Header("Selection Pointer")]
    public RectTransform pointerRect;

    // The starting screen and canvas local positions.
    private Vector2 startScreenPos;
    private Vector2 startLocalPos;

    // Track selected objects and their original colors.
    private HashSet<GameObject> currentSelectedObjects = new HashSet<GameObject>();
    private Dictionary<GameObject, Color> originalColors = new Dictionary<GameObject, Color>();

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
        }

        // On mouse button up: disable the selection rectangle and perform selection.
        if (Input.GetMouseButtonUp(0))
        {
            if (this.selectionRect != null)
                this.selectionRect.gameObject.SetActive(false);

            // Select visible game objects that are within rect bounds.
            SelectVisibleObjects();
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
    /// Selects visible objects whose screen position is within the selection rectangle.
    /// Selected objects have their color changed to light green,
    /// while objects no longer selected revert to their original color.
    /// </summary>
    private void SelectVisibleObjects()
    {
        // Get the ending screen position.
        Vector2 endScreenPos = Input.mousePosition;

        // Determine the min and max corners of the rectangle.
        Vector2 minScreen = Vector2.Min(startScreenPos, endScreenPos);
        Vector2 maxScreen = Vector2.Max(startScreenPos, endScreenPos);

        // Get the appropriate camera.
        Camera cam = (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            ? Camera.main : canvas.worldCamera;

        // Build a new set for the current selection.
        HashSet<GameObject> newSelectedObjects = new HashSet<GameObject>();

        // Iterate through visible objects (using a VisibilityTracker for example).
        foreach (GameObject obj in VisibilityTracker.VisibleObjects)
        {
            // Convert the object's world position to screen space.
            Vector3 screenPos = cam.WorldToScreenPoint(obj.transform.position);

            // Check that the object is in front of the camera (screenPos.z > 0)
            // and that its screen position falls within the selection bounds.
            if (screenPos.z > 0 &&
                screenPos.x >= minScreen.x && screenPos.x <= maxScreen.x &&
                screenPos.y >= minScreen.y && screenPos.y <= maxScreen.y)
            {
                newSelectedObjects.Add(obj);
            }
        }

        // Deselect objects that were previously selected but are no longer selected.
        foreach (GameObject obj in currentSelectedObjects)
        {
            if (!newSelectedObjects.Contains(obj))
            {
                Renderer r = obj.GetComponent<Renderer>();
                if (r != null && originalColors.ContainsKey(obj))
                {
                    r.material.color = originalColors[obj];
                    originalColors.Remove(obj);
                }
            }
        }

        // Select new objects by changing their color to light green.
        // Also, store their original color if not already stored.
        foreach (GameObject obj in newSelectedObjects)
        {
            if (!currentSelectedObjects.Contains(obj))
            {
                Renderer r = obj.GetComponent<Renderer>();
                if (r != null)
                {
                    if (!originalColors.ContainsKey(obj))
                        originalColors[obj] = r.material.color;

                    // Set the new color to light green.
                    // You can adjust the values as needed.
                    r.material.color = new Color(0.5f, 1f, 0.5f, 1f);
                }
            }
        }

        // Update the selection.
        currentSelectedObjects = newSelectedObjects;
    }
}