namespace Quantum {
  using System;
  using Photon.Deterministic;
  using Quantum.Core;
  using Quantum.Allocator;
#if QUANTUM_UNITY
  using UnityEditor;
  using UnityEngine;
  using HideInInspector = UnityEngine.HideInInspector;
#endif

  /// <summary>
  /// The SimulationConfig holds parameters used in the ECS layer and inside core systems like physics and navigation.
  /// </summary>
  public partial class SimulationConfig : AssetObject
#if QUANTUM_UNITY
    , ISerializationCallbackReceiver
#endif
  {
    /// <summary>
    /// Obsolete: Don't use the hard coded guids instead reference the simulation config used in the RuntimeConfig.
    /// </summary>
    [Obsolete("Don't use the hard coded guids instead reference the simulation config used in the RuntimeConfig")]
    public const long DEFAULT_ID = (long)DefaultAssetGuids.SimulationConfig;

    /// <summary>
    /// The scene load mode to use when changing Quantum maps.
    /// <para>Will trigger for example for the initial map that is set in Quantum by <see cref="RuntimeConfig.Map"/> and on subsequent map changes.</para>
    /// <para>The Unity scene referenced by <see cref="Quantum.Map.Scene"/> will be loaded.</para>
    /// </summary>
    public enum AutoLoadSceneFromMapMode {
      /// <summary>
      /// Automatic scene loading disabled.
      /// </summary>
      Disabled,
      /// <summary>
      /// Obsolete: unused.
      /// </summary>
      [Obsolete]
      Legacy,
      /// <summary>
      /// Unload the current scene then load the new scene.
      /// </summary>
      UnloadPreviousSceneThenLoad,
      /// <summary>
      /// Load the new scene then unload the current scene.
      /// </summary>
      LoadThenUnloadPreviousScene
    }

    /// <summary>
    /// Global entities settings.
    /// </summary>
    [Space, InlineHelp]
    public FrameBase.EntitiesConfig Entities;
    
    /// <summary>
    /// Global physics settings.
    /// </summary>
    [Space, InlineHelp]
    public PhysicsCommon.Config Physics;
    
    /// <summary>
    /// Global navmesh settings.
    /// </summary>
    [Space, InlineHelp]
    public Navigation.Config Navigation;

    /// <summary>
    /// Global heap settings.
    /// </summary>
    [Space, InlineHelp]
    public FrameHeapConfig Heap;

    #region Legacy

    [HideInInspector, Obsolete("Use Heap.TrackingMode")]
    public HeapTrackingMode HeapTrackingMode;
    [HideInInspector, Obsolete("Use Heap.PageShift")]
    public int HeapPageShift;
    [HideInInspector, Obsolete("Use Heap.PageCount")]
    public int HeapPageCount;
    [HideInInspector, Obsolete("Use Heap.ExtraHeapCount")]
    public int HeapExtraCount;
    [HideInInspector]
    public bool HeapSettingsMigrated;

    #endregion

    /// <summary>
    /// This option will trigger a Unity scene load during the Quantum start sequence.\n
    /// This might be convenient to start with but once the starting sequence is customized disable it and implement the scene loading by yourself.
    /// "Previous Scene" refers to a scene name in Quantum Map.
    /// </summary>
    [Space, InlineHelp]
    public AutoLoadSceneFromMapMode AutoLoadSceneFromMap = AutoLoadSceneFromMapMode.UnloadPreviousSceneThenLoad;

    /// <summary>
    /// Configure how the client tracks the time to progress the Quantum simulation from the QuantumRunner class.
    /// </summary>
    [Obsolete("Set on SessionRunner.Arguments.DeltaTimeType instead")]
    [HideInInspector]
    public SimulationUpdateTime DeltaTimeType = SimulationUpdateTime.Default;

    /// <summary>
    /// Override the number of threads used internally. Default is 2.
    /// </summary>
    [InlineHelp]
    [ErrorIf(nameof(ThreadCount), 0, "Thread Count must be greater than 0.", CompareOperator.LessOrEqual)]
    public int ThreadCount = 2;

    /// <summary>
    /// How long to store checksumed verified frames. They are used to generate a frame dump in case of a checksum error happening. Not used in Replay and Local mode. Default is 3.
    /// </summary>
    [InlineHelp]
    public FP ChecksumSnapshotHistoryLengthSeconds = 3;

    /// <summary>
    /// Additional options for checksum dumps, if the default settings don't provide a clear picture.
    /// </summary>
    [InlineHelp]
    public SimulationConfigChecksumErrorDumpOptions ChecksumErrorDumpOptions;

    /// <summary>
    /// The asset loaded callback, caches fixed calculation results.
    /// </summary>
    /// <param name="resourceManager">Resource manager.</param>
    public override void Loaded(IResourceManager resourceManager) {
      Physics.PenetrationCorrection = FPMath.Clamp01(Physics.PenetrationCorrection);
      ThreadCount = Math.Max(1, ThreadCount);
    }
    
#if QUANTUM_UNITY
    /// <summary>
    /// Unity Reset() method is used to initialize class fields with default values.
    /// </summary>
    public override void Reset() {
      Physics    = new PhysicsCommon.Config();
      Navigation = new Navigation.Config();
      Heap       = new FrameHeapConfig();

      // Automatic should be the default value, but in order to keep backwards compatibility we
      // use Manual as the default and only assign Automatic here in Reset, which is called for
      // newly created instances. This way existing instances are left in Manual mode and won't
      // unexpectedly start automatic imports.
      Physics.ImportMode = PhysicsCommon.PhysicsImportMode.Automatic;

      Physics.MatrixImportSource = PhysicsCommon.PhysicsMatrixImportSource._3D;

      ImportLayerListAndMatrixFromUnity();
    }

    private void OnValidate() {
      if (Physics.ImportMode == PhysicsCommon.PhysicsImportMode.Automatic) {
        // Import is cheap, so don't bother checking if the mode or source value actually changed and always import.
        ImportLayerListAndMatrixFromUnity();
      }
    }

    /// <summary>
    /// Physics 2D or 3D used for importing layers from Unity.
    /// </summary>
    [Obsolete("Replaced by " + nameof(PhysicsCommon.PhysicsImportMode) + " and " + nameof(PhysicsCommon.Config.MatrixImportSource))]
    public enum PhysicsType {
      /// <summary>
      /// Quantum Physics 3D.
      /// </summary>
      Physics3D,
      /// <summary>
      /// Quantum Physics 2D.
      /// </summary>
      Physics2D
    }

    [Obsolete("Use " + nameof(ImportLayerListAndMatrixFromUnity) + " instead")]
    public void ImportLayersFromUnity(PhysicsType physicsType = PhysicsType.Physics3D) {
      var source = physicsType == PhysicsType.Physics3D
        ? PhysicsCommon.PhysicsMatrixImportSource._3D
        : PhysicsCommon.PhysicsMatrixImportSource._2D;

      Physics.Layers = GetUnityLayerNameArray();
      Physics.LayerMatrix = GetUnityLayerMatrix(source);
    }

    /// <summary>
    /// Import Unity physics layer list and matrix specified in config's <see cref="PhysicsCommon.Config.MatrixImportSource"/>.
    /// </summary>
    public void ImportLayerListAndMatrixFromUnity() {
      ImportLayerListFromUnity();
      ImportLayerMatrixFromUnity();
    }

    /// <summary>
    /// Import Unity physics layer list.
    /// </summary>
    public void ImportLayerListFromUnity() {
      Physics.Layers = GetUnityLayerNameArray();
    }

    /// <summary>
    /// Import Unity physics layer matrix specified in config's <see cref="PhysicsCommon.Config.MatrixImportSource"/>.
    /// </summary>
    public void ImportLayerMatrixFromUnity() {
      Physics.LayerMatrix = GetUnityLayerMatrix(Physics.MatrixImportSource);
    }

    /// <summary>
    /// Creates 32 physics layer names from Unity.
    /// </summary>
    public static String[] GetUnityLayerNameArray() {
      var layers = new String[32];

      for (Int32 i = 0; i < layers.Length; ++i) {
        try {
          layers[i] = UnityEngine.LayerMask.LayerToName(i);
        } catch {
          // just eat exceptions
        }
      }

      return layers;
    }

    /// <summary>
    /// Creates 32 physics layer masks from Unity.
    /// </summary>
    /// <param name="source">Specifies the source of the layer masks: 3D or 2D</param>
    public static Int32[] GetUnityLayerMatrix(PhysicsCommon.PhysicsMatrixImportSource source) {
      var matrix = new Int32[32];

      for (Int32 a = 0; a < 32; ++a) {
        for (Int32 b = 0; b < 32; ++b) {
          bool ignoreLayerCollision = false;
          
          switch (source) {
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
            case PhysicsCommon.PhysicsMatrixImportSource._3D:
              ignoreLayerCollision = UnityEngine.Physics.GetIgnoreLayerCollision(a, b);
              break;
#endif
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
            case PhysicsCommon.PhysicsMatrixImportSource._2D:
              ignoreLayerCollision = UnityEngine.Physics2D.GetIgnoreLayerCollision(a, b);
              break;
#endif
            default:
              break;
          }

          if (ignoreLayerCollision == false) {
            matrix[a] |= (1 << b);
            matrix[b] |= (1 << a);
          }
        }
      }

      return matrix;
    }

    /// <inheritdoc cref="ISerializationCallbackReceiver.OnBeforeSerialize"/>
    public void OnBeforeSerialize() {
    }

    /// <inheritdoc cref="ISerializationCallbackReceiver.OnAfterDeserialize"/>
    public void OnAfterDeserialize() {
#if UNITY_EDITOR
      // 3.1 Migrating heap config
#pragma warning disable CS0618 // Type or member is obsolete
      if (HeapSettingsMigrated == false && HeapPageCount > 0) {
        Heap.PageShift = HeapPageShift;
        Heap.PageCount = HeapPageCount;
        Heap.TrackingMode = HeapTrackingMode;
        Heap.ExtraHeapCount = HeapExtraCount;
        // Force to new page based management and migrate page size, actual required size could be smaller
        // PageCount not required because heap grows dynamically now
        Heap.Management = HeapManagement.PageBased;
        Heap.PageSize = (HeapPageSize) Math.Clamp(HeapPageShift - 10, 0, (int)HeapPageSize.Size_128_KiB);
        HeapSettingsMigrated = true;
      }
#pragma warning restore CS0618 // Type or member is obsolete
#endif // UNITY_EDITOR
    }
#endif // QUANTUM_UNITY
  }

  /// <summary>
  /// Configuration options for checksum error dumps.
  /// </summary>
  [Flags]
  public enum SimulationConfigChecksumErrorDumpOptions {
    /// <summary>
    /// Sends asset db checksums.
    /// </summary>
    SendAssetDBChecksums = 1 << 0,
    /// <summary>
    /// Dumps readable information from the dynamic db.
    /// </summary>
    ReadableDynamicDB    = 1 << 1,
    /// <summary>
    /// Prints raw FP values.
    /// </summary>
    RawFPValues          = 1 << 2,
    /// <summary>
    /// Dumps component checksums.
    /// </summary>
    ComponentChecksums   = 1 << 3,
    /// <summary>
    /// Dumps 3D Physics SceneMesh metadata.
    /// </summary>
    SceneMesh3D          = 1 << 4,
  }
}
