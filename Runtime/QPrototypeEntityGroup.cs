namespace Quantum {
  [UnityEngine.DisallowMultipleComponent]
  public partial class QPrototypeEntityGroup : Quantum.QuantumUnityComponentPrototype<Quantum.Prototypes.EntityGroupPrototype>,
    IQuantumUnityPrototypeWrapperForComponent<Quantum.EntityGroup> {

    [DrawInline, ReadOnly(InEditMode = false)]
    public Quantum.Prototypes.Unity.EntityGroupPrototype Prototype = new();

    public override System.Type ComponentType => typeof(Quantum.EntityGroup);

    public override Quantum.ComponentPrototype CreatePrototype(Quantum.QuantumEntityPrototypeConverter converter) => base.ConvertPrototype(converter, Prototype);
  }
}