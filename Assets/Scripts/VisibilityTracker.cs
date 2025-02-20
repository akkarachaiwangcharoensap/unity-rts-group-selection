using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VisibilityTracker : MonoBehaviour
{

    // A static list to track all visible objects.
    public static List<GameObject> VisibleObjects = new List<GameObject>();

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    // Called when the object becomes visible to any camera.
    void OnBecameVisible()
    {
        if (!VisibleObjects.Contains(gameObject))
        {
            VisibleObjects.Add(gameObject);
            Debug.Log(gameObject.name + " added to visible objects.");
        }
    }

    // Called when the object is no longer visible by any camera.
    void OnBecameInvisible()
    {
        if (VisibleObjects.Contains(gameObject))
        {
            VisibleObjects.Remove(gameObject);
            Debug.Log(gameObject.name + " removed from visible objects.");
        }
    }
}
