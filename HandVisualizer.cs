using UnityEngine;
using System.Collections.Generic;

public class HandVisualizer : MonoBehaviour
{
    // Prefabs for visualizing the hand joints and lines
    [SerializeField] private GameObject jointPrefab; // Prefab for visualizing each joint
    [SerializeField] private LineRenderer skeletonLinePrefab; // Prefab for visualizing the skeleton lines between joints
    [SerializeField] private LineRenderer contourLinePrefab; // Prefab for visualizing the contour line around the hand
    [SerializeField] private Camera mainCamera; // Reference to the main camera for coordinate conversion
    [SerializeField] private ARCreateObject aRCreateObject; // Reference to ARCreateObject script for handling AR object creation
    protected bool isPinched = false; // State to check if a pinching gesture is detected
    public bool IsPinched => isPinched; // Public property to access the pinching state
    private List<GameObject> joints = new List<GameObject>(); // List to store all the instantiated joint GameObjects
    private List<LineRenderer> skeletonLines = new List<LineRenderer>(); // List to store all the instantiated skeleton lines
    private LineRenderer contourLine; // LineRenderer to draw the contour line around the hand

    // Indices of the finger tips in the landmarks list
    private readonly int[] fingerTips = { 4, 8, 12, 16, 20 };

    // Connections defining which joints are connected to form the hand skeleton
    private readonly int[][] connections = new int[][]
    {
        new int[] { 0, 1, 2, 3, 4 },     // Thumb
        new int[] { 0, 5, 6, 7, 8 },     // Index finger
        new int[] { 9, 10, 11, 12 },     // Middle finger
        new int[] { 13, 14, 15, 16 },    // Ring finger
        new int[] { 17, 18, 19, 20 },    // Pinky
        new int[] { 0, 5, 9, 13, 17 }    // Palm
    };

    private void Start()
    {
        // Initialize joints
        for (int i = 0; i < 21; i++)
        {
            GameObject joint = Instantiate(jointPrefab, transform); // Instantiate each joint prefab
            joints.Add(joint); // Add the instantiated joint to the list
        }

        // Initialize skeleton lines
        foreach (var connection in connections)
        {
            LineRenderer line = Instantiate(skeletonLinePrefab, transform); // Instantiate a line renderer for each connection
            line.positionCount = connection.Length; // Set the number of positions based on the number of joints in the connection
            skeletonLines.Add(line); // Add the instantiated line to the list
        }

        // Initialize contour line
        contourLine = Instantiate(contourLinePrefab, transform); // Instantiate the contour line renderer
        contourLine.positionCount = fingerTips.Length + 1; // Set position count to include all finger tips plus one for closing the loop
    }

    // Updates the visualization of the hand based on detected landmarks
    public void UpdateHandVisualization(List<Vector3> landmarks)
    {
        if (landmarks.Count != 21) return; // Ensure the landmarks list has 21 positions

        // Update joint positions
        for (int i = 0; i < landmarks.Count; i++)
        {
            Debug.LogError("Visualize landmark=" + landmarks[i]);
            
            // Convert viewport coordinates to world coordinates for each landmark
            Vector3 worldPosition = mainCamera.ViewportToWorldPoint(landmarks[i]);
            Debug.LogError("WorldPosition=" + worldPosition);
            joints[i].transform.localPosition = worldPosition; // Update joint position
        }

        // Check for pinching gesture
        if (DetectPinchingGesture(landmarks))
        {
            // If a pinching gesture is detected and it's the first detection
            if (!isPinched)
            {
                Debug.Log("Pinching gesture detected!");
                aRCreateObject.GetPinched(); // Notify AR object creation about the pinch
                isPinched = true;  // Set the local isPinched state to true
            }
        }
        else
        {
            // If no pinching gesture is detected and it was previously pinched
            if (isPinched)
            {
                Debug.Log("Pinching gesture released!");
                aRCreateObject.ReleasePinch(); // Notify AR object creation about the release
                isPinched = false;  // Reset the local isPinched state
            }
        }

        // The code below to update skeleton lines and contour lines is currently commented out
        // If you need to visualize these, uncomment the code below

        // // Update skeleton lines
        // for (int i = 0; i < connections.Length; i++)
        // {
        //     Vector3[] positions = new Vector3[connections[i].Length];
        //     for (int j = 0; j < connections[i].Length; j++)
        //     {
        //         positions[j] = landmarks[connections[i][j]];
        //     }
        //     skeletonLines[i].SetPositions(positions);
        // }

        // // Update contour line
        // Vector3[] contourPositions = new Vector3[fingerTips.Length + 1];
        // for (int i = 0; i < fingerTips.Length; i++)
        // {
        //     contourPositions[i] = landmarks[fingerTips[i]];
        // }
        // contourPositions[fingerTips.Length] = landmarks[fingerTips[0]]; // Close the loop
        // contourLine.SetPositions(contourPositions);
    }

    // Detects a pinching gesture based on hand landmarks
    private bool DetectPinchingGesture(List<Vector3> landmarks)
    {
        // Access the hand landmarks and detect the pinching gesture
        Vector3 thumbTip = landmarks[4]; // Thumb tip position
        Vector3 indexTip = landmarks[8]; // Index finger tip position

        // Calculate the distance between the thumb and index finger tips
        float distance = Vector3.Distance(thumbTip, indexTip);

        // Detect the pinching gesture based on the distance
        return distance < 0.05f; // Return true if the distance is less than the threshold
    }
}
