namespace Quantum.Editor {
  using System;
  using UnityEngine;

  class QuantumNavMeshBakedDataDescriptor : ScriptableObject {
    public int Version = 1;
    public string[] NavMeshRegions;
    public NavMeshEntry[] NavMeshes;
    
    [Serializable]
    public struct NavMeshEntry {
      public NavMesh NavMesh;
      public BinaryData NavMeshData;
    }
  }
}