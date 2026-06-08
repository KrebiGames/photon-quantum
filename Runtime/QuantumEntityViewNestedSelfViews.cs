namespace Quantum {
  using UnityEngine;

  public class QuantumEntityViewNestedSelfViews : QuantumMonoBehaviour {
    public QuantumEntityView[] NestedSelfViews;
    
    void OnValidate() {
      // for prefab instances, don't show and don't serialize
      if (Application.isEditor && gameObject.scene.IsValid()) {
        hideFlags |= HideFlags.HideAndDontSave | HideFlags.HideInInspector;
      }
    }
  }
}