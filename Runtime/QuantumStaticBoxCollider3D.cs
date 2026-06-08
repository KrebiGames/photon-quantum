namespace Quantum {
  using Photon.Deterministic;
  using UnityEngine;

  /// <summary>
  /// The script will create a static 3D Quantum box collider during map baking.
  /// </summary>
  public class QuantumStaticBoxCollider3D : QuantumStaticCollider3DSource {
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
    /// <summary>
    /// The Unity box collider to copy the size and position of during Quantum map baking.
    /// </summary>
    [InlineHelp]
    public BoxCollider SourceCollider;
    /// <summary>
    /// The size of the collider.
    /// </summary>
    [InlineHelp, DrawIf("SourceCollider", 0)]
    public FPVector3 Size;
    /// <summary>
    /// The position offset added to the <see cref="Transform.position"/> during baking.
    /// </summary>
    [InlineHelp, DrawIf("SourceCollider", 0)]
    public FPVector3 PositionOffset;
    /// <summary>
    /// The rotation offset added to the <see cref="Transform.rotation"/> during baking.
    /// </summary>
    [InlineHelp]
    public FPVector3 RotationOffset;
    /// <summary>
    /// Additional static collider settings.
    /// </summary>
    [InlineHelp, DrawInline, Space]
    public QuantumStaticColliderSettings Settings = new QuantumStaticColliderSettings();

    private void ClampSize() {
      Size.X = FPMath.Clamp(Size.X, 0, Size.X);
      Size.Y = FPMath.Clamp(Size.Y, 0, Size.Y);
      Size.Z = FPMath.Clamp(Size.Z, 0, Size.Z);
    }

    private void OnValidate() {
      UpdateFromSourceCollider();
    }

    /// <summary>
    /// Copy collider configuration from source collider if exist. 
    /// </summary>
    public void UpdateFromSourceCollider() {
      if (SourceCollider == null) {
        ClampSize();
        return;
      }

      Size = SourceCollider.size.ToFPVector3();
      Size.X = FPMath.Abs(Size.X);
      Size.Y = FPMath.Abs(Size.Y);
      Size.Z = FPMath.Abs(Size.Z);
      PositionOffset = SourceCollider.center.ToFPVector3();
      Settings.Trigger = SourceCollider.isTrigger;
    }

    /// <summary>
    /// Calculates and outputs the shape settings converted to FP format.
    /// </summary>
    /// <param name="position">World-space position of the shape.</param>
    /// <param name="rotation">World-space rotation of the shape.</param>
    /// <param name="extents">Box extents.</param>
    public void GetShapeSettings(out FPVector3 position, out FPQuaternion rotation, out FPVector3 extents) {
      UpdateFromSourceCollider();

      var absScale = FPVector3.Abs(transform.lossyScale.ToFPVector3());
      var toExtents = absScale * FP._0_50;

      extents = new FPVector3(
        x: Size.X * toExtents.X,
        y: Size.Y * toExtents.Y,
        z: Size.Z * toExtents.Z
      );

      FPVector3 scaledPosOffset;
      scaledPosOffset.X = PositionOffset.X * absScale.X;
      scaledPosOffset.Y = PositionOffset.Y * absScale.Y;
      scaledPosOffset.Z = PositionOffset.Z * absScale.Z;

      var fpTransform = Transform3D.Create(transform.position.ToFPVector3(), transform.rotation.ToFPQuaternion());
      position = fpTransform.TransformPoint(scaledPosOffset);
      rotation = fpTransform.Rotation * FPQuaternion.Euler(RotationOffset);
    }

    /// <inheritdoc cref="QuantumStaticCollider3DSource.GetColliders"/>
    public override void GetColliders(QuantumStaticCollider3DBakeContext context) {
      GetShapeSettings(out var pos, out var rot, out var extents);

      context.Add(new MapStaticCollider3D {
        Position = pos,
        Rotation = rot,
        PhysicsMaterial = Settings.PhysicsMaterial,
        StaticData = context.MakeStaticData(gameObject, Settings),
        ShapeType = Shape3DType.Box,
        BoxExtents = extents
      });
    }
#else 
    public override void GetColliders(QuantumStaticCollider3DBakeContext context) { 
    }
#endif
  }
}