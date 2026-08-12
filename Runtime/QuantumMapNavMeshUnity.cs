namespace Quantum {
  using System.Collections.Generic;
  using System.Linq;
  using UnityEngine;
  using UnityEngine.SceneManagement;

  public class QuantumMapNavMeshUnity : QuantumMonoBehaviour, IQuantumNavMeshSource {
    public GameObject[] NavMeshSurfaces;
    
    [DrawInline]
    public QuantumNavMesh.ImportSettings Settings;

    public void GetNavMeshes(QuantumNavMeshBakeContext context) {
#if QUANTUM_ENABLE_AI_NAVIGATION && !QUANTUM_DISABLE_AI_NAVIGATION
      if (context.ImportFromUnity == false) {
        return;
      }

      if (!GetComponentInParent<QuantumMapData>()) {
        // originally this only works for parented by the map data
        return;
      }
      var bakeData = CreateBakeData();
      if (bakeData == null) {
        Log.Error($"Could not import navmesh '{name}'");
        return;
      }

      context.Add(bakeData);
#endif
    }

#if QUANTUM_ENABLE_AI_NAVIGATION && !QUANTUM_DISABLE_AI_NAVIGATION
    public NavMeshBakeData CreateBakeData() {
      // if NavMeshSurface is installed, non-linked surfaces are deactivated so CalculateTriangulation
      // runs only against the selected Unity navmesh; the finally block restores them
      var deactivated  = new List<GameObject>();
      var existingData = new List<UnityEngine.Object>();
      var scene = gameObject.scene;
      
      try {
        if (NavMeshSurfaces?.Length > 0) {
          foreach (var surface in scene.GetComponentsInHierarchyOrder<Unity.AI.Navigation.NavMeshSurface>()) {
            if (NavMeshSurfaces.Contains(surface.gameObject) == false) {
              surface.gameObject.SetActive(false);
              deactivated.Add(surface.gameObject);
            } else {
              if (surface.navMeshData == null) {
                // TODO: why wasn't this here in the first place?
                surface.BuildNavMesh();
              }
              if (surface.navMeshData != null) {
                existingData.Add(surface.navMeshData);
              }
            }
          }
        }
        
        var data = QuantumNavMesh.ImportFromUnity(scene, Settings, name, existingData);
        if (data == null) {
          return null;
        }

        data.Name                            = name;
        data.AgentRadius                     = QuantumNavMesh.FindSmallestAgentRadius(NavMeshSurfaces);
        data.EnableQuantum_XY                = Settings.EnableQuantum_XY;
        data.ClosestTriangleCalculation      = Settings.ClosestTriangleCalculation;
        data.ClosestTriangleCalculationDepth = Settings.ClosestTriangleCalculationDepth;
        return data;
      } finally {
        foreach (var go in deactivated) {
          go.SetActive(true);
        }
      }
    }
#endif
  }
}
