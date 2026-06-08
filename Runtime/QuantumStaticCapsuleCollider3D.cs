namespace Quantum {
  using System;
  using Photon.Deterministic;
  using UnityEngine;

  /// <summary>
  /// The script will create a static 3D capsule collider during Quantum map baking.
  /// </summary>
  public class QuantumStaticCapsuleCollider3D : QuantumStaticCollider3DSource {
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
    /// <summary>
    /// Link a Unity capsule collider to copy its size and position of during Quantum map baking.
    /// </summary>
    public CapsuleCollider SourceCollider;
    /// <summary>
    /// The radius of the capsule.
    /// </summary>
    [InlineHelp, DrawIf("SourceCollider", 0)] 
    public FP Radius;
    /// <summary>
    /// The height of the capsule.
    /// </summary>
    [InlineHelp, DrawIf("SourceCollider", 0)] 
    public FP Height;
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

    internal CapsuleDirection3D Direction = CapsuleDirection3D.Y;
    

    private void OnValidate() {
      Radius = FPMath.Clamp(Radius, 0, Radius);
      Height = FPMath.Clamp(Height, 0, Height);
      UpdateFromSourceCollider();
    }

    /// <summary>
    /// Copy collider configuration from source collider if exist. 
    /// </summary>
    public void UpdateFromSourceCollider() {
      if (SourceCollider == null) {
        return;
      }
      switch (SourceCollider.direction) {
        case 0:
          Direction = CapsuleDirection3D.X;
          break;
        case 1:
          Direction = CapsuleDirection3D.Y;
          break;
        case 2:
          Direction = CapsuleDirection3D.Z;
          break;
      }
      Radius = SourceCollider.radius.ToFP();
      Height = SourceCollider.height.ToFP();
      PositionOffset = SourceCollider.center.ToFPVector3();
      Settings.Trigger = SourceCollider.isTrigger;
    }

    /// <summary>
    /// Calculates and outputs the shape settings converted to FP format.
    /// </summary>
    /// <param name="position">World-space position of the shape.</param>
    /// <param name="rotation">World-space rotation of the shape.</param>
    /// <param name="capsuleRadius">Capsule radius.</param>
    /// <param name="capsuleHeight">Capsule Height.</param>
    public void GetShapeSettings(out FPVector3 position, out FPQuaternion rotation, out FP capsuleRadius, out FP capsuleHeight) {
      UpdateFromSourceCollider();

      var absScale = FPVector3.Abs(transform.lossyScale.ToFPVector3());

      FP radiusScale;
      FP heightScale;
      FPVector3 axisRotation;
      switch (Direction) {
        case CapsuleDirection3D.X:
          axisRotation = new FPVector3(0, 0, 90);
          heightScale = absScale.X;
          radiusScale = FPMath.Max(absScale.Y, absScale.Z);
          break;
        case CapsuleDirection3D.Y:
          axisRotation = new FPVector3(0, 0, 0);
          heightScale = absScale.Y;
          radiusScale = FPMath.Max(absScale.X, absScale.Z);
          break;
        case CapsuleDirection3D.Z:
          axisRotation = new FPVector3(90, 0, 0);
          heightScale = absScale.Z;
          radiusScale = FPMath.Max(absScale.X, absScale.Y);
          break;
        default:
          throw new ArgumentOutOfRangeException();
      }

      capsuleRadius = Radius * radiusScale;
      capsuleHeight = Height * heightScale;

      FPVector3 scaledPosOffset;
      scaledPosOffset.X = PositionOffset.X * absScale.X;
      scaledPosOffset.Y = PositionOffset.Y * absScale.Y;
      scaledPosOffset.Z = PositionOffset.Z * absScale.Z;

      var fpTransform = Transform3D.Create(transform.position.ToFPVector3(), transform.rotation.ToFPQuaternion());
      position = fpTransform.TransformPoint(scaledPosOffset);
      rotation = fpTransform.Rotation * FPQuaternion.Euler(RotationOffset + axisRotation);
    }

    /// <inheritdoc cref="QuantumStaticCollider3DSource.GetColliders"/>
    public override void GetColliders(QuantumStaticCollider3DBakeContext context) {
      UpdateFromSourceCollider();

      GetShapeSettings(out var pos, out var rot, out var radius, out var height);

      context.Add(new MapStaticCollider3D {
        Position = pos,
        Rotation = rot,
        PhysicsMaterial = Settings.PhysicsMaterial,
        StaticData = context.MakeStaticData(gameObject, Settings),
        ShapeType = Shape3DType.Capsule,
        CapsuleRadius = radius,
        CapsuleHeight = height
      });
    }
#else 
    public override void GetColliders(QuantumStaticCollider3DBakeContext context) {
    }
#endif
  }
}