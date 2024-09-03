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
    private ARCameraManager arCameraManager;
    private ClientWebSocket webSocket = null;
    private CancellationTokenSource cts;
    private Coroutine captureCoroutine;
    public float CaptureInterval = 0.1f;
    private string WebSocketUrl = "ws://192.168.0.15:8803";
    // private string WebSocketUrl ="wss://f4ba-182-233-135-13.ngrok-free.app";
    private Queue<System.Action> mainThreadActions = new Queue<System.Action>();

    [SerializeField] private HandVisualizer handVisualizer;
    [SerializeField] private ARCreateObject aRCreateObject;

    private void Update()
    {
        // Execute queued actions on the main thread
        while (mainThreadActions.Count > 0)
        {
            mainThreadActions.Dequeue()?.Invoke();
        }
    }
    private async void Start()
    {
        arCameraManager = FindObjectOfType<ARCameraManager>();
        if (arCameraManager == null)
        {
            Debug.LogError("ARCameraManager not found!");
            return;

            
        }

        cts = new CancellationTokenSource();
        try
        {
            await InitializeWebSocket();
            captureCoroutine = StartCoroutine(CaptureImageAtIntervals());
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize: {e.Message}");
        }
    }
    private void EnqueueMainThreadAction(System.Action action)
    {
        lock (mainThreadActions)
        {
            mainThreadActions.Enqueue(action);
        }
    }
    private async Task InitializeWebSocket()
    {
        try
        {
            webSocket = new ClientWebSocket();
            await webSocket.ConnectAsync(new Uri(WebSocketUrl), cts.Token);
            Debug.Log("WebSocket connected...");
            _ = ReceiveHandTrackingData();
        }
        catch (Exception e)
        {
            Debug.LogError($"WebSocket connection failed: {e.Message}");
            throw;
        }
    }

    private IEnumerator CaptureImageAtIntervals()
    {
        while (!cts.IsCancellationRequested)
        {
            CaptureAndSendImage();
            Debug.LogError("Capture image .......................................................");
            yield return new WaitForSeconds(CaptureInterval);
        }
    }

   private void CaptureAndSendImage()
    {
        if (arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
        {
            using (image)
            {
                try
                {
                    Texture2D texture = ConvertCpuImageToTexture2D(image);
                    byte[] imageBytes = texture.EncodeToJPG();
                    _ = SendImageToServer(imageBytes);
                    Destroy(texture);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error processing image: {e.Message}");
                }
            }
        }
    }

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
            image.Convert(conversionParams, buffer);
            var texture = new Texture2D(conversionParams.outputDimensions.x, conversionParams.outputDimensions.y, conversionParams.outputFormat, false);
            texture.LoadRawTextureData(buffer);
            texture.Apply();
            return texture;
        }
        finally
        {
            buffer.Dispose();
        }
    }

[Serializable]
public class HandTrackingData
{
    public List<List<Vector3Data>> landmarks;
}

[Serializable]
public class Vector3Data
{
    public float x;
    public float y;
    public float z;
}



    private async Task ReceiveHandTrackingData()
{
    Debug.LogError("Inside Receive Hand Tracking Data");
    var buffer = new byte[1024 * 4]; // Increased buffer size
    while (webSocket.State == WebSocketState.Open && !cts.IsCancellationRequested)
    {
        Debug.LogError("In while");
        try
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
            Debug.LogError("In try, result:" + result.Count + "buffer=" + buffer);
            if (result.MessageType == WebSocketMessageType.Text)
            {
                Debug.LogError("In if");
                string jsonString = Encoding.UTF8.GetString(buffer, 0, result.Count);
                Debug.LogError("JsonString:" + jsonString);
                if (jsonString != null)
                {
                    string handString = jsonString.Trim('[', ']');
                    string[] innerLists = handString.Split(new string[] { "], [" }, StringSplitOptions.RemoveEmptyEntries);
                    List<Vector3>  handVectors = new List<Vector3>();
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
                    handVisualizer.UpdateHandVisualization(handVectors);


                }
                



                // try
                // {
                //     // Deserialize JSON into a list of lists of Vector3Data
                //     var handTrackingData = JsonConvert.DeserializeObject<HandTrackingData>(jsonString);

                //     // Convert Vector3Data to Vector3
                //     List<List<Vector3>> handLandmarks = new List<List<Vector3>>();
                //     foreach (var sublist in handTrackingData.landmarks)
                //     {
                //         List<Vector3> vector3List = new List<Vector3>();
                //         foreach (var data in sublist)
                //         {
                //             vector3List.Add(new Vector3(data.x, data.y, data.z));
                //         }
                //         handLandmarks.Add(vector3List);
                //     }

                //     Debug.LogError("Successfully deserialized JSON into hand landmarks.");
                //     UpdateHandModel(handLandmarks);
                // }
                // catch (JsonException ex)
                // {
                //     Debug.LogError($"JSON Deserialization Error: {ex.Message}");
                // }
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

    private void UpdateHandModel(List<List<Vector3>> handLandmarks)
    {
        Debug.LogError("In updatehandmodel");
        if (handLandmarks == null || handLandmarks.Count == 0){
            Debug.LogError("No handlandmarks...");
            return;}
        
        List<Vector3> landmarks = handLandmarks[0];
        Debug.LogError("after list");
        // Convert the landmarks to Unity's coordinate system
        for (int i = 0; i < landmarks.Count; i++)
        {
            Debug.LogError("Inside landmarks for");
            landmarks[i] = new Vector3(landmarks[i].x, -landmarks[i].y, landmarks[i].z);
        }

        // Update the hand visualizer
        Debug.LogError("Landmarks:"+landmarks);
        handVisualizer.UpdateHandVisualization(landmarks);
    }

    private async void OnDisable()
    {
        await CleanUp();
    }

    private async void OnDestroy()
    {
        await CleanUp();
    }

    private async Task CleanUp()
    {
        if (captureCoroutine != null)
        {
            StopCoroutine(captureCoroutine);
            captureCoroutine = null;
        }

        cts.Cancel();

        if (webSocket != null && webSocket.State == WebSocketState.Open)
        {
            try
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by the client", CancellationToken.None);
            }
            catch (WebSocketException e)
            {
                Debug.LogError($"Error closing WebSocket: {e.Message}");
            }
        }

        webSocket?.Dispose();
        cts.Dispose();
    }
}