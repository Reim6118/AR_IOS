using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections;
using Newtonsoft.Json;

public class HandTracking : MonoBehaviour
{
    private ARCameraManager arCameraManager; // Reference to the ARCameraManager component
    private ClientWebSocket webSocket = null; // WebSocket client for communication with the server
    private CancellationTokenSource cts; // Cancellation token source for managing task cancellation
    private Coroutine captureCoroutine; // Coroutine for capturing images at intervals
    public float CaptureInterval = 0.1f; // Interval in seconds between image captures
    private string WebSocketUrl = "ws://192.168.0.15:8803"; // WebSocket server URL
    // private string WebSocketUrl ="wss://f4ba-182-233-135-13.ngrok-free.app";
    private Queue<System.Action> mainThreadActions = new Queue<System.Action>(); // Queue to hold actions to be executed on the main thread

    [SerializeField] private HandVisualizer handVisualizer; // Reference to the HandVisualizer script
    [SerializeField] private ARCreateObject aRCreateObject; // Reference to the ARCreateObject script

    private void Update()
    {
        // Execute queued actions on the main thread to avoid threading issues with Unity
        while (mainThreadActions.Count > 0)
        {
            mainThreadActions.Dequeue()?.Invoke();
        }
    }

    private async void Start()
    {
        arCameraManager = FindObjectOfType<ARCameraManager>(); // Find the ARCameraManager in the scene
        if (arCameraManager == null)
        {
            Debug.LogError("ARCameraManager not found!");
            return;
        }

        cts = new CancellationTokenSource(); // Initialize the cancellation token source
        try
        {
            await InitializeWebSocket(); // Initialize the WebSocket connection
            captureCoroutine = StartCoroutine(CaptureImageAtIntervals()); // Start capturing images at regular intervals
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize: {e.Message}");
        }
    }

    // Adds an action to the queue to be executed on the main thread
    private void EnqueueMainThreadAction(System.Action action)
    {
        lock (mainThreadActions)
        {
            mainThreadActions.Enqueue(action);
        }
    }

    // Initializes the WebSocket connection to the server
    private async Task InitializeWebSocket()
    {
        try
        {
            webSocket = new ClientWebSocket(); // Create a new WebSocket client
            await webSocket.ConnectAsync(new Uri(WebSocketUrl), cts.Token); // Connect to the WebSocket server
            Debug.Log("WebSocket connected...");
            _ = ReceiveHandTrackingData(); // Start receiving hand tracking data from the server
        }
        catch (Exception e)
        {
            Debug.LogError($"WebSocket connection failed: {e.Message}");
            throw;
        }
    }

    // Coroutine that captures images at regular intervals
    private IEnumerator CaptureImageAtIntervals()
    {
        while (!cts.IsCancellationRequested)
        {
            CaptureAndSendImage(); // Capture and send the image to the server
            Debug.LogError("Capture image .......................................................");
            yield return new WaitForSeconds(CaptureInterval); // Wait for the specified interval before capturing the next image
        }
    }

