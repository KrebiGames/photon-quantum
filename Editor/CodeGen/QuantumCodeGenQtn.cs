namespace Quantum.Editor {
  using System;
  using System.Collections.Generic;
  using System.Diagnostics;
  using System.IO;
  using System.Linq;
  using CodeGen;
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEditor.Compilation;

  /// <summary>
  /// Quantum CodeGen for Qtn files. This type is placed in a standalone asmdef to make sure it can be compiled and run even in
  /// case of compile errors in other parts of the project.
  /// </summary>
  public static class QuantumCodeGenQtn {
    /// <summary>
    /// Runs the Quantum CodeGen for all <see cref="QuantumQtnAsset"/> in the project with default settings. Can be invoked with
    /// command line argument (-executeMethod Quantum.Editor.QuantumCodeGenQtn.Run).
    /// </summary>
    public static void Run() {
      Run(verbose: QuantumCodeGenQtnSettings.Instance.LogVerbose);
    }
    
    /// <summary>
    /// Runs the Quantum CodeGen for specific Qtn files with default settings.
    /// </summary>
    /// <param name="qtnFiles">Qtn files to be analyzed</param>
    /// <param name="verbose">Log verbose output</param>
    public static void Run(string[] qtnFiles, bool verbose) {
      Run(qtnFiles, verbose, null);
    }

    /// <summary>
    /// Runs the Quantum CodeGen for specific Qtn files with custom settings.
    /// </summary>
    /// <param name="qtnFiles">Qtn files to be analyzed</param>
    /// <param name="verbose">Log verbose output</param>
    /// <param name="options">CodeGen options</param>
    public static void Run(string[] qtnFiles, bool verbose, GeneratorOptions options) {
      Run(qtnFiles, verbose, options, QuantumCodeGenQtnSettings.Instance.SimulationOutputPath, QuantumCodeGenQtnSettings.Instance.ViewOutputPath);
    }

    /// <summary>
    /// Runs the Quantum CodeGen for specific Qtn files with custom settings.
    /// </summary>
    /// <param name="qtnFiles">Qtn files to be analyzed</param>
    /// <param name="verbose">Log verbose output</param>
    /// <param name="options">CodeGen options</param>
    /// <param name="simulationOutputFolder"></param>
    /// <param name="unityRuntimeOutputFolder"></param>
    /// <param name="deleteOrphanedFiles">If true, all files in target folder(s) that have not been generated will be removed</param>
    public static void Run(string[] qtnFiles, bool verbose, GeneratorOptions options, string simulationOutputFolder, string unityRuntimeOutputFolder, bool deleteOrphanedFiles = true) {
      if (qtnFiles == null) {
        throw new ArgumentNullException(nameof(qtnFiles));
      }
      
#if !QUANTUM_DISABLE_CODEGEN_MVID_CHECK
      if (!IsLatestQtnCodeGenLoaded()) {
        QuantumEditorLog.Error("Quantum CodeGen assembly has changed on disk, but was not reloaded. Code generation will not run. Please restart UnityEditor or fix compile errors.");
        return;
      }
#endif

      Action<string> logVerbose = verbose ? x => QuantumEditorLog.LogCodeGen(x) : (Action<string>)null;

      if (verbose) {
        logVerbose?.Invoke($"Starting Qtn CodeGen");
        foreach (var file in qtnFiles) {
          logVerbose?.Invoke($"Found {file}");
        }
      }

      var stopwatch = Stopwatch.StartNew();

      if (Directory.Exists(simulationOutputFolder) == false) {
        Directory.CreateDirectory(simulationOutputFolder);
      }
      
      IEnumerable<GeneratorOutputFile> outputFiles;
      try {
        outputFiles = Generator.Generate(qtnFiles, options ?? QuantumCodeGenQtnSettings.Instance.GeneratorOptions, warning => {
          string msg = "";
          if (!string.IsNullOrEmpty(warning.Path)) {
            msg += $"{warning.Path}({warning.Position}): ";
          }
          msg += warning.Message;
          if (!string.IsNullOrEmpty(warning.SourceCode)) {
            msg += $"\n{warning.SourceCode}";
          }
          QuantumEditorLog.WarnCodeGen(msg);
        });
      } catch (Exception ex) {
        // it seems that attempting to inspect StackTrace of the exception leads to a crash in Unity;
        // throwing like this will clear the original stack trace
        // ReSharper disable once PossibleIntendedRethrow
        throw ex;
      }
      
      if (simulationOutputFolder == unityRuntimeOutputFolder) {
        UpdateScriptsDirectory(simulationOutputFolder, outputFiles, logVerbose, deleteOrphanedFiles);  
      } else {
        
        bool IsUnitySpecific(GeneratorOutputFileKind kind) {
          switch (kind) {
            case GeneratorOutputFileKind.UnityPrototypeAdapters:
            case GeneratorOutputFileKind.UnityPrototypeWrapper:
            case GeneratorOutputFileKind.UnityLegacyAssetBase:
              return true;
            default:
              return false;
          }
        }

        var groups = outputFiles.ToLookup(x => IsUnitySpecific(x.Kind));

        UpdateScriptsDirectory(simulationOutputFolder, groups[false], logVerbose, deleteOrphanedFiles);
          
        if (!string.IsNullOrEmpty(unityRuntimeOutputFolder)) {
          UpdateScriptsDirectory(unityRuntimeOutputFolder, groups[true], logVerbose, deleteOrphanedFiles, p => {
            if (!QuantumCodeGenQtnSettings.IsMigrationEnabled) {
              return false;
            }
            
            if (p.StartsWith("QAsset")) {
              // TODO: remove after all the internal samples have been migrated
              return true; // early v3 style
            }

            // wrappers for components and wrappers
            if (!p.StartsWith("EntityComponent") && p.EndsWith("Asset.cs")) {
              return true;
            }
            // wrappers for generic assets
            if (p == "GenericAssets.cs") {
              return true;
            }
            

            return false;
          });
        }
      }
      
      QuantumEditorLog.LogCodeGen($"Finished Qtn CodeGen in {stopwatch.Elapsed}");
      AssetDatabase.Refresh();
    }

    /// <summary>
    /// Runs the Quantum CodeGen for all <see cref="QuantumQtnAsset"/> in the project with custom settings.
    /// </summary>
    /// <param name="verbose">Log verbose output</param>
    /// <param name="options">CodeGen options</param>
    public static void Run(bool verbose, GeneratorOptions options) {
      var assets = AssetDatabase.FindAssets($"t:{nameof(QuantumQtnAsset)}")
        .Select(x => AssetDatabase.GUIDToAssetPath(x))
        .Where(x => {
          var importer = AssetImporter.GetAtPath(x) as QuantumQtnAssetImporter;
          return importer == null || importer.UseCustomSettings == false;
        })
        .OrderBy(x => Path.GetFileNameWithoutExtension(x), StringComparer.OrdinalIgnoreCase)
        .ToArray();
      Run(assets, verbose, options);
    }
    
    /// <summary>
    /// Runs the Quantum CodeGen for all <see cref="QuantumQtnAsset"/> in the project with default settings.
    /// </summary>
    /// <param name="verbose">Log verbose output</param>
    public static void Run(bool verbose) {
      Run(verbose, null);
    }
    
    static string AppendFileIndex(string file, int index) {
      if (index == 0) {
        return file;
      }

      return Path.GetFileNameWithoutExtension(file) + $".Part{index+1}" + Path.GetExtension(file);
    }
    
    static void UpdateScriptsDirectory(string outputDir, IEnumerable<GeneratorOutputFile> files, Action<string> logProgress, bool deleteOrphaned = true, Predicate<string> ignoreFilter = null) {
      logProgress?.Invoke($"Generating scripts to {outputDir}");
      Directory.CreateDirectory(outputDir); // Create a directory first, because it might not exist.
      
      var generatedFiles = new HashSet<string>();
      var outputDirFullName = Path.GetFullPath(outputDir);

      foreach (var file in files) {
        for (int i = 0; i < file.Contents?.Length; ++i) {
          if (string.IsNullOrEmpty(file.Contents[i])) {
            continue;
          }

          var fileName = AppendFileIndex(file.Name, i);
          if ((string.IsNullOrEmpty(file.UserFolder) || Path.GetFullPath(file.UserFolder) == outputDirFullName) && !generatedFiles.Add(fileName)) {
            throw new InvalidOperationException($"File already generated: {fileName}");
          }
          UpdateFile(fileName, file.Contents[i], file.FormerNames, file.UserFolder);  
        }
      }

      if (!deleteOrphaned) {
        return;
      }
      
      var orphanedFiles = Directory.GetFiles(outputDir, "*.cs")
       .Select(x => Path.GetFileName(x))
       .Where(x => !generatedFiles.Contains(Path.GetFileName(x)))
       .ToList();

      foreach (var fileName in orphanedFiles) {
        if (ignoreFilter?.Invoke(fileName) == true) {
          logProgress?.Invoke($"Ignoring " + fileName);
          continue;
        }
        
        var filePath = Path.Combine(outputDir, fileName);
        
        if (File.Exists(filePath + ".meta")) {
          File.Delete(filePath + ".meta");
        }

        logProgress?.Invoke($"Deleting " + fileName);
        File.Delete(filePath);
      }

      void UpdateFile(string fileName, string contents, IList<string> formerNames, string userFolder) {
        var outputPath = Path.Combine(string.IsNullOrEmpty(userFolder) ? outputDir : userFolder, fileName);

        if (formerNames?.Count > 0 && !File.Exists(outputPath)) {
          // find the first match
          foreach (var formerName in formerNames) {
            var formerPath = Path.Combine(outputDir, formerName);
            if (File.Exists(formerPath)) {
              logProgress?.Invoke($"{outputPath} (moved from {formerPath})");
              File.Move(formerPath, outputPath);
              if (File.Exists(formerPath + ".meta")) {
                File.Move(formerPath + ".meta", outputPath + ".meta");
              }
              break;
            }
          }
        }
        
        
        if (AreContentsTheSame(contents, outputPath)) {
          logProgress?.Invoke($"{outputPath} (Skip)");
          return;
        }

        logProgress?.Invoke($"{outputPath} (Written)");
        File.WriteAllText(outputPath, contents);
      }
      
      bool AreContentsTheSame(string contents, string path) {
        if (!File.Exists(path)) {
          return false;
        }
    
        var oldContents = File.ReadAllText(path);
        if (oldContents.Length != contents.Length) {
          return false;
        }

        ulong checksumNew, checksumOld;
        checksumNew = CRC64.Calculate(0, contents);
        checksumOld = CRC64.Calculate(0, oldContents);
        return checksumNew == checksumOld;
      }
    }
    
#if !QUANTUM_DISABLE_CODEGEN_MVID_CHECK
    static bool IsLatestQtnCodeGenLoaded() {
      var assembly = typeof(Generator).Assembly;
      Guid onDiskGuid;
      try {
        onDiskGuid = Quantum.CodeGen.DllUtils.ReadModuleVersionId(QuantumPlatform.GetLoadedPath(assembly));
      } catch (Exception ex) {
        QuantumEditorLog.Warn($"Error while trying to read CodeGen assembly MVID, assuming DLL is up to date: {ex}");
        return true;
      }

      return onDiskGuid == assembly.ManifestModule.ModuleVersionId;
    }
#endif

    const int MenuPriority = 4000;
    
    [MenuItem("Tools/Quantum/CodeGen/Run Qtn CodeGen", priority = MenuPriority + 1)]
    static void MenuItemRunNonVerbose() {
      Run(false);
    }
    
    [MenuItem("Tools/Quantum/CodeGen/Run Qtn CodeGen (verbose)", priority = MenuPriority + 2)]
    static void MenuItemRunVerbose() {
      Run(true);
    }
  }
}