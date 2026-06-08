#if QUANTUM_ENABLE_QMAP
namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Diagnostics;
  using System.IO;
  using System.Linq;
  using System.Reflection;
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEditor.AssetImporters;
  using Unity.Profiling;
  using UnityEditorInternal;
  using UnityEngine;
  using UnityEngine.Pool;
  using UnityEngine.Serialization;
  using Object = UnityEngine.Object;
#if UNITY_EDITOR && !UNITY_2022_2_OR_NEWER
  using ManagedReferenceUtility = UnityEditor.SerializationUtility;
#endif


  [ScriptedImporter(4, Extension, ImportQueueOffset)]
  [QuantumAssetObjectScriptedImporter(typeof(Quantum.Map))]
  partial class QuantumMapImporter : ScriptedImporter {
    public const int ImportQueueOffset = 100100;
    public const string Extension = "qmap";
    public const string ExtensionWithDot = "." + Extension;
    /// <summary>
    /// Default location of the bake cache folder, used when <see cref="OverrideBakeCacheRoot"/> is not implemented or leaves the value unchanged.
    /// </summary>
    public const string DefaultBakeCacheRoot = "Assets/QuantumUser/BakeCache";

    /// <summary>
    /// Project-relative folder under which baked <c>.qmapdata</c> and <c>.qnavdata</c> files are written.
    /// Resolves to <see cref="DefaultBakeCacheRoot"/> unless a partial implementation of <see cref="OverrideBakeCacheRoot"/> assigns a different path.
    /// </summary>
    public static string BakeCacheRoot {
      get {
        var root = DefaultBakeCacheRoot;
        OverrideBakeCacheRoot(ref root);
        return root;
      }
    }

    /// <summary>
    /// Optional extension point for relocating the bake cache folder. Implement as a second partial of <see cref="QuantumMapImporter"/>
    /// and assign the desired project-relative path to <paramref name="root"/>. Leave unchanged to keep <see cref="DefaultBakeCacheRoot"/>.
    /// </summary>
    /// <param name="root">Seeded with <see cref="DefaultBakeCacheRoot"/>; overwrite to change where bake cache files are stored.</param>
    static partial void OverrideBakeCacheRoot(ref string root);

    const string NavMeshAssetIdPrefix = "nm-";
    const string NavMeshDataAssetIdPrefix = "nmdata-";
    const string MapAssetId = "main";
    const string MeshAssetId = "mesh";

    static readonly ProfilerMarker s_importMarker = new($"{nameof(QuantumMapImporter)}.{nameof(OnImportAsset)}");
    static readonly ProfilerMarker s_loadMapMarker = new($"{nameof(QuantumMapImporter)}.LoadMapBakeCache");
    static readonly ProfilerMarker s_loadNavMarker = new($"{nameof(QuantumMapImporter)}.LoadNavBakeCache");
    static readonly ProfilerMarker s_bakeMapMarker = new($"{nameof(QuantumMapImporter)}.{nameof(BakeCollidersAndPrototypes)}");
    static readonly ProfilerMarker s_bakeNavMarker = new($"{nameof(QuantumMapImporter)}.{nameof(BakeNavMesh)}");

    static readonly Lazy<Type[]> s_callbackTypes = new(() => {
      var list = new List<Type>();
      foreach (var t in TypeCache.GetTypesDerivedFrom<MapDataBakerCallback>()) {
        if (t.IsAbstract || t.IsGenericTypeDefinition) continue;
        var asmAttr = t.Assembly.GetCustomAttribute<QuantumMapBakeAssemblyAttribute>();
        if (asmAttr == null || asmAttr.Ignore) continue;
        list.Add(t);
      }

      list.Sort((a, b) =>
        (a.GetCustomAttribute<MapDataBakerCallbackAttribute>()?.InvokeOrder ?? 0)
        - (b.GetCustomAttribute<MapDataBakerCallbackAttribute>()?.InvokeOrder ?? 0));
      return list.ToArray();
    });

    static void InvokeCallbacks(Action<MapDataBakerCallback> action) {
      foreach (var t in s_callbackTypes.Value) {
        try {
          action((MapDataBakerCallback)Activator.CreateInstance(t));
        } catch (Exception ex) {
          Quantum.Log.Exception(ex);
        }
      }
    }

    public override void OnImportAsset(AssetImportContext ctx) {
      using var _ = s_importMarker.Auto();
      var sw = Stopwatch.StartNew();

      QuantumEditorLog.TraceImport(ctx.assetPath, $"Importing with {GetType().FullName}");

      var guid = AssetDatabase.GUIDFromAssetPath(ctx.assetPath);

      var mapBakeCachePath = $"{BakeCacheRoot}/{guid}.qmapdata";
      var navBakeCachePath = $"{BakeCacheRoot}/{guid}.qnavdata";

      ctx.DependsOnSourceAsset(mapBakeCachePath);
      ctx.DependsOnSourceAsset(navBakeCachePath);

      var baseName = Path.GetFileNameWithoutExtension(ctx.assetPath);
      Map mapAsset = null;

      if (File.Exists(mapBakeCachePath)) {
        using var __ = s_loadMapMarker.Auto();
        try {
          var mapDesc = LoadDescriptor(mapBakeCachePath);

          if (mapDesc.Map) {
            mapAsset = mapDesc.Map;
            AddBakedAsset(mapAsset, MapAssetId, baseName, parentAssetType: null);

            if (mapDesc.CollisionMesh) {
              AddBakedAsset(mapDesc.CollisionMesh, MeshAssetId, $"{baseName}_mesh", parentAssetType: typeof(Map));
              mapAsset.StaticColliders3DTrianglesData = mapDesc.CollisionMesh;
            } else {
              mapAsset.StaticColliders3DTrianglesData = default;
            }
          }
        } catch (Exception ex) {
          ctx.LogImportError(ex.Message);
        }
      }

      if (mapAsset == null) {
        // fallback so the asset still resolves to a Map
        mapAsset = ScriptableObject.CreateInstance<Map>();
        AddBakedAsset(mapAsset, MapAssetId, baseName, parentAssetType: null);
      }

      if (File.Exists(navBakeCachePath)) {
        using var __ = s_loadNavMarker.Auto();
        try {
          var navDesc = LoadDescriptor(navBakeCachePath);

          if (navDesc.NavMeshes != null) {
            foreach (var entry in navDesc.NavMeshes) {
              AddBakedAsset(entry.NavMesh, $"{NavMeshAssetIdPrefix}{entry.NavMesh.Name}", $"{baseName}_{entry.NavMesh.Name}", parentAssetType: typeof(Map));
              AddBakedAsset(entry.Data, $"{NavMeshDataAssetIdPrefix}{entry.NavMesh.Name}", $"{baseName}_{entry.NavMesh.Name}_data", parentAssetType: typeof(Map));
              entry.NavMesh.DataAsset = entry.Data;
            }

            mapAsset.NavMeshAssets = navDesc.NavMeshes.Select(e => new AssetRef<NavMesh>(e.NavMesh)).ToArray();
          }

          if (navDesc.Regions != null) {
            mapAsset.Regions = navDesc.Regions;
          }
        } catch (Exception ex) {
          ctx.LogImportError(ex.Message);
        }
      }

      QuantumEditorLog.TraceImport(ctx.assetPath, $"Imported in {sw.ElapsedMilliseconds}ms");

      void AddBakedAsset(AssetObject asset, string assetId, string assetName, Type parentAssetType) {
        asset.name = assetName;
        asset.Guid = QuantumUnityDBUtilities.GetExpectedAssetGuid(guid, AssetDatabaseUtils.GetLocalFileIdentifier(asset, assetId), out var _);
        asset.Path = QuantumUnityDBUtilities.GetExpectedAssetPath(ctx.assetPath, assetName, parentAssetType);
        ctx.AddObjectToAsset(assetId, asset);
      }

      static QuantumMapBakedDataDescriptor LoadDescriptor(string path) {
        var objects = InternalEditorUtility.LoadSerializedFileAndForget(path);
        if (objects == null || objects.Length == 0) {
          throw new InvalidOperationException($"No objects found in {path}");
        }

        var root = objects[0];
        if (root == null) {
          throw new InvalidOperationException($"Root object is null in {path}");
        }

        if (root is not QuantumMapBakedDataDescriptor descriptor) {
          throw new InvalidOperationException($"Expected {nameof(QuantumMapBakedDataDescriptor)} at root of {path}, got {root.GetType().Name}");
        }

        return descriptor;
      }
    }

    public void BakeCollidersAndPrototypes(QuantumMapData mapData, QuantumMapDataBaker.BuildTrigger trigger = QuantumMapDataBaker.BuildTrigger.Manual, QuantumMapDataBakeFlags bakeFlags = QuantumMapDataBakeFlags.None) {
      using var _ = s_bakeMapMarker.Auto();
      var sw = Stopwatch.StartNew();

      QuantumEditorLog.TraceImport(assetPath, $"Bake cache");

      var guid = AssetDatabase.GUIDFromAssetPath(assetPath);
      var path = $"{BakeCacheRoot}/{guid}.qmapdata";
      Directory.CreateDirectory(BakeCacheRoot);
      var scene = mapData.gameObject.scene;
      var mapSettings = mapData.Settings;
      Map mapAsset = ScriptableObject.CreateInstance<Map>();
      
      
      var descriptor = ScriptableObject.CreateInstance<QuantumMapBakedDataDescriptor>();
      descriptor.Map = mapAsset;

      var assets = new List<Object> { descriptor, mapAsset };

      mapAsset.Scene = Path.GetFileNameWithoutExtension(scene.path);
      mapAsset.SceneGuid = AssetDatabase.GUIDFromAssetPath(scene.path).ToString();
      mapAsset.ScenePath = scene.path;

      mapAsset.UserAsset = mapSettings.UserAsset;
      mapAsset.WorldSize = mapSettings.WorldSize;
      mapAsset.BucketsCount = mapSettings.BucketsCount;
      mapAsset.BucketsSubdivisions = mapSettings.BucketsSubdivisions;
      mapAsset.BucketingAxis = mapSettings.BucketingAxis;
      mapAsset.SortingAxis = mapSettings.SortingAxis;
      mapAsset.SceneMeshCellSize = mapSettings.SceneMeshCellSize;
      mapAsset.GridSizeX = mapSettings.GridSizeX;
      mapAsset.GridSizeY = mapSettings.GridSizeY;
      mapAsset.GridNodeSize = mapSettings.GridNodeSize;

      InvokeCallbacks(cb => cb.OnBeforeBake(mapData, trigger, bakeFlags));
      InvokeCallbacks(cb => cb.OnBeforeBake(mapData));

      // baking mutates the live sub-asset in place; reset accumulating collections from any prior bake
      mapAsset.CollidersManagedTriangles = new();

      using (ListPool<GameObject>.Get(out var roots)) {
        scene.GetRootGameObjectsInHierarchyOrder(roots);

        // colliders 2d
        using (var bakeContext = new QuantumStaticCollider2DBakeContext()) {
          var colliders = QuantumUnitySceneManagerUtils.GetComponentsInHierarchyOrder<QuantumStaticCollider2DSource>(roots).ToList();
          InvokeCallbacks(cb => cb.OnCollectColliders2D(mapData, colliders));
          foreach (var collider in colliders) {
            collider.GetColliders(bakeContext);
          }
          
          mapAsset.StaticColliders2D = bakeContext.Colliders.ToArray();
        }

        // colliders 3d
        // TODO: handle terrains properly; need to add asset dependency it seems
        using (var bakeContext = new QuantumStaticCollider3DBakeContext()) {
          var colliders = QuantumUnitySceneManagerUtils.GetComponentsInHierarchyOrder<QuantumStaticCollider3DSource>(roots).ToList();
          InvokeCallbacks(cb => cb.OnCollectColliders3D(mapData, colliders));
          foreach (var collider in colliders) {
            collider.GetColliders(bakeContext);
          }

          mapAsset.StaticColliders3D = bakeContext.Colliders.ToArray();
          foreach (var triangle in bakeContext.MeshTriangles) {
            mapAsset.CollidersManagedTriangles.Add(triangle.MeshColliderIndex, triangle);
          }
        }

        // prototypes
        {
          var prototypes = QuantumUnitySceneManagerUtils.GetComponentsInHierarchyOrder<QuantumEntityPrototypeSource>(roots);
          
          using (var bakeContext = new QuantumEntityPrototypeBakeContext(new QuantumEntityPrototypeConverter(mapData, prototypes))) {
            mapAsset.MapEntities = new ComponentPrototypeSet[prototypes.Length];
            for (int i = 0; i < prototypes.Length; ++i) {
              prototypes[i].GetPrototypes(bakeContext);
              mapAsset.MapEntities[i] = bakeContext.Flush();
            }
          }
          
          // stable serialized ids derived from GlobalObjectId
          var globalIds = new GlobalObjectId[prototypes.Length];
          GlobalObjectId.GetGlobalObjectIdsSlow(prototypes.Cast<Object>().ToArray(), globalIds);
          for (int i = 0; i < mapAsset.MapEntities.Length; ++i) {
            var globalId = globalIds[i];
            var components = mapAsset.MapEntities[i].Components;
            uint hash = 0;
            hash = HashStep(globalId.identifierType, hash);
            hash = HashStep(globalId.assetGUID, hash);
            hash = HashStep(globalId.targetObjectId, hash);
            hash = HashStep(globalId.targetPrefabId, hash);

            // leave the highest bit intact, some negative refIds are used for special cases by Unity
            long refIdBase = (long)hash << 31;
            for (int j = 0; j < components.Length; ++j) {
              ManagedReferenceUtility.SetManagedReferenceIdForObject(mapAsset, components[j], refIdBase + j);
            }
          }
        }
      }

      if (mapAsset.CollidersManagedTriangles.Count > 0) {
        var stream = new ByteStream(new byte[mapAsset.GetStaticColliderTrianglesSerializedSize(isWriting: true)]);
        mapAsset.SerializeStaticColliderTriangles(stream, true);

        var collisionMesh = ScriptableObject.CreateInstance<BinaryData>();
        collisionMesh.SetData(stream.ToArray(), compress: mapSettings.CompressSceneMesh);
        descriptor.CollisionMesh = collisionMesh;
        assets.Add(collisionMesh);
      }

      InvokeCallbacks(cb => cb.OnBake(mapData));

      InternalEditorUtility.SaveToSerializedFileAndForget(assets.ToArray(), path, false);

      QuantumEditorLog.TraceImport(assetPath, $"Baked cache in {sw.ElapsedMilliseconds}ms");

      static unsafe uint HashStep<T>(T data, uint initialHash) where T : unmanaged {
        var hash = initialHash;
        var ptr = (byte*)&data;
        for (var i = 0; i < sizeof(T); ++i) {
          hash = hash * 31 + ptr[i];
        }

        return hash;
      }
    }

    public void BakeNavMesh(QuantumMapData mapData) {
      using var _ = s_bakeNavMarker.Auto();
      var sw = Stopwatch.StartNew();

      QuantumEditorLog.TraceImport(assetPath, $"Bake nav mesh");

      var guid = AssetDatabase.GUIDFromAssetPath(assetPath);
      var path = $"{BakeCacheRoot}/{guid}.qnavdata";
      Directory.CreateDirectory(BakeCacheRoot);

      var descriptor = ScriptableObject.CreateInstance<QuantumMapBakedDataDescriptor>();
      var assets = new List<Object> { descriptor };

      var mapSettings = mapData.Settings;

      InvokeCallbacks(cb => cb.OnBeforeBakeNavMesh(mapData));

      // collect bake data + parallel source names
      var allBakeData = new List<NavMeshBakeData>();

#if QUANTUM_ENABLE_AI_NAVIGATION && !QUANTUM_DISABLE_AI_NAVIGATION
      foreach (var source in mapData.GetComponentsInChildren<QuantumMapNavMeshUnity>()) {
        if (!source.isActiveAndEnabled) {
          continue;
        }
        
        var bakeData = source.CreateBakeData();
        if (bakeData == null) {
          continue;
        }

        allBakeData.Add(bakeData);
      }
#endif

      // callbacks may add more bake data
      InvokeCallbacks(cb => cb.OnCollectNavMeshBakeData(mapData, allBakeData));

      // union of regions, MainArea pinned at index 0
      var allRegions = new List<string> { "MainArea" };
      var comparer = StringComparer.Ordinal;
      foreach (var bakeData in allBakeData) {
        foreach (var region in bakeData.Regions) {
          if (comparer.Equals(allRegions[0], region)) {
            continue;
          }

          var index = allRegions.BinarySearch(1, allRegions.Count - 1, region, comparer);
          if (index > 0) {
            continue;
          }
          allRegions.Insert(~index, region);
        }
      }

      var lookup = new Dictionary<string, int>(StringComparer.Ordinal);
      foreach (var region in allRegions) {
        lookup.Add(region, lookup.Count);
      }

      // bake each NavMesh
      var navMeshes = new List<NavMesh>(allBakeData.Count);
      for (int i = 0; i < allBakeData.Count; ++i) {
        var bakeData = allBakeData[i];
        var navMesh = ScriptableObject.CreateInstance<NavMesh>();
        navMesh.GridSizeX = mapSettings.GridSizeX;
        navMesh.GridSizeY = mapSettings.GridSizeY;
        navMesh.GridNodeSize = mapSettings.GridNodeSize;
        navMesh.WorldOffset = new FPVector2(-mapSettings.GridSizeX * mapSettings.GridNodeSize / 2, -mapSettings.GridSizeY * mapSettings.GridNodeSize / 2);
        navMesh.BakeData = bakeData;
        navMesh.BakeData.Regions = allRegions.ToArray();
        navMesh.SerializeType = mapSettings.NavMeshSerializeType;

        NavMeshBaker.BakeIntoExistingNavMesh(navMesh.BakeData, navMesh, null, regions: lookup);
        navMeshes.Add(navMesh);
      }

      InvokeCallbacks(cb => cb.OnCollectNavMeshes(mapData, navMeshes));

      // serialize
      ByteStream byteStream = null;
      var entries = new QuantumMapBakedDataDescriptor.NavMeshEntry[navMeshes.Count];
      for (int i = 0; i < navMeshes.Count; ++i) {
        var navMesh = navMeshes[i];
        if (byteStream == null) {
          byteStream = new ByteStream(new byte[mapSettings.NavMeshSerializationBufferSize]);
        } else {
          byteStream.Reset();
        }

        navMesh.Serialize(byteStream, true);

        var data = ScriptableObject.CreateInstance<BinaryData>();
        data.SetData(byteStream.ToArray(), mapSettings.CompressNavMeshes);

        entries[i] = new () {
          NavMesh = navMesh, 
          Data = data
        };

        assets.Add(navMesh);
        assets.Add(data);
      }

      descriptor.Regions = allRegions.ToArray();
      descriptor.NavMeshes = entries;

      InvokeCallbacks(cb => cb.OnBakeNavMesh(mapData));

      if (navMeshes.Count == 0) {
        // nothing to bake — drop any stale .qnavdata so the .qmap reimports without nav
        if (File.Exists(path)) {
          AssetDatabase.DeleteAsset(path);
        }
        QuantumEditorLog.TraceImport(assetPath, $"Skipped nav mesh write (no navmeshes); took {sw.ElapsedMilliseconds}ms");
        return;
      }

      InternalEditorUtility.SaveToSerializedFileAndForget(assets.ToArray(), path, false);

      QuantumEditorLog.TraceImport(assetPath, $"Baked nav mesh in {sw.ElapsedMilliseconds}ms");
    }
  }
}
#endif