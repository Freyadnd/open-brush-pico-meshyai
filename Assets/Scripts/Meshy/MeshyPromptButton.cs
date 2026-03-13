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
    /// Receives the prompt text after the VR keyboard closes, then starts generation.
    ///
    /// Prefab wiring:
    ///   1. Add an OpenTextInputPopupButton to the same GameObject.
    ///   2. In its m_AfterPopupAction UnityEvent, add this component and select
    ///      MeshyPromptButton.OnPromptEntered.
    ///   3. Optionally assign m_PromptLabel to show the typed prompt.
    /// </summary>
    public class MeshyPromptButton : MonoBehaviour
    {
        [SerializeField] private TextMeshPro m_PromptLabel;
        [SerializeField] private string m_DefaultLabelText = "Tap to enter prompt…";

        private void Awake()
        {
            UpdateLabel(MeshyPanel.LastPrompt);
        }

        /// <summary>
        /// Called by OpenTextInputPopupButton.m_AfterPopupAction after the VR keyboard closes.
        /// </summary>
        public void OnPromptEntered(OpenTextInputPopupButton _)
        {
            string prompt = KeyboardPopUpWindow.m_LastInput;
            MeshyPanel.LastPrompt = prompt;
            UpdateLabel(prompt);

            if (!string.IsNullOrWhiteSpace(prompt))
                MeshyManager.Instance?.GenerateFromText(prompt);
        }

        private void UpdateLabel(string prompt)
        {
            if (m_PromptLabel == null) return;
            m_PromptLabel.text = string.IsNullOrWhiteSpace(prompt)
                ? m_DefaultLabelText
                : $"\"{prompt}\"";
        }
    }
}
