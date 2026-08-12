namespace Quantum.Editor {
  using System.Diagnostics.CodeAnalysis;
  using CodeGen;
  using UnityEditor;
  using UnityEditor.Build;
  using UnityEngine;

  [QuantumProjectSettings("Project/Photon Quantum/Code Gen")]
  public class QuantumCodeGenQtnSettings : UnityEngine.ScriptableObject, IQuantumProjectSettings {
    /// <summary>
    /// The default folder path for the Simulation generated code.
    /// </summary>
    public const string DefaultSimulationOutputPath = "Assets/QuantumUser/Simulation/Generated";
    /// <summary>
    /// The default folder path for the Unity runtime generated code.
    /// </summary>
    public const string DefaultViewOutputPath = "Assets/QuantumUser/View/Generated";
    
    /// <summary>
    /// Folder path for the Simulation generated code.
    /// </summary>
    [DirectoryPath, InlineHelp]
    public string SimulationOutputPath = DefaultSimulationOutputPath;

    /// <summary>
    /// Folder path for the Unity runtime generated code.
    /// </summary>
    [DirectoryPath, InlineHelp]
    public string ViewOutputPath = DefaultViewOutputPath;
    
    /// <summary>
    /// Enable if detailed logging is needed.
    /// </summary>
    [InlineHelp]
    public bool LogVerbose;

    [Header("Advanced")]
    [InlineHelp]
    public GeneratorOptions GeneratorOptions = new();
      
      
    void IQuantumProjectSettings.OnSettingsGUI(SerializedObject serializedObject) {
      EditorGUILayout.Space();
      if (GUILayout.Button("Generate Now")) {
        QuantumCodeGenQtn.Run(LogVerbose);
      }
    }

    bool IQuantumProjectSettings.Migrate() {
#pragma warning disable CS0618 // Type or member is obsolete
      QuantumCodeGenSettings.MigrateFromPartial(this);
#pragma warning restore CS0618 // Type or member is obsolete
      return true;
    }
    
    [NotNull]
    public static QuantumCodeGenQtnSettings Instance => QuantumProjectSettings<QuantumCodeGenQtnSettings>.Instance;
    
    internal static bool IsMigrationEnabled => HasDefine("QUANTUM_ENABLE_MIGRATION");
      
    private static bool HasDefine(string define) {
      var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup)).Split(';');
      return System.Array.IndexOf(defines, define) >= 0;
    }
  }
}