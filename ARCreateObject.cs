using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARCreateObject : MonoBehaviour
{
    // Reference to the ARRaycastManager for performing raycasts to detect plane hits
    private ARRaycastManager arRaycastManager;

    // List to store the results of the raycast
    private List<ARRaycastHit> hits = new List<ARRaycastHit>();

    // Reference to the placement indicator (a visual cue for where the object will be placed)
    private GameObject placementIndicator;
    public GameObject PlacementIndicator => placementIndicator; // Public property to access the placement indicator

    // Prefab to be instantiated at the placement location
    [SerializeField] private GameObject Prop;

    // Static variable to keep track of the pinch state across instances
    private static bool isPinched = false;

    // Variable to track the previous pinch state to detect transitions
    private bool wasPinched = false;

    // Start is called before the first frame update
    void Start()
    {
        // Find the ARRaycastManager component in the scene
        arRaycastManager = FindObjectOfType<ARRaycastManager>();

        // Get the placement indicator object (assumes it's the first child of this GameObject)
        placementIndicator = transform.GetChild(0).gameObject;
        placementIndicator.SetActive(false); // Initially hide the placement indicator
    }

    // Update is called once per frame
    void Update()
    {
        // Create a ray from the center of the screen
        var ray = new Vector2(Screen.width / 2, Screen.height / 2);

        // Perform the AR raycast to detect if the ray hits any planes
        if (arRaycastManager.Raycast(ray, hits, TrackableType.Planes))
        {
            // Get the pose of the hit (position and rotation) from the first hit
            Pose hitPose = hits[0].pose;

            // Move this GameObject to the hit position and orientation
            transform.position = hitPose.position;
            transform.rotation = hitPose.rotation;

            // Activate the placement indicator if it is not already active
            if (!placementIndicator.activeInHierarchy)
            {
                placementIndicator.SetActive(true);
            }

            // Check if the pinch gesture was detected and there was no previous pinch
            if (isPinched && !wasPinched)
            {
                Debug.LogError("Pinch gesture detected, spawning Prop.");

                // Instantiate the Prop at the placement indicator's position and rotation
                Instantiate(Prop, placementIndicator.transform.position, placementIndicator.transform.rotation);

                // Set wasPinched to true to mark that we have handled this pinch
                wasPinched = true;

                // Hide the placement indicator after spawning the object
                placementIndicator.SetActive(false);
            }
        }
        else
        {
            // If no plane is hit, hide the placement indicator
            if (placementIndicator.activeInHierarchy)
            {
                placementIndicator.SetActive(false);
            }
        }

        // Reset the wasPinched state if the pinch is no longer detected
        if (!isPinched)
        {
            wasPinched = false;
        }
    }

    // Method to be called externally to trigger a pinch detection
    public void GetPinched()
    {
        isPinched = true;
    }

    // Method to be called externally to release the pinch state
    public void ReleasePinch()
    {
        isPinched = false;
    }
}
