namespace Quantum {
  using System;
  using System.Collections.Generic;
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;
  using UnityEngine.Serialization;

  /// <summary>
  /// Unity component that holds and bakes the map data for a Quantum map from a given scene.
  /// </summary>
  [ExecuteInEditMode]
  public partial class QuantumMapData : QuantumMonoBehaviour {

    /// <summary>
    /// The source asset to bake the data into.
    /// </summary>
    [InlineHelp]
    [DisplayName("Asset")]
    public AssetRef<Map> AssetRef;
    
#if QUANTUM_ENABLE_QMAP
    /// <summary>
    /// Map settings.
    /// </summary>
    [InlineHelp, DrawInline, DrawIf("IsUsingScriptedImporter", true, CompareOperator.Equal, mode: DrawIfMode.Hide)]
    public QuantumMapDataSettings Settings;
#endif
    
    /// <summary>
    /// How the map data should be baked.
    /// </summary>
    [InlineHelp] 
#if QUANTUM_ENABLE_QMAP
    [DrawIf("IsUsingScriptedImporter", false, CompareOperator.Equal, mode: DrawIfMode.Hide)]
#endif
    public QuantumMapDataBakeFlags BakeAllMode = QuantumMapDataBakeFlags.BakeMapData | QuantumMapDataBakeFlags.GenerateAssetDB;

    /// <summary>
    /// <see cref="Quantum.NavMeshSerializeType"/>
    /// </summary>
    [InlineHelp]
#if QUANTUM_ENABLE_QMAP
    [DrawIf("IsUsingScriptedImporter", false, CompareOperator.Equal, mode: DrawIfMode.Hide)]
#endif
    public NavMeshSerializeType NavMeshSerializeType;
    
    
    /// <summary>
    /// One-to-one mapping of Quantum 2D static collider entries in QAssetMap to their original source scripts. 
    /// Purely for convenience to do post bake mappings and not required by the Quantum simulation.
    /// </summary>
    [Header("Baked Data")]
    [InlineHelp, ReadOnly]
    public List<QuantumStaticCollider2DSource> StaticCollider2DReferences = new();

    /// <summary>
    /// One-to-one mapping of Quantum 3D static collider entries in QAssetMap to their original source scripts. 
    /// Purely for convenience to do post bake mappings and not required by the Quantum simulation.
    /// </summary>
    [InlineHelp, ReadOnly]
    public List<QuantumStaticCollider3DSource> StaticCollider3DReferences = new();
    
    /// <summary>
    /// One-to-one mapping of Quantum map entity entries in QAssetMap to their original source scripts.
    /// </summary>
    [InlineHelp, ReadOnly]
    public List<QuantumEntityView> MapEntityReferences = new();

    void Update() {
      transform.position = Vector3.zero;
      transform.rotation = Quaternion.identity;
    }
    
    [Obsolete("Use GetAsset instead. To assign a value, use AssetRef field.", true)]
    [HideInInspector]
    public Map Asset;
    
    public Map GetAsset(bool forEditor) {
#if UNITY_EDITOR
      if (forEditor) {
        return QuantumUnityDB.GetGlobalAssetEditorInstance(AssetRef);
      }
#endif
      return QuantumUnityDB.GetGlobalAsset(AssetRef);
    }

    void OnValidate() {
#if QUANTUM_ENABLE_QMAP
      Settings.OnValidate();
#endif
      transform.position = new Vector3();
      
#pragma warning disable CS0612 // Type or member is obsolete
      if (TryMigrateAsset()) {
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.EditorUtility.SetDirty(gameObject);
#endif
      }
#pragma warning restore CS0612 // Type or member is obsolete

      [Obsolete]
      bool TryMigrateAsset() {
        if (!Asset) {
          return false;
        }
      
        AssetRef = Asset;
        Asset = null;
        return true;
      }
    }


    #if UNITY_EDITOR
    bool IsUsingScriptedImporter {
      get {
        if (QuantumUnityDB.TryGetGlobalAssetEditorInstance(AssetRef, out var map)) {
          return AssetDatabase.IsNativeAsset(map) == false;
        }

        return false;
      }
    }
    #endif
  }
  
#if QUANTUM_ENABLE_QMAP
  /// <summary/>
  [Serializable]
  public class QuantumMapDataSettings {
    /// <inheritdoc cref="Map.UserAsset"/>
    [Header("Map Settings")]
    [InlineHelp]
    public AssetRef UserAsset;
    
