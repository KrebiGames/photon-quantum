namespace Quantum.Editor {
  using System;
  using UnityEngine;

  class QuantumMapBakedDataDescriptor : ScriptableObject {
    public int Version = 1;
    public Map Map;
    public BinaryData CollisionMesh;
    public string[] Regions;
    public NavMeshEntry[] NavMeshes;

    [Serializable]
    public struct NavMeshEntry {
      public NavMesh NavMesh;
      public BinaryData Data;
    }
  }
}
