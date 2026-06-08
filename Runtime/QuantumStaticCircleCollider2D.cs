namespace Quantum {
  using Photon.Deterministic;
  using UnityEngine;

  /// <summary>
  /// The script will create a static 2D circle collider during Quantum map baking.
  /// </summary>
  public class QuantumStaticCircleCollider2D : QuantumStaticCollider2DSource {
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
    /// <summary>
    /// Link a Unity circle collider to copy its size and position of during Quantum map baking.
    /// </summary>
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
    [InlineHelp, MultiTypeReference(typeof(CircleCollider2D), typeof(SphereCollider))]
#else
    [InlineHelp, SerializeReferenceTypePicker(typeof(CircleCollider2D))]
#endif
    public Component SourceCollider;
    /// <summary>
    /// The radius of the circle.
    /// </summary>
    [InlineHelp, DrawIf("SourceCollider", 0)]
    public FP Radius;
    /// <summary>
    /// The position offset added to the <see cref="Transform.position"/> during baking.
    /// </summary>
    [InlineHelp, DrawIf("SourceCollider", 0)]
    public FPVector2 PositionOffset;
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
      UpdateFromSourceCollider();
    }

    /// <summary>
    /// Copy collider configuration from source collider if exist. 
    /// </summary>
    public void UpdateFromSourceCollider() {
      Radius = FPMath.Clamp(Radius, 0, Radius);
      Height = FPMath.Clamp(Height, 0, Height);
      if (SourceCollider == null) {
        return;
      }

      switch (SourceCollider) {
#if QUANTUM_ENABLE_PHYSICS3D && !QUANTUM_DISABLE_PHYSICS3D
        case SphereCollider sphere:
          Radius = sphere.radius.ToFP();
          PositionOffset = sphere.center.ToFPVector2();
          Settings.Trigger = sphere.isTrigger;
          break;
#endif

        case CircleCollider2D circle:
          Radius = circle.radius.ToFP();
          PositionOffset = circle.offset.ToFPVector2();
          Settings.Trigger = circle.isTrigger;
          break;

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
    /// <param name="radius">Circle radius.</param>
    /// <param name="verticalOffset">Offset in the axis orthogonal to the 2D plane (Z if QUANTUM_XY is defined, Y otherwise).</param>
    /// <param name="height">Height of in the axis orthogonal to the 2D plane (Z if QUANTUM_XY is defined, Y otherwise).</param>
    public void GetShapeSettings(out FPVector2 position, out FP rotation, out FP radius, out FP verticalOffset, out FP height) {
      UpdateFromSourceCollider();

      var absScale2D = FPVector2.Abs(transform.lossyScale.ToFPVector2());
      radius = Radius * FPMath.Max(absScale2D.X, absScale2D.Y);

      FPVector2 scaledPosOffset;
      scaledPosOffset.X = PositionOffset.X * absScale2D.X;
      scaledPosOffset.Y = PositionOffset.Y * absScale2D.Y;

      var fpTransform = Transform2D.Create(transform.position.ToFPVector2(), transform.rotation.ToFPRotation2D());
      position = fpTransform.TransformPoint(scaledPosOffset);
      rotation = fpTransform.Rotation;
      
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

      GetShapeSettings(out var pos, out var rot, out var radius, out var verticalOffset, out var height);

      context.Add(new MapStaticCollider2D {
        Position = pos,
        Rotation = rot,
        VerticalOffset = verticalOffset,
        Height = height,
        PhysicsMaterial = Settings.PhysicsMaterial,
        StaticData = context.MakeStaticData(gameObject, Settings),
        Layer = gameObject.layer,
        ShapeType = Shape2DType.Circle,
        CircleRadius = radius,
      });
    }
#else
    public override void GetColliders(QuantumStaticCollider2DBakeContext context) {
    }
#endif
  }
}