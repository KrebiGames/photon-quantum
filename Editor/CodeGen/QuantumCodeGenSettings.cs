namespace Quantum.Editor {
  using System;
  using CodeGen;
  using UnityEditor;
  using UnityEditor.Build;
  using UnityEngine;

  /// <summary>
  /// Settings for the Quantum code generation. Extend this class with a partial implementation to customize the settings.
  /// </summary>
  [Obsolete("Prefer using " + nameof(QuantumCodeGenQtnSettings))]
  public static partial class QuantumCodeGenSettings {

    /// <summary>
    /// The default folder path for the Simulation generated code.
    /// </summary>
    public const string DefaultCodeGenQtnFolderPath = QuantumCodeGenQtnSettings.DefaultSimulationOutputPath;
    /// <summary>
    /// The default folder path for the Unity runtime generated code.
    /// </summary>
    public const string DefaultCodeGenUnityRuntimeFolderPath = QuantumCodeGenQtnSettings.DefaultViewOutputPath;
    /// <summary>
    /// The default code generation options. If <code>QUANTUM_ENABLE_MIGRATION</code> is defined,
    /// <see cref="GeneratorLegacyOptions.DefaultMigrationFlags"/> are used for <see cref="GeneratorOptions.LegacyCodeGenOptions"/>.
    /// </summary>
    [Obsolete("No longer used")]
    public static GeneratorOptions DefaultOptions => new() {
      LegacyCodeGenOptions = 
        (QuantumCodeGenQtnSettings.IsMigrationEnabled ? GeneratorLegacyOptions.DefaultMigrationFlags : default),
    };


    /// <summary>
    /// Creates a new instance of <see cref="GeneratorOptions"/> with the default options. Uses <see cref="DefaultOptions"/> and
    /// calls <see cref="GetOptionsUser"/> to customize the options.
    /// </summary>
    public static GeneratorOptions Options => QuantumCodeGenQtnSettings.Instance.GeneratorOptions;

    /// <summary>
    /// Returns the folder path for the Simulation generated code. Uses <see cref="DefaultCodeGenQtnFolderPath"/> and
    /// calls <see cref="GetCodeGenFolderPathUser"/> to customize the path.
    /// </summary>
    public static string CodeGenQtnFolderPath => QuantumCodeGenQtnSettings.Instance.SimulationOutputPath;

    /// <summary>
    /// Returns the folder path for the Unity runtime generated code. Uses <see cref="DefaultCodeGenUnityRuntimeFolderPath"/> and
    /// calls <see cref="GetCodeGenUnityRuntimeFolderPathUser"/> to customize the path.
    /// </summary>
    public static string CodeGenUnityRuntimeFolderPath => QuantumCodeGenQtnSettings.Instance.ViewOutputPath;
    
    /// <summary>
    /// Implement this method to customize the code generation options.
    /// </summary>
    /// <param name="options"></param>
    [Obsolete]
    static partial void GetOptionsUser(ref GeneratorOptions options);
    
    /// <summary>
    /// Implement this method to customize the code generation Simulation folder path.
    /// </summary>
    /// <param name="path"></param>
    [Obsolete]
    static partial void GetCodeGenFolderPathUser(ref string path);
    
    /// <summary>
    /// Implement this method to customize the code generation View folder path.
    /// </summary>
    /// <param name="path"></param>
    [Obsolete]
    static partial void GetCodeGenUnityRuntimeFolderPathUser(ref string path);
    
    public static void MigrateFromPartial(QuantumCodeGenQtnSettings settings) {
#pragma warning disable CS0612 // Type or member is obsolete
      GetCodeGenFolderPathUser(ref settings.SimulationOutputPath);
      GetCodeGenUnityRuntimeFolderPathUser(ref settings.ViewOutputPath);
      GetOptionsUser(ref settings.GeneratorOptions);
#pragma warning restore CS0612 // Type or member is obsolete
    }
  }
}