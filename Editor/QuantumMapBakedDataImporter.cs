#if QUANTUM_ENABLE_QMAP
namespace Quantum.Editor {
  using UnityEditor;
  using UnityEditor.AssetImporters;
  using UnityEngine;

  [ScriptedImporter(1, new[] { QuantumMapImporter.BakedMapExtension, QuantumMapImporter.BakedNavExtension }, QuantumMapImporter.ImportQueueOffset - 1)]
  class QuantumMapBakedDataImporter : ScriptedImporter {


    public override void OnImportAsset(AssetImportContext ctx) {
      // intermediate bake artifact; consumed by QuantumMapImporter, no Unity assets produced here
    }
  }
}
#endif