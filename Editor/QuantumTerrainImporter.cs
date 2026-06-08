namespace Quantum.Editor {
#if QUANTUM_ENABLE_TERRAIN && !QUANTUM_DISABLE_TERRAIN
  using System.IO;
  using UnityEditor;
  using UnityEditor.AssetImporters;
  using UnityEngine;

  [ScriptedImporter(1, Extension, 100002)]
  [QuantumAssetObjectScriptedImporter(typeof(Quantum.TerrainCollider))]
  public class QuantumTerrainImporter : ScriptedImporter {
    public const string Extension = "qterrain";
    public const string ExtensionWithDot = "." + Extension;

    /// <summary>
    /// Source terrain data.
    /// </summary>
    [InlineHelp, WarnIf(nameof(TerrainData), 0, "Source " + nameof(UnityEngine.TerrainData) + " not set", AsBox = true)]
    public LazyLoadReference<TerrainData> TerrainData;

    /// <inheritdoc cref="QuantumStaticTerrainCollider3D.BakeResolutionDivisor"/>
    [InlineHelp]
    public Quantum.TerrainBakeResolutionDivisor BakeResolutionDivisor = TerrainBakeResolutionDivisor.MatchSource;
    
    public override void OnImportAsset(AssetImportContext ctx) {
      if (!TerrainData.isSet) {
        return;
      }
      
      if (TerrainData.isBroken) {
        ctx.LogImportWarning($"{nameof(TerrainData)} is reference broken");
        return;
      }

      var (terrainGuid, _) = AssetDatabaseUtils.GetGUIDAndLocalFileIdentifierOrThrow(TerrainData);
      
      var asset = ScriptableObject.CreateInstance<Quantum.TerrainCollider>();
      asset.name = Path.GetFileNameWithoutExtension(ctx.assetPath);

      var importedGuid = AssetDatabase.GUIDFromAssetPath(ctx.assetPath);
      var fileId = AssetDatabaseUtils.GetLocalFileIdentifier(asset, "main");
      asset.Guid = QuantumUnityDBUtilities.GetExpectedAssetGuid(importedGuid, fileId, out _);
      asset.Path = QuantumUnityDBUtilities.GetExpectedAssetPath(ctx.assetPath, asset.name, null);

      ctx.AddObjectToAsset("main", asset);
      ctx.DependsOnSourceAsset(terrainGuid);
      Import(asset, TerrainData.asset, BakeResolutionDivisor);
    }

    internal static void Import(Quantum.TerrainCollider asset, TerrainData terrainData, TerrainBakeResolutionDivisor bakeResDivisor) {
      QuantumStaticTerrainCollider3D.Bake(asset, terrainData, bakeResDivisor);
    }
  }
#endif
}