namespace Quantum {
  using Photon.Deterministic;
  using UnityEngine;

  /// <summary>
  /// The script will create a static 2D edge collider during Quantum map baking.
  /// </summary>
  public class QuantumStaticEdgeCollider2D : QuantumStaticCollider2DSource {
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
    /// <summary>
    /// Link a Unity edge collider to copy its size and position of during Quantum map baking.
    /// </summary>
    public EdgeCollider2D SourceCollider;
    /// <summary>
    /// Vertex A of the edge.
    /// </summary>
    [InlineHelp, DrawIf("SourceCollider", 0)]
    public FPVector2 VertexA = new FPVector2(2, 2);
    /// <summary>
    /// Vertex B of the edge.
    /// </summary>
    [InlineHelp, DrawIf("SourceCollider", 0)]
    public FPVector2 VertexB = new FPVector2(-2, -2);
    /// <summary>
    /// The position offset added to the <see cref="Transform.position"/> during baking.
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

      Settings.Trigger = SourceCollider.isTrigger;
      PositionOffset = SourceCollider.offset.ToFPVector2();

      VertexA = SourceCollider.points[0].ToFPVector2();
      VertexB = SourceCollider.points[1].ToFPVector2();
    }

    /// <summary>
    /// Edge transformation to bake.
    /// </summary>
    public static void GetEdgeGizmosSettings(Transform t, FPVector2 posOffset, FP rotOffset, FPVector2 localStart, FPVector2 localEnd, FP localHeight, out Vector3 start, out Vector3 end, out float height) {
      var scale = t.lossyScale;
      var trs = Matrix4x4.TRS(t.TransformPoint(posOffset.ToUnityVector3()), t.rotation * rotOffset.FlipRotation().ToUnityQuaternionDegrees(), scale);

      start = trs.MultiplyPoint(localStart.ToUnityVector3());
      end = trs.MultiplyPoint(localEnd.ToUnityVector3());

#if QUANTUM_XY
      height = localHeight.AsFloat * Mathf.Abs(scale.z);
#else
      height = localHeight.AsFloat * Mathf.Abs(scale.y);
#endif
    }
    
    public static MapStaticCollider2D BakeStaticEdge2D(Transform t, FPVector2 positionOffset, FP rotationOffset, FPVector2 vertexA, FPVector2 vertexB, FP height, QuantumStaticColliderSettings settings, QuantumStaticCollider2DBakeContext context) {
      GetEdgeGizmosSettings(t, positionOffset, rotationOffset, vertexA, vertexB, height, out var start, out var end, out var scaledHeight);

      var startToEnd = end - start;
      var pos = (start + end) / 2.0f;
      var rot = Quaternion.FromToRotation(Vector3.right, startToEnd);

      return new MapStaticCollider2D {
        Position = pos.ToFPVector2(),
        Rotation = rot.ToFPRotation2D(),
#if QUANTUM_XY
        VerticalOffset = -t.position.z.ToFP(),
        Height         = scaledHeight.ToFP(),
#else
        VerticalOffset = t.position.y.ToFP(),
        Height         = scaledHeight.ToFP(),
#endif
        PhysicsMaterial = settings.PhysicsMaterial,
        StaticData      = context.MakeStaticData(t.gameObject, settings),
        Layer           = t.gameObject.layer,
        ShapeType  = Shape2DType.Edge,
        EdgeExtent = (startToEnd.magnitude / 2.0f).ToFP(),
      };
    }

    public override void GetColliders(QuantumStaticCollider2DBakeContext context) {
      UpdateFromSourceCollider();
      context.Add(BakeStaticEdge2D(transform, PositionOffset, RotationOffset, VertexA, VertexB, Height, Settings, context));
    }
#else
    public override void GetColliders(QuantumStaticCollider2DBakeContext context) {
    }
#endif
  }
}