namespace Quantum {

using UnityEngine;

  /// <summary>
  /// A custom entity view for the parts of the ragdoll. Since ragdoll limbs can have parents in Unity's hierarchy, they must be updated in the correct order.
  /// Otherwise, the updating of a parent transform position right after its child position will mess with the limb position.
  /// </summary>
  public class QuantumRagdollLimbView : QuantumEntityView {

    /// <summary>
    /// The depth of the body's limb or part based on its unity's transform hierarchy. 
    /// </summary>
    public int _depth;

    /// <summary>
    /// A reference to the Unity's ragdoll editor. It will update 
    /// </summary>
    [SerializeField] QuantumRagdoll _ragdollView;
    
    /// <summary>
    /// Setup the root of the ragdoll system as a <see cref="QuantumRagdoll"/> and set the level of depth of this limb.
    /// </summary>
    /// <param name="ragdoll"></param>
    /// <param name="depth"></param>
    public void SetRoot(QuantumRagdoll ragdoll, int depth) {
      _ragdollView = ragdoll;
      _depth = depth;
    }

    /// <summary>
    /// This method delegates the application of the final position and rotation interpolation to the <see cref="QuantumRagdoll"/> LateUpdate.
    /// </summary>
    /// <param name="param"></param>
    protected override void ApplyTransform(ref UpdatePositionParameter param) {
      Vector3 newPosition;
      if (param.PositionTeleport) {
        newPosition = param.UninterpolatedPosition;
      } else {
        newPosition = param.NewPosition + param.ErrorVisualVector;
      }

      Quaternion newRotation;
      if (param.RotationTeleport) {
        newRotation = param.UninterpolatedRotation;
      } else {
        newRotation = param.ErrorVisualQuaternion * param.NewRotation;
      }
      if (_ragdollView != null) {
        if ((ViewFlags & QuantumEntityViewFlags.UseCachedTransform) > 0) {
          _ragdollView.AddLimbUpdating(Transform, newPosition, newRotation, _depth);
        } else {
          _ragdollView.AddLimbUpdating(transform, newPosition, newRotation, _depth);
        }
      } else {
        Debug.LogWarning($"The limb \"{this.name}\" of the ragdoll doesn't have an root");
      }
      
    }
  }
}
