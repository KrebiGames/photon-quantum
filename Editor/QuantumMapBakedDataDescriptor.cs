namespace Quantum.Editor {
  using System;
  using UnityEngine;

  class QuantumMapBakedDataDescriptor : ScriptableObject {
    public int Version = 1;
    public Map Map;
    public BinaryData CollisionMesh;
    public string[] Regions;
    public NavMeshEntry[] NavMeshes;
    public ulong[] EntityPrototypesIds;
    public ulong[] StaticColliders2DIds;
    public ulong[] StaticColliders3DIds;
    public AdditionalAssetEntry[] AdditionalAssets;
    
    [Serializable]
    public struct NavMeshEntry {
      public NavMesh NavMesh;
      public BinaryData Data;
    }

    [Serializable]
    public struct AdditionalAssetEntry {
      public string Id;
      public string NameSuffix;
      public AssetObject Asset;
    }
  }
}
