namespace Quantum {
  using Photon.Deterministic;
  using UnityEngine;

  /// <summary>
  /// The script will create a static sphere collider during Quantum map baking.
  /// </summary>
  public class QuantumStaticSphereCollider3D : QuantumStaticCollider3DSource {
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
    /// <summary>
    /// Link a Unity sphere collider to copy its size and position of during Quantum map baking.
    /// </summary>
    [InlineHelp] 
    public SphereCollider SourceCollider;
    /// <summary>
    /// The radius of the sphere.
    /// </summary>
    [InlineHelp, DrawIf("SourceCollider", 0)]
    public FP Radius;
    /// <summary>
    /// The position offset added to the <see cref="Transform.position"/> during baking.
    /// </summary>
    [InlineHelp, DrawIf("SourceCollider", 0)]
    public FPVector3 PositionOffset;

    private void OnValidate() {
      Radius = FPMath.Clamp(Radius, 0, Radius);
      UpdateFromSourceCollider();
    }

    /// <summary>
    /// Copy collider configuration from source collider if exist. 
    /// </summary>
    public void UpdateFromSourceCollider() {
      if (SourceCollider == null) {
        return;
      }

      Radius = SourceCollider.radius.ToFP();
      PositionOffset = SourceCollider.center.ToFPVector3();
      Settings.Trigger = SourceCollider.isTrigger;
    }

    /// <summary>
    /// Calculates and outputs the shape settings converted to FP format.
    /// </summary>
    /// <param name="position">World-space position of the shape.</param>
    /// <param name="rotation">World-space rotation of the shape.</param>
    /// <param name="radius">Sphere radius.</param>
    public void GetShapeSettings(out FPVector3 position, out FPQuaternion rotation, out FP radius) {
      UpdateFromSourceCollider();
      
      var lossyScale = transform.lossyScale.ToFPVector3();
      var absScale = FPVector3.Abs(lossyScale);
      var radiusScale = FPMath.Max(absScale.X, absScale.Y, absScale.Z);
      radius = Radius * radiusScale;

      FPVector3 scaledPosOffset;
      scaledPosOffset.X = PositionOffset.X * lossyScale.X;
      scaledPosOffset.Y = PositionOffset.Y * lossyScale.Y;
      scaledPosOffset.Z = PositionOffset.Z * lossyScale.Z;

      var fpTransform = Transform3D.Create(transform.position.ToFPVector3(), transform.rotation.ToFPQuaternion());
      position = fpTransform.TransformPoint(scaledPosOffset);
      rotation = fpTransform.Rotation;
    }

    /// <inheritdoc cref="QuantumStaticCollider3DSource.GetColliders"/>
    public override void GetColliders(QuantumStaticCollider3DBakeContext context) {
      GetShapeSettings(out var pos, out var rot, out var radius);

      context.Add(new MapStaticCollider3D {
        Position = pos,
        Rotation = rot,
        PhysicsMaterial = Settings.PhysicsMaterial,
        ShapeType = Shape3DType.Sphere,
        StaticData = context.MakeStaticData(gameObject, Settings),
        SphereRadius = radius,
#if QUANTUM_ENABLE_ADDON_NAVIGATION
        QNavMeshData = QNavMeshData,
#endif
      });
    }
#else 
    public override void GetColliders(QuantumStaticCollider3DBakeContext context) {
    }
#endif
  }
}