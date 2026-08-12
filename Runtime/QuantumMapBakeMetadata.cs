namespace Quantum {
  using System;
  using UnityEngine;

  /// <summary>
  /// Bake metadata stored as a hidden sub-asset of a baked <see cref="Map"/>. Holds editor-side data
  /// that supports the baked map but is never read by the simulation.
  /// </summary>
  class QuantumMapBakeMetadata : ScriptableObject {
    /// <summary>
    /// Folded GlobalObjectIds of the entity prototype sources, index-aligned with <see cref="Map.MapEntities"/>.
    /// </summary>
    public ulong[] EntityPrototypesIds = Array.Empty<ulong>();

    /// <summary>
    /// Folded GlobalObjectIds of the 2D static collider sources, index-aligned with <see cref="Map.StaticColliders2D"/>.
    /// </summary>
    public ulong[] StaticColliders2DIds = Array.Empty<ulong>();

    /// <summary>
    /// Folded GlobalObjectIds of the 3D static collider sources, index-aligned with <see cref="Map.StaticColliders3D"/>.
    /// </summary>
    public ulong[] StaticColliders3DIds = Array.Empty<ulong>();

#if UNITY_EDITOR
    internal static QuantumMapBakeMetadata TryGet(Map map) {
      var path = UnityEditor.AssetDatabase.GetAssetPath(map);
      if (string.IsNullOrEmpty(path)) {
        return null;
      }

      foreach (var obj in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path)) {
        if (obj is QuantumMapBakeMetadata metadata) {
          return metadata;
        }
      }

      return null;
    }
#endif
  }
}