    // Captures an image from the AR camera and sends it to the server
    private void CaptureAndSendImage()
    {
        if (arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
        {
            using (image)
            {
                try
                {
                    Texture2D texture = ConvertCpuImageToTexture2D(image); // Convert the AR image to a Texture2D
                    byte[] imageBytes = texture.EncodeToJPG(); // Convert the Texture2D to a JPEG byte array
                    _ = SendImageToServer(imageBytes); // Send the image to the server
                    Destroy(texture); // Destroy the texture to free memory
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error processing image: {e.Message}");
                }
            }
        }
    }

    // Converts the XRCpuImage to a Texture2D
    private Texture2D ConvertCpuImageToTexture2D(XRCpuImage image)
    {
        var conversionParams = new XRCpuImage.ConversionParams
        {
            inputRect = new RectInt(0, 0, image.width, image.height),
            outputDimensions = new Vector2Int(image.width, image.height),
            outputFormat = TextureFormat.RGBA32,
            transformation = XRCpuImage.Transformation.MirrorY
        };

        int size = image.GetConvertedDataSize(conversionParams);
        var buffer = new NativeArray<byte>(size, Allocator.Temp);
        
        try
        {
            image.Convert(conversionParams, buffer); // Convert the image to the specified format
            var texture = new Texture2D(conversionParams.outputDimensions.x, conversionParams.outputDimensions.y, conversionParams.outputFormat, false);
            texture.LoadRawTextureData(buffer); // Load the converted data into the texture
            texture.Apply(); // Apply the changes to the texture
            return texture;
        }
        finally
        {
            buffer.Dispose(); // Dispose of the buffer to free memory
        }
    }

    [Serializable]
    public class HandTrackingData
    {
        public List<List<Vector3Data>> landmarks; // List of hand landmark positions
    }

    [Serializable]
    public class Vector3Data
    {
        public float x;
        public float y;
        public float z;
    }

    // Receives hand tracking data from the server
    private async Task ReceiveHandTrackingData()
    {
        Debug.LogError("Inside Receive Hand Tracking Data");
        var buffer = new byte[1024 * 4]; // Buffer to store incoming data
        while (webSocket.State == WebSocketState.Open && !cts.IsCancellationRequested)
        {
            Debug.LogError("In while");
            try
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token); // Receive data from the WebSocket
                Debug.LogError("In try, result:" + result.Count + "buffer=" + buffer);
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    Debug.LogError("In if");
                    string jsonString = Encoding.UTF8.GetString(buffer, 0, result.Count); // Convert the received data to a string
                    Debug.LogError("JsonString:" + jsonString);
                    if (jsonString != null)
                    {
                        string handString = jsonString.Trim('[', ']'); // Trim the JSON string to extract data
                        string[] innerLists = handString.Split(new string[] { "], [" }, StringSplitOptions.RemoveEmptyEntries);
                        List<Vector3> handVectors = new List<Vector3>();
                        foreach (string innerList in innerLists)
                        {
                            Debug.LogError("handstring split1 = " + innerList);
                            string cleanedVectorString = innerList.Trim('[', ']');
                            string[] components = cleanedVectorString.Split(',');
                            float y = float.Parse(components[0].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                            float x = float.Parse(components[1].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                            // float z = float.Parse(components[2].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                            float z = 0.3f;
                            EnqueueMainThreadAction(() =>
                            {
                                if(aRCreateObject.PlacementIndicator.activeSelf != false){
                                    z = aRCreateObject.PlacementIndicator.transform.position.z;
                                }
                            });
                            
                            handVectors.Add(new Vector3(x,y,z));
                        }
                        int count = 1;
                        foreach (var vector in handVectors)
                        {
                            Debug.LogError("Vector quantity" + count);
                            Debug.LogError("vector="+vector);
                            count+=1;
                        }
                        handVisualizer.UpdateHandVisualization(handVectors); // Update the hand visualizer with the received data
                    }
                }
            }
            catch (WebSocketException e)
            {
                Debug.LogError($"WebSocket receive error: {e.Message}");
                break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Unexpected error in ReceiveHandTrackingData: {ex.Message}");
            }
        }
    }

    // Sends the captured image to the server via WebSocket
    private async Task SendImageToServer(byte[] imageBytes)
    {
        if (webSocket.State == WebSocketState.Open)
        {
            try
            {
                await webSocket.SendAsync(new ArraySegment<byte>(imageBytes), WebSocketMessageType.Binary, true, cts.Token);
            }
            catch (WebSocketException e)
            {
                Debug.LogError($"WebSocket send error: {e.Message}");
            }
            catch (OperationCanceledException)
            {
                // Cancellation requested, no need to log
            }
        }
    }

    // Updates the hand model with the received landmarks
    private void UpdateHandModel(List<List<Vector3>> handLandmarks)
    {
        Debug.LogError("In updatehandmodel");
        if (handLandmarks == null || handLandmarks.Count == 0)
        {
            Debug.LogError("No handlandmarks...");
            return;
        }
        
        List<Vector3> landmarks = handLandmarks[0];
        Debug.LogError("after list");

        // Convert the landmarks to Unity's coordinate system
        for (int i = 0; i < landmarks.Count; i++)
        {
            Debug.LogError("Inside landmarks for");
            landmarks[i] = new Vector3(landmarks[i].x, -landmarks[i].y, landmarks[i].z);
        }

        // Update the hand visualizer
        Debug.LogError("Landmarks:" + landmarks);
        handVisualizer.UpdateHandVisualization(landmarks);
    }

    // Cleans up resources when the object is disabled
    private async void OnDisable()
    {
        await CleanUp();
    }

    // Cleans up resources when the object is destroyed
    private async void OnDestroy()
    {
        await CleanUp();
    }

    // Cleans up the WebSocket connection and other resources
    private async Task CleanUp()
    {
        if (captureCoroutine != null)
        {
            StopCoroutine(captureCoroutine);
            captureCoroutine = null;
        }

        cts.Cancel(); // Cancel all pending tasks

        if (webSocket != null && webSocket.State == WebSocketState.Open)
        {
            try
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by the client", CancellationToken.None); // Close the WebSocket connection
            }
            catch (WebSocketException e)
            {
                Debug.LogError($"Error closing WebSocket: {e.Message}");
            }
        }

        webSocket?.Dispose(); // Dispose of the WebSocket to free resources
        cts.Dispose(); // Dispose of the cancellation token source
    }
}
