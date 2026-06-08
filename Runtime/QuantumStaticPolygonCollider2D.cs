namespace Quantum {
  using System.Linq;
  using Photon.Deterministic;
  using UnityEngine;

  /// <summary>
  /// The script will create a static 2D polygon collider during Quantum map baking.
  /// </summary>
  public class QuantumStaticPolygonCollider2D : QuantumStaticCollider2DSource {
#if QUANTUM_ENABLE_PHYSICS2D && !QUANTUM_DISABLE_PHYSICS2D
    /// <summary>
    /// Link a Unity polygon collider to copy its size and position of during Quantum map baking.
    /// </summary>
    [InlineHelp] 
    public PolygonCollider2D SourceCollider;
    /// <summary>
    /// Bake the static collider as 2D edges instead.
    /// </summary>
    [InlineHelp] 
    public bool BakeAsStaticEdges2D = false;
    /// <summary>
    /// The individual vertices of the polygon.
    /// </summary>
    [InlineHelp, DrawIf("SourceCollider", 0)]
    public FPVector2[] Vertices = new FPVector2[3] { new FPVector2(0, 2), new FPVector2(-1, 0), new FPVector2(+1, 0) };
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
    /// <summary>
    /// Should the <see cref="Vertices"/> be set from the source collider during baking.
    /// </summary>
    protected virtual bool UpdateVerticesFromSourceOnBake => true;

    private void OnValidate() {
      Height = FPMath.Clamp(Height, 0, Height);
      UpdateFromSourceCollider();
    }

    /// <summary>
    /// Copy collider configuration from source collider if exist. 
    /// </summary>
    public void UpdateFromSourceCollider(bool updateVertices = true) {
      if (SourceCollider == null) {
        return;
      }

      Settings.Trigger = SourceCollider.isTrigger;
      PositionOffset = SourceCollider.offset.ToFPVector2();

      if (updateVertices == false) {
        return;
      }

      Vertices = new FPVector2[SourceCollider.points.Length];

      for (var i = 0; i < SourceCollider.points.Length; i++) {
        Vertices[i] = SourceCollider.points[i].ToFPVector2();
      }
    }

    public override void GetColliders(QuantumStaticCollider2DBakeContext context) {
      UpdateFromSourceCollider(UpdateVerticesFromSourceOnBake);

      if (BakeAsStaticEdges2D) {
        for (var i = 0; i < Vertices.Length; i++) {
          context.Add(QuantumStaticEdgeCollider2D.BakeStaticEdge2D(transform, PositionOffset, RotationOffset, Vertices[i], Vertices[(i + 1) % Vertices.Length], Height, Settings, context));
        }
        return;
      }

      var s = transform.lossyScale;
      var vertices = Vertices.Select(x => {
        var v = x.ToUnityVector3();
        return new Vector3(v.x * s.x, v.y * s.y, v.z * s.z);
      }).Select(x => x.ToFPVector2()).ToArray();
      if (FPVector2.IsClockWise(vertices)) {
        FPVector2.MakeCounterClockWise(vertices);
      }

      var normals = FPVector2.CalculatePolygonNormals(vertices);
      var rotation = transform.rotation.ToFPRotation2D() + RotationOffset.FlipRotation() * FP.Deg2Rad;
      var positionOffset = FPVector2.Rotate(FPVector2.CalculatePolygonCentroid(vertices), rotation);

      context.Add(new MapStaticCollider2D {
        Position = transform.TransformPoint(PositionOffset.ToUnityVector3()).ToFPVector2() + positionOffset,
        Rotation = rotation,
#if QUANTUM_XY
        VerticalOffset = -transform.position.z.ToFP(),
        Height = Height * Mathf.Abs(s.z).ToFP(),
#else
        VerticalOffset = transform.position.y.ToFP(),
        Height = Height * Mathf.Abs(s.y).ToFP(),
#endif
        PhysicsMaterial = Settings.PhysicsMaterial,
        StaticData = context.MakeStaticData(gameObject, Settings),
        Layer = gameObject.layer,
        ShapeType = Shape2DType.Polygon,
        PolygonCollider = new MapStaticCollider2DPolygonData() { Vertices = FPVector2.RecenterPolygon(vertices), Normals = normals, },
      });
    }
#else 
    public override void GetColliders(QuantumStaticCollider2DBakeContext context) {
    }
#endif
  }
}