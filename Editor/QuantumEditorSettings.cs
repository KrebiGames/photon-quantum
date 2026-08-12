namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Runtime.InteropServices;
  using UnityEditor;
  using UnityEditor.Build;
  using UnityEngine;
  using UnityEngine.Serialization;

  [QuantumProjectSettings("Project/Photon Quantum")]
  public partial class QuantumEditorSettings : ScriptableObject, IQuantumProjectSettings {

    public static QuantumEditorSettings Instance => QuantumProjectSettings.GetInstance<QuantumEditorSettings>();

    public static void Save() => QuantumProjectSettings.Save<QuantumEditorSettings>();
    
    /// <summary>
    /// The default path of the global Quantum editor settings asset.
    /// </summary>
    [Obsolete("No longer used")]
    public const string DefaultPath = "Assets/QuantumUser/Editor/QuantumEditorSettings.asset";
    
    /// <summary>
    /// Get the global Quantum editor settings instance and run the provided action and return result of a certain type.
    /// </summary>
    /// <typeparam name="T">Type to return</typeparam>
    /// <param name="check">The func to run on the settings.</param>
    /// <returns>Returns the result of the func or default T</returns>
    [Obsolete("Use Instance instead")]
    public static T? Get<T>(Func<QuantumEditorSettings, T> check) where T : struct {
      return check(Instance);
    }

    /// <summary>
    /// Get the global Quantum editor settings instance and run the provided action and return result of a certain type.
    /// </summary>
    /// <typeparam name="T">Type to return</typeparam>
    /// <param name="check">The func to run on the settings.</param>
    /// <param name="defaultValue">Return this when the settings have not been found.</param>
    /// <returns>Returns the result of the func</returns>
    [Obsolete("Use Instance instead")]
    public static T Get<T>(Func<QuantumEditorSettings, T> check, T defaultValue) {
      return check(Instance);
    }

    /// <summary/>
    [Obsolete("Use Instance instead.")]
    public static bool TryGetGlobal(out QuantumEditorSettings global) {
      global = Instance;
      return true;
    }
    
    /// <summary/>
    [Obsolete("Use Instance instead")]
    public static QuantumEditorSettings Global => Instance;
    
    /// <summary>
    /// Where to create new Quantum assets by default.
    /// </summary>
    [Obsolete("Use " + nameof(NewAssetsLocation))]
    public string DefaultNewAssetsLocation => NewAssetsLocation; 
    
    /// <summary>
    /// Location where Quantum is installed.
    /// </summary>
    [DirectoryPath]
    public string SdkRoot = 
#if QUANTUM_UPM
      "Packages/com.photonengine.quantum";
#else
      "Assets/Photon/Quantum";
#endif

    public static string GetSdkPath(string relative) {
      return $"{Instance.SdkRoot}/{relative}";
    }
    

    /// <summary>
    /// Locations that the QuantumUnityDB discovers Quantum assets.
    /// Changing this requires reimporting all Unity (Quantum) assets manually.
    /// </summary>
    [Header("Assets")]
    [DirectoryPath, InlineHelp]
    public string[] AssetSearchPaths = new[] { "Assets" };
    
    /// <summary>
    /// Where to create new Quantum assets by default.
    /// </summary>
    [InlineHelp, FormerlySerializedAs("DefaultNewAssetsLocation")]
    public string NewAssetsLocation = "Assets/QuantumUser/Resources";
    
#if QUANTUM_ENABLE_QMAP
    /// <summary>
    /// Default location of the bake cache folder.
    /// </summary>
    [InlineHelp, DirectoryPath]
    public string BakeCacheRoot = "Assets/QuantumUser/BakeCache";
#endif
    
    /// <summary>
    /// Automatically trigger bake on saving a scene.
    /// </summary>
    [Header("Baking")]
    [InlineHelp]
    public QuantumMapDataBakeFlags AutoBuildOnSceneSave = QuantumMapDataBakeFlags.BakeMapData;

    /// <summary>
    /// If set MapData will be automatically baked on entering play mode, on saving a scene and on building a player.
    /// </summary>
    [InlineHelp]
    public QuantumMapDataBakeFlags AutoBuildOnPlaymodeChanged = QuantumMapDataBakeFlags.BakeMapData;

    /// <summary>
    /// If set MapData will be automatically baked on building, on saving a scene and on building a player.
    /// </summary>
    [InlineHelp] 
    public QuantumMapDataBakeFlags AutoBuildOnBuild = QuantumMapDataBakeFlags.BakeMapData;
    
    /// <summary>
    /// A list of Quantum assets that enabled GUID Override. This list is tracked automatically.
    /// </summary>
    [Header("Assets That Have Non-Deterministic GUIDs")]
    [SerializeField]
    [DrawInline]
    [InlineHelp]
    private List<SerializableAssetEntry> AssetGuidOverrides = new();
    
    /// <summary>
    /// The post processor enables duplicating Quantum assets and prefabs and make sure a new GUID and correct path are set. This can make especially batched processes slow and can be toggled off here.
    /// </summary>
    [Header("Editor")]
    [InlineHelp]
    [ToggleLeft]
    public bool UseQuantumUnityDBAssetPostprocessor = true;

    /// <summary>
    /// If enabled a scene loading dropdown is displayed next to the play button.
    /// </summary>
    [InlineHelp]
    [ToggleLeft]
    public bool UseQuantumToolbarUtilities = false;

    /// <summary>
    /// Where to display the toolbar. Requires a domain reload after change.
    /// </summary>
    [InlineHelp]
    public QuantumToolbarZone QuantumToolbarZone = QuantumToolbarZone.ToolbarZoneRightAlign;

    /// <summary>
    /// If enabled a local PhotonPrivateAppVersion scriptable object is created to support the demo menu scene.
    /// </summary>
    [InlineHelp]
    [ToggleLeft]
    public bool UsePhotonAppVersionsPostprocessor = true;

    /// <summary>
    /// If enabled entity components are displayed inside of EntityPrototype inspector
    /// </summary>
    [InlineHelp]
    public QuantumEntityComponentInspectorMode EntityComponentInspectorMode = QuantumEntityComponentInspectorMode.InlineInEntityPrototypeAndHideMonoBehaviours;
    
    [NonSerialized]
    private readonly Dictionary<(GUID, long), SerializableAssetEntry> _assetIdToAssetGuidOverride = new();
    
    [Serializable]
    class SerializableAssetEntry {
      // for importing from legacy settings
      [HideInInspector, Obsolete, SerializeField]
      public LazyLoadReference<UnityEngine.Object> Asset;
      
      [UnityAssetGuid]
      public string UnityGuid;
      public long UnityFileId;
      public AssetGuid Guid;
    }
    
    internal bool TryGetAssetGuidOverride(GUID guid, long fileId, out AssetGuid assetGuid) {
      if (_assetIdToAssetGuidOverride.TryGetValue((guid, fileId), out var entry)) {
        assetGuid = entry.Guid;
        return true;
      } else {
        assetGuid = default;
        return false;
      }
    }
    
    internal bool SetGuidOverride(LazyLoadReference<UnityEngine.Object> asset, AssetGuid assetGuid, out AssetGuid previousGuid) {
      var (unityAssetGuid, unityFileId) = AssetDatabaseUtils.GetGUIDAndLocalFileIdentifierOrThrow(asset);
      
      var key = (unityAssetGuid, unityFileId);
      if (_assetIdToAssetGuidOverride.TryGetValue(key, out var entry)) {
        previousGuid = entry.Guid;
        if (assetGuid.IsValid) {
          entry.Guid = assetGuid.Value;
        } else {
          _assetIdToAssetGuidOverride.Remove(key);
          AssetGuidOverrides.Remove(entry);
        }
      } else {
        previousGuid = default;

        if (assetGuid.IsValid) {
          entry = new SerializableAssetEntry {
            UnityFileId = unityFileId,
            UnityGuid = unityAssetGuid.ToString(),
            Guid  = assetGuid.Value
          };

          _assetIdToAssetGuidOverride.Add(key, entry);
          AssetGuidOverrides.Add(entry);
        }
      }

      return assetGuid != previousGuid;
    }

    internal AssetGuid RemoveGuidOverride(GUID guid, long fileId) {
      if (_assetIdToAssetGuidOverride.TryGetValue((guid, fileId), out var entry)) {
        var result = entry.Guid;
        _assetIdToAssetGuidOverride.Remove((guid, fileId));
        AssetGuidOverrides.Remove(entry);
        return result;
      } else {
        return default;
      }
    }

    // touches AssetDatabase, so cannot be folded into RebuildOverridesDictionary (which runs from OnAfterDeserialize).
    /// <summary>
    /// Drops <see cref="AssetGuidOverrides"/> entries whose Unity GUID no longer resolves to an asset, then
    /// rebuilds the lookup dictionary. Returns the number of removed entries.
    /// </summary>
    bool RemoveOutdatedGuidOverrides() {
      int removed = 0;
      for (int i = AssetGuidOverrides.Count - 1; i >= 0; i--) {
        var entry = AssetGuidOverrides[i];
        var path = AssetDatabase.GUIDToAssetPath(entry.UnityGuid);
        if (string.IsNullOrEmpty(path)) {
          QuantumEditorLog.WarnImport($"Outdated AssetGuid override for {entry.UnityGuid} ({entry.Guid}), removing");
          AssetGuidOverrides.RemoveAt(i);
          ++removed;
        }
      }

      return removed > 0;
    }

    void IQuantumProjectSettings.LoadInternal(string path) {
      QuantumProjectSettings.LoadDefault(path, this);
      
      _assetIdToAssetGuidOverride.Clear();

      for (int i = AssetGuidOverrides.Count - 1; i >= 0; i--) {
        var entry = AssetGuidOverrides[i];
        
        var key = (new GUID(entry.UnityGuid), entry.UnityFileId);
        if (!_assetIdToAssetGuidOverride.TryAdd(key, entry)) {
          QuantumEditorLog.TraceImport($"Duplicate asset override for {key}");
          AssetGuidOverrides.RemoveAt(i);
        }
      }

      RemoveOutdatedGuidOverrides();
      AssetGuidOverrideDependency.Refresh();
    }
    
    void IQuantumProjectSettings.SaveInternal(string path) {
      QuantumProjectSettings.SaveDefault(path, this);
      RemoveOutdatedGuidOverrides();
      AssetGuidOverrideDependency.Refresh();
    }
    
    bool IQuantumProjectSettings.Migrate() {
#pragma warning disable CS0618 // Type or member is obsolete
#pragma warning disable CS0612
      // migrate from editor settings
      var editorSettings = AssetDatabase.FindAssets("t:Quantum.Editor.QuantumEditorSettings")
        .Select(AssetDatabase.GUIDToAssetPath)
        .Select(AssetDatabase.LoadAssetAtPath<QuantumEditorSettings>)
        .Where(x => x != null)
        .ToArray();

      if (editorSettings.Length == 0) {
        editorSettings = AssetDatabase.LoadAllAssetsAtPath(DefaultPath).OfType<QuantumEditorSettings>().ToArray();
      }

      if (editorSettings.Length == 0) {
        return false;
      }

      QuantumEditorLog.LogImport($"Migrating to {GetType().FullName} from {editorSettings[0]}");

      var src = editorSettings[0];
      AssetSearchPaths = (string[])src.AssetSearchPaths.Clone();
      NewAssetsLocation = src.NewAssetsLocation;
      AutoBuildOnSceneSave = src.AutoBuildOnSceneSave;
      AutoBuildOnPlaymodeChanged = src.AutoBuildOnPlaymodeChanged;
      AutoBuildOnBuild = src.AutoBuildOnBuild;
      UseQuantumUnityDBAssetPostprocessor = src.UseQuantumUnityDBAssetPostprocessor;
      UseQuantumToolbarUtilities = src.UseQuantumToolbarUtilities;
      QuantumToolbarZone = src.QuantumToolbarZone;
      UsePhotonAppVersionsPostprocessor = src.UsePhotonAppVersionsPostprocessor;
      EntityComponentInspectorMode = src.EntityComponentInspectorMode;

      foreach (var entry in src.AssetGuidOverrides) {
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(entry.Asset, out _, out _)) {
          Debug.LogWarning($"[Quantum Migration] Dropping AssetGuid override for missing asset (AssetGuid={entry.Guid})");
          continue;

        }

        SetGuidOverride(entry.Asset, entry.Guid, out _);
      }

      foreach (var settings in editorSettings) {
        QuantumEditorLog.LogImport($"Done migrating, deleting {settings}");
        AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(settings));
      }

      return true;
