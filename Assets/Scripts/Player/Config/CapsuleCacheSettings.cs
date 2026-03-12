using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "CapsuleCacheSettings", menuName = "DIG/Physics/CapsuleCacheSettings")]
public class CapsuleCacheSettings : ScriptableObject
{
    [Tooltip("Maximum number of cached capsule blob sizes (LRU)")]
    public int Capacity = 32;

    [Tooltip("If true, prewarm cache on startup with common sizes")]
    public bool Prewarm = true;

    [Tooltip("Heights (meters) to prewarm")]
    public List<float> PrewarmHeights = new List<float> { 1.6f, 1.8f, 2.0f };

    [Tooltip("Radii (meters) to prewarm")]
    public List<float> PrewarmRadii = new List<float> { 0.25f, 0.35f };
}
