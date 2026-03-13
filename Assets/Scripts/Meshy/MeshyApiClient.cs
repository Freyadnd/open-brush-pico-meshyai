// Copyright 2024 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace TiltBrush.Meshy
{
    /// <summary>
    /// Low-level HTTP client for the Meshy AI API.
    /// Attach as a component on the MeshyManager GameObject.
    /// </summary>
    public class MeshyApiClient : MonoBehaviour
    {
        private const string BASE_URL = "https://api.meshy.ai/openapi/v2";

        private string m_ApiKey;

        public void Init(string apiKey)
        {
            m_ApiKey = apiKey;
        }

        // ── Text to 3D ──────────────────────────────────────────────────────────

        public IEnumerator CreateTextTo3DPreview(
            string prompt,
            string negativePrompt,
            Action<string> onSuccess,
            Action<string> onError)
        {
            var body = new JObject
            {
                ["mode"] = "preview",
                ["prompt"] = prompt,
                ["negative_prompt"] = negativePrompt ?? "",
                ["art_style"] = "realistic",
                ["should_remesh"] = true
            };
            yield return PostTask($"{BASE_URL}/text-to-3d", body.ToString(), onSuccess, onError);
        }

        public IEnumerator RefineTextTo3D(
            string previewTaskId,
            Action<string> onSuccess,
            Action<string> onError)
        {
            var body = new JObject
            {
                ["mode"] = "refine",
                ["preview_task_id"] = previewTaskId,
                ["enable_pbr"] = true
            };
            yield return PostTask($"{BASE_URL}/text-to-3d", body.ToString(), onSuccess, onError);
        }

        // ── Image to 3D ─────────────────────────────────────────────────────────

        public IEnumerator CreateImageTo3D(
            byte[] imageBytes,
            string mimeType,
            Action<string> onSuccess,
            Action<string> onError)
        {
            string base64 = Convert.ToBase64String(imageBytes);
            string dataUrl = $"data:{mimeType};base64,{base64}";

            var body = new JObject
            {
                ["image_url"] = dataUrl,
                ["enable_pbr"] = false
            };
            yield return PostTask($"{BASE_URL}/image-to-3d", body.ToString(), onSuccess, onError);
        }

        // ── Polling ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Polls a Meshy task until it succeeds, fails, or expires.
        /// onProgress receives 0-1. onComplete receives the full response JSON object.
        /// </summary>
        public IEnumerator PollTask(
            string endpoint,
            string taskId,
            Action<JObject> onComplete,
            Action<float> onProgress,
            Action<string> onError,
            float pollIntervalSeconds = 4f)
        {
            while (true)
            {
                JObject result = null;
                string error = null;

                yield return Get($"{BASE_URL}/{endpoint}/{taskId}",
                    json => result = JObject.Parse(json),
                    err => error = err);

                if (error != null)
                {
                    onError?.Invoke(error);
                    yield break;
                }

                string status = result["status"]?.Value<string>();

                switch (status)
                {
                    case "SUCCEEDED":
                        onComplete?.Invoke(result);
                        yield break;
                    case "FAILED":
                    case "EXPIRED":
                        string msg = result["task_error"]?["message"]?.Value<string>() ?? status;
                        onError?.Invoke(msg);
                        yield break;
                    default:
                        // Meshy reports progress as 0-100
                        float p = result["progress"]?.Value<float>() ?? 0f;
                        onProgress?.Invoke(p / 100f);
                        yield return new WaitForSeconds(pollIntervalSeconds);
                        break;
                }
            }
        }

        // ── File download ────────────────────────────────────────────────────────

        public IEnumerator DownloadFile(
            string url,
            string destinationPath,
            Action onSuccess,
            Action<string> onError,
            Action<float> onProgress = null)
        {
            using var request = UnityWebRequest.Get(url);
            request.downloadHandler = new DownloadHandlerFile(destinationPath);

            var op = request.SendWebRequest();
            while (!op.isDone)
            {
                onProgress?.Invoke(request.downloadProgress);
                yield return null;
            }

            if (request.result != UnityWebRequest.Result.Success)
                onError?.Invoke(request.error);
            else
                onSuccess?.Invoke();
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private IEnumerator PostTask(
            string url,
            string jsonBody,
            Action<string> onSuccess,
            Action<string> onError)
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {m_ApiKey}");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"HTTP {request.responseCode}: {request.downloadHandler.text}");
                yield break;
            }

            var response = JObject.Parse(request.downloadHandler.text);
            string taskId = response["result"]?.Value<string>() ?? response["id"]?.Value<string>();
            if (string.IsNullOrEmpty(taskId))
            {
                onError?.Invoke($"No task ID in response: {request.downloadHandler.text}");
                yield break;
            }
            onSuccess?.Invoke(taskId);
        }

        private IEnumerator Get(
            string url,
            Action<string> onSuccess,
            Action<string> onError)
        {
            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Authorization", $"Bearer {m_ApiKey}");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
                onError?.Invoke($"HTTP {request.responseCode}: {request.error}");
            else
                onSuccess?.Invoke(request.downloadHandler.text);
        }
    }
}
