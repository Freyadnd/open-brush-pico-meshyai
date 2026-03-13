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
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace TiltBrush.Meshy
{
    /// <summary>
    /// Singleton manager for all Meshy AI generation workflows.
    /// Attach to a persistent GameObject in the main scene.
    /// Set the API key in the Inspector (or via MeshyConfig in Resources).
    /// </summary>
    public class MeshyManager : MonoBehaviour
    {
        public static MeshyManager Instance { get; private set; }

        [Tooltip("Your Meshy AI API key. Alternatively create a MeshyConfig asset in Resources.")]
        [SerializeField] private string m_ApiKey = "";

        private MeshyApiClient m_Client;

        // ── State machine ─────────────────────────────────────────────────────

        public enum State
        {
            Idle,
            Generating,
            Refining,
            Downloading,
            Importing,
            Done,
            Error
        }

        public State CurrentState { get; private set; } = State.Idle;
        public float Progress { get; private set; }
        public string StatusMessage { get; private set; } = "";

        /// <summary>Fired whenever state or progress changes.</summary>
        public event Action<State, float, string> OnStatusChanged;

        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // Load API key from Resources/MeshyConfig if not set in inspector
            if (string.IsNullOrEmpty(m_ApiKey))
            {
                var config = Resources.Load<MeshyConfig>("MeshyConfig");
                if (config != null)
                    m_ApiKey = config.ApiKey;
            }

            if (string.IsNullOrEmpty(m_ApiKey))
                Debug.LogWarning("[Meshy] No API key configured. Set it in MeshyManager or create Assets/Resources/MeshyConfig.asset");

            m_Client = gameObject.AddComponent<MeshyApiClient>();
            m_Client.Init(m_ApiKey);
        }

        public bool IsBusy => CurrentState == State.Generating
                           || CurrentState == State.Refining
                           || CurrentState == State.Downloading
                           || CurrentState == State.Importing;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Generate a 3D model from a text prompt and import it into the scene.
        /// artStyle: "realistic" | "cartoon" | "low-poly" | "sculpture" | "pbr"
        /// </summary>
        public void GenerateFromText(string prompt, string negativePrompt = "", string artStyle = "realistic")
        {
            if (IsBusy)
            {
                Debug.LogWarning("[Meshy] Already generating.");
                return;
            }
            StartCoroutine(TextTo3DCoroutine(prompt, negativePrompt, artStyle));
        }

        /// <summary>
        /// Generate a 3D model from an image (PNG bytes) and import it into the scene.
        /// Typically called after capturing the current sketch via SketchToImage().
        /// </summary>
        public void GenerateFromImage(byte[] pngBytes)
        {
            if (IsBusy)
            {
                Debug.LogWarning("[Meshy] Already generating.");
                return;
            }
            StartCoroutine(ImageTo3DCoroutine(pngBytes));
        }

        /// <summary>
        /// Captures the current view as a PNG and sends it to Image-to-3D.
        /// </summary>
        public void GenerateFromCurrentSketch()
        {
            if (IsBusy) return;
            StartCoroutine(CaptureAndGenerateCoroutine());
        }

        // ── Coroutines ────────────────────────────────────────────────────────

        private IEnumerator TextTo3DCoroutine(string prompt, string negativePrompt, string artStyle)
        {
            SetStatus(State.Generating, 0f, $"Generating preview: \"{prompt}\"");

            // Step 1 – Create preview task
            string previewId = null;
            string error = null;
            yield return m_Client.CreateTextTo3DPreview(prompt, negativePrompt,
                id => previewId = id,
                err => error = err);

            if (!string.IsNullOrEmpty(error)) { SetStatus(State.Error, 0f, error); yield break; }

            // Step 2 – Poll preview
            JObject previewResult = null;
            yield return m_Client.PollTask("text-to-3d", previewId,
                r => previewResult = r,
                p => SetStatus(State.Generating, p * 0.45f, $"Generating… {Pct(p * 0.45f)}"),
                err => error = err);

            if (!string.IsNullOrEmpty(error)) { SetStatus(State.Error, 0f, error); yield break; }

            // Step 3 – Refine
            SetStatus(State.Refining, 0.45f, "Refining model…");
            string refineId = null;
            yield return m_Client.RefineTextTo3D(previewId,
                id => refineId = id,
                err => error = err);

            if (!string.IsNullOrEmpty(error)) { SetStatus(State.Error, 0f, error); yield break; }

            // Step 4 – Poll refine
            JObject refineResult = null;
            yield return m_Client.PollTask("text-to-3d", refineId,
                r => refineResult = r,
                p => SetStatus(State.Refining, 0.45f + p * 0.45f, $"Refining… {Pct(0.45f + p * 0.45f)}"),
                err => error = err);

            if (!string.IsNullOrEmpty(error)) { SetStatus(State.Error, 0f, error); yield break; }

            string glbUrl = refineResult?["model_urls"]?["glb"]?.Value<string>();
            yield return DownloadAndImport(glbUrl, SafeName(prompt));
        }

        private IEnumerator ImageTo3DCoroutine(byte[] pngBytes)
        {
            SetStatus(State.Generating, 0f, "Sending sketch to Meshy…");

            string taskId = null;
            string error = null;
            yield return m_Client.CreateImageTo3D(pngBytes, "image/png",
                id => taskId = id,
                err => error = err);

            if (!string.IsNullOrEmpty(error)) { SetStatus(State.Error, 0f, error); yield break; }

            JObject result = null;
            yield return m_Client.PollTask("image-to-3d", taskId,
                r => result = r,
                p => SetStatus(State.Generating, p * 0.9f, $"Generating… {Pct(p * 0.9f)}"),
                err => error = err);

            if (!string.IsNullOrEmpty(error)) { SetStatus(State.Error, 0f, error); yield break; }

            string glbUrl = result?["model_urls"]?["glb"]?.Value<string>();
            yield return DownloadAndImport(glbUrl, $"sketch_{DateTime.Now:yyyyMMdd_HHmmss}");
        }

        private IEnumerator CaptureAndGenerateCoroutine()
        {
            SetStatus(State.Generating, 0f, "Capturing sketch…");

            // Wait one frame so the UI hides before the screenshot
            yield return new WaitForEndOfFrame();

            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            byte[] png = tex.EncodeToPNG();
            Destroy(tex);

            yield return ImageTo3DCoroutine(png);
        }

        private IEnumerator DownloadAndImport(string glbUrl, string filename)
        {
            if (string.IsNullOrEmpty(glbUrl))
            {
                SetStatus(State.Error, 0f, "Meshy returned no GLB URL");
                yield break;
            }

            SetStatus(State.Downloading, 0.9f, "Downloading model…");

            string meshyDir = Path.Combine(App.ModelLibraryPath(), "Meshy");
            Directory.CreateDirectory(meshyDir);
            string localPath = Path.Combine(meshyDir, $"{filename}.glb");

            string error = null;
            yield return m_Client.DownloadFile(glbUrl, localPath,
                () => { },
                err => error = err,
                p => SetStatus(State.Downloading, 0.9f + p * 0.09f, $"Downloading… {Pct(p)}"));

            if (!string.IsNullOrEmpty(error)) { SetStatus(State.Error, 0f, error); yield break; }

            SetStatus(State.Importing, 0.99f, "Importing into scene…");

            // ImportModel expects a path relative to the Models folder
            string relativePath = Path.Combine("Meshy", $"{filename}.glb");
            ApiMethods.ImportModel(relativePath);

            SetStatus(State.Done, 1f, $"Done! \"{filename}\" added to scene.");
            TriggerHaptics();

            yield return new WaitForSeconds(4f);
            SetStatus(State.Idle, 0f, "");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetStatus(State state, float progress, string message)
        {
            CurrentState = state;
            Progress = progress;
            StatusMessage = message;
            OnStatusChanged?.Invoke(state, progress, message);
            Debug.Log($"[Meshy] [{state}] {message}");
        }

        private static string SafeName(string input)
        {
            string safe = string.Join("_", input.Split(Path.GetInvalidFileNameChars()));
            return safe.Length > 40 ? safe.Substring(0, 40) : safe;
        }

        private static string Pct(float v) => $"{Mathf.RoundToInt(v * 100)}%";

        private static void TriggerHaptics()
        {
            try
            {
                InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, 0.3f);
                InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Wand, 0.3f);
            }
            catch { /* haptics not critical */ }
        }
    }
}
