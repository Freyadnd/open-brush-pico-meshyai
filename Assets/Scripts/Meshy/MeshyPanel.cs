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

using TMPro;
using UnityEngine;

namespace TiltBrush.Meshy
{
    /// <summary>
    /// VR panel for Meshy AI generation. Inherits from BasePanel.
    ///
    /// Prefab layout:
    ///   - m_StatusText     TextMeshPro — shows current state message
    ///   - m_ProgressFill   Transform   — scaled on X (0-1) to show progress
    ///   - m_IdleGroup      GameObject  — shown when idle (prompt + sketch buttons)
    ///   - m_BusyGroup      GameObject  — shown while generating (progress bar)
    ///
    /// Button wiring (via ActionButton.m_Action UnityEvents):
    ///   - "Sketch to 3D" button  → MeshyPanel.OnSketchButtonPressed
    ///   - "Cancel" button        → MeshyPanel.OnCancelButtonPressed
    ///   - Prompt entry uses OpenTextInputPopupButton + MeshyPromptButton on same GO.
    /// </summary>
    public class MeshyPanel : BasePanel
    {
        [Header("Meshy Panel")]
        [SerializeField] private TextMeshPro m_StatusText;

        [Tooltip("Child transform whose local X scale is set to 0-1 to show progress.")]
        [SerializeField] private Transform m_ProgressFill;

        [SerializeField] private GameObject m_IdleGroup;
        [SerializeField] private GameObject m_BusyGroup;

        /// <summary>Shared prompt text — written by MeshyPromptButton, read by GenerateFromText.</summary>
        public static string LastPrompt = "";

        private MeshyManager m_Meshy => MeshyManager.Instance;

        public override void InitPanel()
        {
            base.InitPanel();
            RefreshUI();
        }

        protected override void OnEnablePanel()
        {
            base.OnEnablePanel();
            if (m_Meshy != null)
                m_Meshy.OnStatusChanged += OnMeshyStatusChanged;
            RefreshUI();
        }

        protected override void OnDisablePanel()
        {
            base.OnDisablePanel();
            if (m_Meshy != null)
                m_Meshy.OnStatusChanged -= OnMeshyStatusChanged;
        }

        // ── Button callbacks ──────────────────────────────────────────────────

        public void OnSketchButtonPressed()
        {
            m_Meshy?.GenerateFromCurrentSketch();
        }

        public void OnCancelButtonPressed()
        {
            Debug.Log("[Meshy] Cancel requested – will finish current step then stop.");
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private void OnMeshyStatusChanged(MeshyManager.State state, float progress, string message)
        {
            ApplyStatus(progress, message, m_Meshy != null && m_Meshy.IsBusy);
        }

        private void RefreshUI()
        {
            bool busy = m_Meshy != null && m_Meshy.IsBusy;
            float progress = m_Meshy != null ? m_Meshy.Progress : 0f;
            string message = m_Meshy != null ? m_Meshy.StatusMessage : "";
            ApplyStatus(progress, message, busy);
        }

        private void ApplyStatus(float progress, string message, bool busy)
        {
            if (m_StatusText != null)
                m_StatusText.text = message;

            if (m_ProgressFill != null)
            {
                Vector3 s = m_ProgressFill.localScale;
                s.x = Mathf.Clamp01(progress);
                m_ProgressFill.localScale = s;
            }

            if (m_IdleGroup != null) m_IdleGroup.SetActive(!busy);
            if (m_BusyGroup != null) m_BusyGroup.SetActive(busy);
        }
    }
}
