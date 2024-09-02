using UnityEngine;
using System.Collections.Generic;

public class HandVisualizer : MonoBehaviour
{
    [SerializeField] private GameObject jointPrefab;
    [SerializeField] private LineRenderer skeletonLinePrefab;
    [SerializeField] private LineRenderer contourLinePrefab;
    [SerializeField] private Camera mainCamera;
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

        // Update skeleton lines
        for (int i = 0; i < connections.Length; i++)
        {
            Vector3[] positions = new Vector3[connections[i].Length];
            for (int j = 0; j < connections[i].Length; j++)
            {
                positions[j] = landmarks[connections[i][j]];
            }
            skeletonLines[i].SetPositions(positions);
        }

        // Update contour line
        Vector3[] contourPositions = new Vector3[fingerTips.Length + 1];
        for (int i = 0; i < fingerTips.Length; i++)
        {
            contourPositions[i] = landmarks[fingerTips[i]];
        }
        contourPositions[fingerTips.Length] = landmarks[fingerTips[0]]; // Close the loop
        contourLine.SetPositions(contourPositions);
    }
}