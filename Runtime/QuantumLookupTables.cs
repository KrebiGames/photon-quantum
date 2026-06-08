namespace Quantum {
  using Photon.Deterministic;
  using UnityEngine;

  [QuantumGlobalScriptableObject(DefaultPath)]
  [CreateAssetMenu(menuName = "Quantum/Configurations/LookupTables", fileName = "QuantumLookupTables", order = EditorDefines.AssetMenuPriorityConfigurations + 32)]
  public class QuantumLookupTables : QuantumGlobalScriptableObject<QuantumLookupTables> {
    public const string DefaultPath = "Assets/QuantumUser/Resources/QuantumLookupTables.asset";
      
    public TextAsset TableSinCos;
    public TextAsset TableTan;
    public TextAsset TableAsin;
    public TextAsset TableAcos;
    public TextAsset TableAtan;
    public TextAsset TableSqrt;

    public void InitializeLookupTables() {
      FPLut.Init(
        sinCos: TableSinCos != null ? TableSinCos.bytes : null,
        tan: TableTan != null ? TableTan.bytes : null,
        asin: TableAsin != null ? TableAsin.bytes : null,
        acos: TableAcos != null ? TableAcos.bytes : null,
        atan: TableAtan != null ? TableAtan.bytes : null,
        sqrt: TableSqrt != null ? TableSqrt.bytes : null);
    }
  }
}