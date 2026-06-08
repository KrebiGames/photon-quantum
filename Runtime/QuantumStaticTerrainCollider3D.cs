namespace Quantum {
  using System;
  using Photon.Deterministic;
  using UnityEditor;
  using UnityEngine;

  /// <summary>
  /// Factor by which to reduce the source Unity terrain heightmap resolution when baking the Quantum terrain collider.
  /// </summary>
  public enum TerrainBakeResolutionDivisor {
    /// <summary>
    /// Use the source Unity terrain heightmap resolution (no downsampling).
    /// </summary>
    MatchSource = 1,

    // U+2215 Division Slash (∕), not U+002F (/), so Unity's popup does not interpret these labels as submenu paths.
    [InspectorName("1∕2")]  Half         = 2,
    [InspectorName("1∕4")]  Quarter      = 4,
    [InspectorName("1∕8")]  Eighth       = 8,
    [InspectorName("1∕16")] Sixteenth    = 16,
    [InspectorName("1∕32")] ThirtySecond = 32,
  }

  /// <summary>
  /// The script will create a static 3D terrain collider during Quantum map baking.
  /// </summary>
  [ExecuteInEditMode]
  public class QuantumStaticTerrainCollider3D : QuantumStaticCollider3DSource {
    /// <summary>
    /// The Quantum terrain collider asset.
    /// </summary>
    [InlineHelp]
    public TerrainCollider Asset;
    
    /// <inheritdoc cref="TerrainBakeResolutionDivisor"/>
    [InlineHelp]
    public Quantum.TerrainBakeResolutionDivisor BakeResolutionDivisor = TerrainBakeResolutionDivisor.MatchSource;

    /// <summary>
    /// Additional static collider settings.
    /// </summary>
    [InlineHelp, DrawInline, Space]
    public QuantumStaticColliderSettings Settings = new QuantumStaticColliderSettings();

    /// <summary>
    /// The physics solver will resolve sphere and capsule shapes against terrain collisions as if it was a regular flat and smooth plane.
    /// </summary>
    public Boolean SmoothSphereMeshCollisions = false;
    
    
#pragma warning disable 618 // use of obsolete
    [Obsolete("Use 'Settings.MutableMode' instead.")]
    public PhysicsCommon.StaticColliderMutableMode MutableMode => Settings.MutableMode;
#pragma warning restore 618

    public void Bake() {
#if QUANTUM_ENABLE_TERRAIN && !QUANTUM_DISABLE_TERRAIN
      if (!Asset) {
        return;
      }
      
#if UNITY_EDITOR
      if (!UnityEditor.AssetDatabase.IsNativeAsset(Asset)) {
        // no need to import, everything is dependency-driven
        return;
      }
#endif
      
      FPMathUtils.LoadLookupTables();
      var t = GetComponent<Terrain>();
      Bake(Asset, t.terrainData, BakeResolutionDivisor);
#if UNITY_EDITOR
      UnityEditor.EditorUtility.SetDirty(Asset);
      UnityEditor.EditorUtility.SetDirty(this);
#endif
#endif
    }

#if QUANTUM_ENABLE_TERRAIN && !QUANTUM_DISABLE_TERRAIN
    internal static void Bake(Quantum.TerrainCollider asset, UnityEngine.TerrainData terrainData, TerrainBakeResolutionDivisor bakeResDivisor = TerrainBakeResolutionDivisor.MatchSource) {
      var sourceRes = terrainData.heightmapResolution;
      var newRes = GetResolution(sourceRes, bakeResDivisor, out var resDivisor);
      asset.Resolution = newRes;

      // Unity heightmapScale: x/z are per-vertex spacing in world units, y is max terrain height (not spacing).
      var srcScale = terrainData.heightmapScale;
      var newScale = new Vector3(
        srcScale.x * resDivisor,
        srcScale.y,
        srcScale.z * resDivisor
      ).ToFPVector3();
      asset.Scale = newScale;

      asset.HeightMap = new FP[newRes * newRes];

      // Box-filter average over a (divisor + 1) x (divisor + 1) window centered on the source vertex
      var halfWindow = resDivisor / 2;
      for (var i = 0; i < newRes; i++) {
        for (var j = 0; j < newRes; j++) {
          var srcI = i * resDivisor;
          var srcJ = j * resDivisor;
          var iMin = Math.Max(0, srcI - halfWindow);
          var iMax = Math.Min(sourceRes - 1, srcI + halfWindow);
          var jMin = Math.Max(0, srcJ - halfWindow);
          var jMax = Math.Min(sourceRes - 1, srcJ + halfWindow);

          var sum = default(FP);
          var count = 0;
          for (var a = iMin; a <= iMax; a++) {
            for (var b = jMin; b <= jMax; b++) {
              sum += FP.FromFloat_UNSAFE(terrainData.GetHeight(a, b));
              count++;
            }
          }
          asset.HeightMap[j + i * newRes] = sum / count;
        }
      }

      // support to Terrain Paint Holes: https://docs.unity3d.com/2019.4/Documentation/Manual/terrain-PaintHoles.html
      // Conservative OR: an output cell is a hole if any source cell in its divisor x divisor block is a hole.
      asset.HoleMask = new ulong[(newRes * newRes - 1) / 64 + 1];
      for (var i = 0; i < newRes - 1; i++) {
        for (var j = 0; j < newRes - 1; j++) {
          var hasHole = false;
          var aMax = Math.Min(sourceRes - 1, (i + 1) * resDivisor);
          var bMax = Math.Min(sourceRes - 1, (j + 1) * resDivisor);
          for (var a = i * resDivisor; a < aMax && !hasHole; a++) {
            for (var b = j * resDivisor; b < bMax; b++) {
              if (terrainData.IsHole(a, b)) {
                hasHole = true;
                break;
              }
            }
          }
          if (hasHole) {
            asset.SetHole(i, j);
          }
        }
      }
    }

    private static int GetResolution(int sourceRes, TerrainBakeResolutionDivisor resolutionDivisor, out int resDivisor) {
      resDivisor = (int)resolutionDivisor;

      if (resDivisor <= 1) {
        resDivisor = 1;
        return sourceRes;
      }

      if (resDivisor > sourceRes - 1) {
        Log.DebugWarn($"Resolution divisor {resDivisor} is too high for source resolution {sourceRes}. Falling back to source resolution.");
        resDivisor = 1;
        return sourceRes;
      }

      if ((sourceRes - 1) % resDivisor != 0) {
        // Defensive: stock Unity terrains are always 2^n + 1 and the enum exposes only power-of-2 divisors, so this
        // should not trigger in practice. If it does, the downsampled grid would not cleanly cover the source extent.
        Log.DebugWarn($"Source resolution {sourceRes} is not cleanly divisible by divisor {resDivisor}. Falling back to source resolution.");
        resDivisor = 1;
        return sourceRes;
      }

      return ((sourceRes - 1) / resDivisor) + 1;
    }
#endif

    public override void GetColliders(QuantumStaticCollider3DBakeContext context) {
#if QUANTUM_ENABLE_TERRAIN && !QUANTUM_DISABLE_TERRAIN
      if (!Asset) {
        return;
      }
      
      var staticColliderIndex = context.StaticColliderCount;
      
      context.Add(new MapStaticCollider3D {
        Position                   = default(FPVector3),
        Rotation                   = FPQuaternion.Identity,
        PhysicsMaterial            = Settings.PhysicsMaterial,
        SmoothSphereMeshCollisions = SmoothSphereMeshCollisions,
        ShapeType = Shape3DType.Mesh,
        StaticData = context.MakeStaticData(gameObject, Settings),
      });
      
      context.Add(Asset.CreateMeshTriangles(transform.position.ToFPVector3(), staticColliderIndex));
#endif
    }
  }
}