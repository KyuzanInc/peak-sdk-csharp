using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Peak.Exceptions;

namespace Peak.Utils
{
    public class PeakHttpClient : IPeakHttpClient
    {
        private readonly string baseUrl;
        private readonly string projectApiKey;
        private readonly int timeoutSeconds;

        public PeakHttpClient(string baseUrl, string projectApiKey, int timeoutSeconds = 30)
        {
            this.baseUrl = baseUrl.TrimEnd('/');
            this.projectApiKey = projectApiKey;
            this.timeoutSeconds = timeoutSeconds;
        }

        public async UniTask<T> GetAsync<T>(string endpoint, Dictionary<string, string> headers = null)
        {
            string url = $"{baseUrl}/{endpoint.TrimStart('/')}";

            using var request = UnityWebRequest.Get(url);
            SetHeaders(request, headers);

            return await SendRequestAsync<T>(request);
        }

        public async UniTask<T> PostAsync<T>(string endpoint, object payload = null, Dictionary<string, string> headers = null)
        {
            string url = $"{baseUrl}/{endpoint.TrimStart('/')}";

            using var request = new UnityWebRequest(url, "POST");

            if (payload != null)
            {
                string jsonPayload = JsonUtility.ToJson(payload);
                Debug.Log($"[PeakHttpClient] POST - Endpoint: {endpoint}");
                Debug.Log($"[PeakHttpClient] JSON Payload: {jsonPayload}");
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.SetRequestHeader("Content-Type", "application/json");
            }

            request.downloadHandler = new DownloadHandlerBuffer();
            SetHeaders(request, headers);

            return await SendRequestAsync<T>(request);
        }

        private async UniTask<T> SendRequestAsync<T>(UnityWebRequest request)
        {
            request.timeout = timeoutSeconds;

            Debug.Log($"[PeakHttpClient] Sending {request.method} request to: {request.url}");

            try
            {
                // Use UniTask's ToUniTask() extension method
                await request.SendWebRequest().ToUniTask();
            }
            catch (Exception ex) when (!(ex is HttpException))
            {
                Debug.LogError($"[PeakHttpClient] Request exception - Result: {request.result}, Error: {request.error}, Exception: {ex.Message}");

                // Don't wrap HTTP errors - let them fall through to be handled below
                // Only catch true network/connection errors here
                if (request.result == UnityWebRequest.Result.ConnectionError ||
                    request.result == UnityWebRequest.Result.DataProcessingError)
                {
                    throw new HttpException(0, $"Network request failed: {request.error ?? ex.Message}", ex);
                }
                // For HTTP protocol errors, let them be handled below with proper status codes
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;

                if (string.IsNullOrEmpty(responseText))
                {
                    return default(T);
                }

                try
                {
                    return JsonUtility.FromJson<T>(responseText);
                }
                catch (ArgumentException ex)
                {
                    throw new HttpException((int)request.responseCode,
                        $"Failed to parse JSON response: {ex.Message}", responseText);
                }
            }
            else
            {
                string errorMessage = GetErrorMessage(request);
                Debug.LogError($"[PeakHttpClient] Request failed - Status: {request.responseCode}, Result: {request.result}, Error: {request.error}");
                Debug.LogError($"[PeakHttpClient] Response body: {request.downloadHandler?.text}");
                throw new HttpException((int)request.responseCode, errorMessage, request.downloadHandler?.text);
            }
        }

        private void SetHeaders(UnityWebRequest request, Dictionary<string, string> additionalHeaders)
        {
            // Set project API key header
            if (!string.IsNullOrEmpty(projectApiKey))
            {
                request.SetRequestHeader("X-API-KEY", projectApiKey);
            }

            // Set additional headers
            if (additionalHeaders != null)
            {
                foreach (var header in additionalHeaders)
                {
                    request.SetRequestHeader(header.Key, header.Value);
                }
            }
        }

        private string GetErrorMessage(UnityWebRequest request)
        {
            switch (request.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                    return "Connection error: Unable to connect to the server";
                case UnityWebRequest.Result.DataProcessingError:
                    return "Data processing error: Invalid response data";
                case UnityWebRequest.Result.ProtocolError:
                    return $"HTTP error {request.responseCode}: {request.error}";
                default:
                    return $"Unknown error: {request.error}";
            }
        }
    }
}