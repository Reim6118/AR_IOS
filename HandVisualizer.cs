using UnityEngine;
using System.Collections.Generic;

public class HandVisualizer : MonoBehaviour
{
    [SerializeField] private GameObject jointPrefab;
    [SerializeField] private LineRenderer skeletonLinePrefab;
    [SerializeField] private LineRenderer contourLinePrefab;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private ARCreateObject aRCreateObject;
    protected bool isPinched = false;
    public bool IsPinched => isPinched;
    private List<GameObject> joints = new List<GameObject>();
    private List<LineRenderer> skeletonLines = new List<LineRenderer>();
    private LineRenderer contourLine;

    private readonly int[] fingerTips = { 4, 8, 12, 16, 20 };
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
            GameObject joint = Instantiate(jointPrefab, transform);
            joints.Add(joint);
        }

        // Initialize skeleton lines
        foreach (var connection in connections)
        {
            LineRenderer line = Instantiate(skeletonLinePrefab, transform);
            line.positionCount = connection.Length;
            skeletonLines.Add(line);
        }

        // Initialize contour line
        contourLine = Instantiate(contourLinePrefab, transform);
        contourLine.positionCount = fingerTips.Length + 1; // +1 for closing the loop
    }

    

    public void UpdateHandVisualization(List<Vector3> landmarks)
    {
        if (landmarks.Count != 21) return;

        // Update joint positions
        for (int i = 0; i < landmarks.Count; i++)
        {
            Debug.LogError("Visualize landmark=" + landmarks[i]);
            // joints[i].transform.localPosition = landmarks[i];
            Vector3 worldPosition = mainCamera.ViewportToWorldPoint(landmarks[i]);
            Debug.LogError("WorldPosition="+worldPosition);
            joints[i].transform.localPosition = worldPosition;

        }

        if (DetectPinchingGesture(landmarks))
        {
            // If a pinching gesture is detected and it's the first detection
            if (!isPinched)
            {
                Debug.Log("Pinching gesture detected!");
                aRCreateObject.GetPinched();
                isPinched = true;  // Set the local isPinched state to true
            }
        }
        else
        {
            // If no pinching gesture is detected and it was previously pinched
            if (isPinched)
            {
                Debug.Log("Pinching gesture released!");
                aRCreateObject.ReleasePinch();
                isPinched = false;  // Reset the local isPinched state
            }
        }

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

        private bool DetectPinchingGesture(List<Vector3> landmarks)
    {
        // Access the hand landmarks and detect the pinching gesture
        Vector3 thumbTip = landmarks[4];
        Vector3 indexTip = landmarks[8];

        // Calculate the distance between the thumb and index finger tips
        float distance = Vector3.Distance(thumbTip, indexTip);

        // Detect the pinching gesture based on the distance
        return distance < 0.05f;
    }
}