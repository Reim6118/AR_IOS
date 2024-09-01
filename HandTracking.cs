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

public class HandTracking : MonoBehaviour
{
    private ARCameraManager arCameraManager;
    private ClientWebSocket webSocket = null;
    private CancellationTokenSource cts;
    private Coroutine captureCoroutine;
    public float CaptureInterval = 0.1f;
    public string WebSocketUrl = "ws://192.168.0.15:8803";

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

    private async Task ReceiveHandTrackingData()
    {
        var buffer = new byte[8192]; // Increased buffer size
        while (webSocket.State == WebSocketState.Open && !cts.IsCancellationRequested)
        {
            try
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string jsonString = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var handLandmarks = JsonUtility.FromJson<List<List<Vector3>>>(jsonString);
                    UpdateHandModel(handLandmarks);
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
        if (handLandmarks == null || handLandmarks.Count == 0)
            return;

        foreach (var hand in handLandmarks)
        {
            for (int i = 0; i < hand.Count; i++)
            {
                Vector3 position = hand[i];
                // TODO: Update your hand model here
            }
        }
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