#pragma warning restore CS0612
#pragma warning restore CS0618 // Type or member is obsolete
    }


    [NonSerialized] LogSettingsDrawer _logSettingsDrawer;
    
    void IQuantumProjectSettings.OnSettingsGUI(SerializedObject serializedObject) {
      EditorGUILayout.Space();
      EditorGUILayout.LabelField("Build Features", EditorStyles.boldLabel);
      DrawScriptingDefineToggle(
        "Enable DebugDraw in Development Builds (current platform only)",
        "Toggles QUANTUM_DRAW_SHAPES scripting define for the current platform to enable/disable debug draw in development builds.",
        "QUANTUM_DRAW_SHAPES", allPlatforms: false);

      EditorGUI.BeginChangeCheck();
      DrawScriptingDefineToggle(
        "Enable Remote Task Profiler (current platform only)",
        "Toggles QUANTUM_ENABLE_REMOTE_PROFILER scripting define for the current platform.",
        "QUANTUM_ENABLE_REMOTE_PROFILER", allPlatforms: false);
      if (EditorGUI.EndChangeCheck()) {
        // remove legacy define
        AssetDatabaseExt.UpdateScriptingDefineSymbol("QUANTUM_REMOTE_PROFILER", false);
      }

      DrawScriptingDefineToggle(
        "Enable Quantum Graph Profiler (all platforms)",
        "Toggles QUANTUM_DISABLE_GRAPHPROFILER scripting define to enable/disable Quantum graph profiler code.",
        "QUANTUM_DISABLE_GRAPHPROFILER", allPlatforms: true, invertDefine: true);

      EditorGUILayout.Space();
      EditorGUILayout.LabelField("Quantum 2D", EditorStyles.boldLabel);
      DrawScriptingDefineToggle(
        "Enable Quantum XY (all platforms)",
        "Toggles QUANTUM_XY scripting define to enable/disable Quantum XY.",
        "QUANTUM_XY", allPlatforms: true);

      EditorGUILayout.Space();
      EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
      _logSettingsDrawer.DrawLayout(this, inlineHelp: true);
    }

    static void DrawScriptingDefineToggle(string label, string tooltip, string define, bool allPlatforms = false, bool invertDefine = false) {
      NamedBuildTarget buildTarget = default;
      bool? hasDefine;
      if (allPlatforms) {
        hasDefine = AssetDatabaseExt.HasScriptingDefineSymbol(define);
      } else {
        buildTarget = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));
        hasDefine   = AssetDatabaseExt.HasScriptingDefineSymbol(buildTarget, define);
      }

      var value = hasDefine ?? false;
      if (invertDefine) value = !value;

      EditorGUI.BeginChangeCheck();
      value = EditorGUI.ToggleLeft(EditorGUILayout.GetControlRect(), new GUIContent(label, tooltip), value);
      if (invertDefine) value = !value;

      if (EditorGUI.EndChangeCheck()) {
        if (allPlatforms) {
          AssetDatabaseExt.UpdateScriptingDefineSymbol(define, value);
        } else {
          AssetDatabaseExt.UpdateScriptingDefineSymbol(buildTarget, define, value);
        }
      }
    }

    public static readonly QuantumCustomDependency AssetGuidOverrideDependency = new ("QuantumUnityDBUtilitiesAssetGuidOverrideDependency", () => {
      Hash128 hash = new Hash128();
      foreach (var entry in Instance.AssetGuidOverrides) {
        hash.Append(entry.UnityGuid);
        hash.Append(entry.UnityFileId);
        hash.Append(entry.Guid);
      }
      return hash;
    });
  }
  
  
  
  /// <summary>
  /// The toolbar zone to display the Quantum toolbar.
  /// </summary>
  [Serializable]
  public enum QuantumToolbarZone {
    /// <summary>
    /// Show toolbar on the right side of the play button.
    /// </summary>
    ToolbarZoneRightAlign,
    /// <summary>
    /// Show the toolbar on the left side of the play button.
    /// </summary>
    ToolbarZoneLeftAlign
  }

  /// <summary>
  /// Entity component inspector mode.
  /// </summary>
  public enum QuantumEntityComponentInspectorMode {
    /// <summary>
    /// Show the mono behaviours.
    /// </summary>
    ShowMonoBehaviours,
    /// <summary>
    /// Inline entity prototype and show mono behaviours as stubs.
    /// </summary>
    InlineInEntityPrototypeAndShowMonoBehavioursStubs,
    /// <summary>
    /// Inline entity prototype and hide mono behaviours.
    /// </summary>
    InlineInEntityPrototypeAndHideMonoBehaviours,
  }
}