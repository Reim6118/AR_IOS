using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARCreateObject : MonoBehaviour
{
    private ARRaycastManager arRaycastManager;
    private List<ARRaycastHit> hits = new List<ARRaycastHit>();
    private GameObject placementIndicator;
    public GameObject PlacementIndicator => placementIndicator;
    [SerializeField] private GameObject Prop;
    
    private static bool isPinched = false;
    private bool wasPinched = false; // New variable to keep track of the previous pinch state

    // Start is called before the first frame update
    void Start()
    {
        // Find the ARRaycastManager component in the scene
        arRaycastManager = FindObjectOfType<ARRaycastManager>();
        placementIndicator = transform.GetChild(0).gameObject;
        placementIndicator.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        var ray = new Vector2(Screen.width / 2, Screen.height / 2);

        // Perform the AR raycast
        if (arRaycastManager.Raycast(ray, hits, TrackableType.Planes))
        {
            Pose hitPose = hits[0].pose;
            transform.position = hitPose.position;
            transform.rotation = hitPose.rotation;

            if (!placementIndicator.activeInHierarchy)
            {
                placementIndicator.SetActive(true);
            }

            // Check for the transition from not pinched to pinched
            if (isPinched && !wasPinched)
            {
                Debug.LogError("Pinch gesture detected, spawning Prop.");

                // Instantiate the Prop at the placement indicator's position and rotation
                Instantiate(Prop, placementIndicator.transform.position, placementIndicator.transform.rotation);

                // Set wasPinched to true to mark that we have handled this pinch
                wasPinched = true;
                
                // Optionally hide or move the placement indicator here
                placementIndicator.SetActive(false); // Hide the indicator after spawning
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

        // Reset the state if the pinch is no longer detected
        if (!isPinched)
        {
            wasPinched = false;
        }
    }

    // This method can be called externally to trigger a pinch detection
    public void GetPinched()
    {
        isPinched = true;
    }

    // This method can be called externally to release the pinch state
    public void ReleasePinch()
    {
        isPinched = false;
    }
}
