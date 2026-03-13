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

using UnityEngine;

namespace TiltBrush.Meshy
{
    /// <summary>
    /// ScriptableObject for storing the Meshy API key.
    /// Create via Assets > Create > Open Brush > Meshy Config,
    /// then place it at Assets/Resources/MeshyConfig.asset.
    /// Add MeshyConfig.asset to .gitignore to keep the key out of source control.
    /// </summary>
    [CreateAssetMenu(menuName = "Open Brush/Meshy Config", fileName = "MeshyConfig")]
    public class MeshyConfig : ScriptableObject
    {
        [Tooltip("Your Meshy AI API key from https://app.meshy.ai/settings")]
        [SerializeField] private string m_ApiKey = "";

        public string ApiKey => m_ApiKey;
    }
}