    /// <inheritdoc cref="Map.WorldSize"/>
    [Header("Physics Settings"), InlineHelp]
    public int WorldSize = 256;
    /// <inheritdoc cref="Map.BucketsCount"/>
    [InlineHelp]
    public int BucketsCount = 16;
    /// <inheritdoc cref="Map.BucketsSubdivisions"/>
    [InlineHelp]
    public int BucketsSubdivisions = 8;
    /// <inheritdoc cref="Map.BucketingAxis"/>
    [InlineHelp]
    public PhysicsCommon.BucketAxis BucketingAxis = PhysicsCommon.BucketAxis.X;
    /// <inheritdoc cref="Map.SortingAxis"/>
    [InlineHelp]
    public PhysicsCommon.SortAxis SortingAxis = PhysicsCommon.SortAxis.Y;
    /// <inheritdoc cref="Map.SceneMeshCellSize"/>
    [InlineHelp]
    public FP SceneMeshCellSize = FP._4;
    /// <summary>
    /// Should the mesh collider data be compressed?
    /// Only relevant if <see cref="QuantumStaticTerrainCollider3D"/> or <see cref="QuantumStaticMeshCollider3D"/> are used.
    /// </summary>
    [InlineHelp]
    public bool CompressSceneMesh = false;
    
    /// <inheritdoc cref="NavMesh.GridSizeX"/>
    [Header("NavMesh Settings"), InlineHelp]
    public Int32 GridSizeX;
    /// <inheritdoc cref="NavMesh.GridSizeY"/>
    [InlineHelp]
    public Int32 GridSizeY;
    /// <inheritdoc cref="NavMesh.GridNodeSize"/>
    [InlineHelp]
    public Int32 GridNodeSize;

    /// <inheritdoc cref="NavMesh.SerializeType"/>
    [InlineHelp, DisplayName("Serialize Type")]
    public NavMeshSerializeType NavMeshSerializeType;
    /// <summary/>
    [InlineHelp, DisplayName("Serialization Buffer Size")]
    public int NavMeshSerializationBufferSize = 1024 * 1024 * 60;
    /// <summary/>
    [InlineHelp]
    public bool CompressNavMeshes = false;
    
    internal void OnValidate() {
      if (GridSizeX < 2) {
        GridSizeX = 2;
      }

      if (GridSizeY < 2) {
        GridSizeY = 2;
      }

      if ((GridSizeX & 1) == 1) {
        GridSizeX += 1;
      }

      if ((GridSizeY & 1) == 1) {
        GridSizeY += 1;
      }

      if (GridNodeSize < 2) {
        GridNodeSize = 2;
      }

      if ((GridNodeSize & 1) == 1) {
        GridNodeSize += 1;
      }
    }

    internal void Apply(Map map) {
      map.UserAsset = UserAsset;
      map.WorldSize = WorldSize;
      map.BucketsCount = BucketsCount;
      map.BucketsSubdivisions = BucketsSubdivisions;
      map.BucketingAxis = BucketingAxis;
      map.SortingAxis = SortingAxis;
      map.SceneMeshCellSize = SceneMeshCellSize;
      
      map.GridSizeX = GridSizeX;
      map.GridSizeY = GridSizeY;
      map.GridNodeSize = GridNodeSize;
    }

    internal void CopyFrom(Map map) {
      UserAsset = map.UserAsset;
      WorldSize = map.WorldSize;
      BucketsCount = map.BucketsCount;
      BucketsSubdivisions = map.BucketsSubdivisions;
      BucketingAxis = map.BucketingAxis;
      SortingAxis = map.SortingAxis;
      SceneMeshCellSize = map.SceneMeshCellSize;
      
      GridSizeX = map.GridSizeX;
      GridSizeY = map.GridSizeY;
      GridNodeSize = map.GridNodeSize;
    }
  }
#endif
}