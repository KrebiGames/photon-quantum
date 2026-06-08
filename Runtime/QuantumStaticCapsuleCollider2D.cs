namespace Quantum {
  using Photon.Deterministic;
  using UnityEngine;

  /// <summary>
  /// The script will create a static 2D capsule collider during Quantum map baking.
  /// </summary>
  public class QuantumStaticCapsuleCollider2D : QuantumStaticCollider2DSource {
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
    /// <summary>
    /// Link a Unity capsule collider to copy its size and position of during Quantum map baking.
    /// </summary>
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
    [InlineHelp, MultiTypeReference(typeof(CapsuleCollider2D), typeof(CapsuleCollider))]
#else
    [InlineHelp, SerializeReferenceTypePicker(typeof(CapsuleCollider2D))]
#endif
    public Component SourceCollider;
    /// <summary>
    /// Define the capsule size if not source collider exists. The x-axis is the diameter and the y-axis is the height.
    /// </summary>
    [InlineHelp, DrawIf("SourceCollider", 0)]
    public FPVector2 Size;
    /// <summary>
    /// The world axis that the capsule will be aligned.
    /// </summary>
    [InlineHelp, DrawIf("SourceCollider", 0)]
    public UnityEngine.CapsuleDirection2D Direction = UnityEngine.CapsuleDirection2D.Vertical;
    /// <summary>
    /// Additional static collider settings.
    /// </summary>
    [InlineHelp, DrawIf("SourceCollider", 0)]
    public FPVector2 PositionOffset;
    /// <summary>
    /// The rotation offset added to the <see cref="Transform.rotation"/> during baking.
    /// </summary>
    [InlineHelp]
    public FP RotationOffset;
    /// <summary>
    /// The optional 2D pseudo height of the collider.
    /// </summary>
    [InlineHelp]
    public FP Height;
    /// <summary>
    /// Additional static collider settings.
    /// </summary>
    [InlineHelp, DrawInline, Space]
    public QuantumStaticColliderSettings Settings = new QuantumStaticColliderSettings();

    private void OnValidate() {
      Size.X = FPMath.Clamp(Size.X, 0, Size.X);
      Size.Y = FPMath.Clamp(Size.Y, 0, Size.Y);
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

      switch (SourceCollider) {
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
        case CapsuleCollider capsule:
          switch (capsule.direction) {
            case 0: // X-Axs
              Direction = UnityEngine.CapsuleDirection2D.Horizontal;
              break;
            case 1: // Y-Axs
              Direction = UnityEngine.CapsuleDirection2D.Vertical;
              break;
            case 2: // Z-Axs
#if QUANTUM_XY
              Direction = UnityEngine.CapsuleDirection2D.Horizontal;
#else
              Direction = UnityEngine.CapsuleDirection2D.Vertical;
#endif
              break;
          }

          var capsuleRadius = capsule.radius.ToFP();
          var capsuleHeight = capsule.height.ToFP();

          Size = Direction == UnityEngine.CapsuleDirection2D.Horizontal
            ? new FPVector2(capsuleHeight, capsuleRadius * 2)
            : new FPVector2(capsuleRadius * 2, capsuleHeight);

          PositionOffset = capsule.center.ToFPVector2();
          Settings.Trigger = capsule.isTrigger;
          
          break;
#endif
            case CapsuleCollider2D capsule: {
            Size = capsule.size.ToFPVector2();
            PositionOffset = capsule.offset.ToFPVector2();
            Settings.Trigger = capsule.isTrigger;
            Direction = capsule.direction;
            break;
          }

        default:
          SourceCollider = null;
          break;
      }
    }

    /// <summary>
    /// Calculates and outputs the shape settings converted to FP format.
    /// </summary>
    /// <param name="position">World-space position of the shape.</param>
    /// <param name="rotation">World-space rotation of the shape.</param>
    /// <param name="size">Capsule size.</param>
    /// <param name="verticalOffset">Offset in the axis orthogonal to the 2D plane (Z if QUANTUM_XY is defined, Y otherwise).</param>
    /// <param name="height">Height of in the axis orthogonal to the 2D plane (Z if QUANTUM_XY is defined, Y otherwise).</param>
    public void GetShapeSettings(out FPVector2 position, out FP rotation, out FPVector2 size, out FP verticalOffset, out FP height) {
      UpdateFromSourceCollider();

      var absScale2D = FPVector2.Abs(transform.lossyScale.ToFPVector2());

      FP directionRotation;
      if (Direction == UnityEngine.CapsuleDirection2D.Horizontal) {
        directionRotation = FP.Rad_90;
        size = new FPVector2(Size.Y * absScale2D.Y, Size.X * absScale2D.X);
      } else {
        directionRotation = FP._0;
        size = new FPVector2(Size.X * absScale2D.X, Size.Y * absScale2D.Y);
      }

      FPVector2 scaledPosOffset;
      scaledPosOffset.X = PositionOffset.X * absScale2D.X;
      scaledPosOffset.Y = PositionOffset.Y * absScale2D.Y;

      var fpTransform = Transform2D.Create(transform.position.ToFPVector2(), transform.rotation.ToFPRotation2D());
      position = fpTransform.TransformPoint(scaledPosOffset);
      rotation = fpTransform.Rotation + directionRotation + RotationOffset.FlipRotation() * FP.Deg2Rad;
      
#if QUANTUM_XY
      verticalOffset = -transform.position.z.ToFP();
      height = Height * FPMath.Abs(transform.lossyScale.z.ToFP());
#else
      verticalOffset = transform.position.y.ToFP();
      height = Height * FPMath.Abs(transform.lossyScale.y.ToFP());
#endif
    }

    /// <inheritdoc cref="QuantumStaticCollider2DSource.GetColliders"/>
    public override void GetColliders(QuantumStaticCollider2DBakeContext context) {
      UpdateFromSourceCollider();

      GetShapeSettings(out var pos, out var rot, out var size, out var verticalOffset, out var height);

      context.Add(new MapStaticCollider2D {
        Position = pos,
        Rotation = rot,
        VerticalOffset = verticalOffset,
        Height = height,
        PhysicsMaterial = Settings.PhysicsMaterial,
        StaticData = context.MakeStaticData(gameObject, Settings),
        Layer = gameObject.layer,
        ShapeType = Shape2DType.Capsule,
        CapsuleSize = size
      });
    }
#else
    public override void GetColliders(QuantumStaticCollider2DBakeContext context) {
    }
#endif
  }
}