namespace Quantum {

  using Photon.Deterministic;
  using System;
  using UnityEngine;

  /// <summary>
  /// A custom entity view for the parts of the ragdoll. Since ragdoll limbs can have parents in Unity's hierarchy, they must be updated in the correct order.
  /// Otherwise, the updating of a parent transform position right after its child position will mess with the limb position.
  /// </summary>
  public class QuantumRagdollLimbView : QuantumEntityView {

    public int _depth;
    [SerializeField] QuantumRagdoll _ragdollView;
    [HideInInspector]public LimbOverrides _overrides;
    [SerializeField, HideInInspector] LimbBakeSnapshot _lastBaked;
    [NonSerialized] public bool _hasPendingPrototypeEdits;
    [NonSerialized] public bool _hasPendingJointEdits;

    /// <summary>
    /// The <see cref="QuantumRagdoll"/> that baked this limb. Used to track the ownership of the baked components.
    /// </summary>
    public QuantumRagdoll Ragdoll => _ragdollView;

    /// <summary>
    /// True when any of the per-limb override deltas is not zero.
    /// </summary>
    public bool HasOverrides => _overrides.IsZero == false;

    [Serializable]
    public struct LimbOverrides {
      public FP CapsuleRadius;
      public FP CapsuleHeight;
      public FP SphereRadius;
      public FPVector3 BoxExtents;
      public FPVector3 PositionOffset;
      public FP Mass;
      public FP Drag;
      public FP AngularDrag;
      public FP TwistLowerAngle;
      public FP TwistUpperAngle;
      public FP Swing1AngleLimits;
      public FP Swing2AngleLimits;

      public bool IsZero =>
        CapsuleRadius.RawValue == 0 &&
        CapsuleHeight.RawValue == 0 &&
        SphereRadius.RawValue == 0 &&
        BoxExtents == FPVector3.Zero &&
        PositionOffset == FPVector3.Zero &&
        Mass.RawValue == 0 &&
        Drag.RawValue == 0 &&
        AngularDrag.RawValue == 0 &&
        TwistLowerAngle.RawValue == 0 &&
        TwistUpperAngle.RawValue == 0 &&
        Swing1AngleLimits.RawValue == 0 &&
        Swing2AngleLimits.RawValue == 0;
    }

    [Serializable]
    public struct LimbBakeSnapshot {
      public bool IsValid;
      public Shape3DType ShapeType;
      public FP CapsuleRadius;
      public FP CapsuleHeight;
      public FP SphereRadius;
      public FPVector3 BoxExtents;
      public FPVector3 PositionOffset;
      public FP Mass;
      public FP Drag;
      public FP AngularDrag;
      public bool HasJoint;
      public FP TwistLowerAngle;
      public FP TwistUpperAngle;
      public FP Swing1AngleLimits;
      public FP Swing2AngleLimits;
    }

    public void SetRoot(QuantumRagdoll ragdoll, int depth) {
      _ragdollView = ragdoll;
      _depth = depth;
    }

    public void ApplyBake(QuantumEntityPrototype prototype, Shape3DConfig computed, FP computedMass) {
      ApplyBake(prototype, computed, computedMass, default, default, includeDamping: false);
    }

    public void ApplyBake(QuantumEntityPrototype prototype, Shape3DConfig computed, FP computedMass, FP computedDrag, FP computedAngularDrag) {
      ApplyBake(prototype, computed, computedMass, computedDrag, computedAngularDrag, includeDamping: true);
    }

    void ApplyBake(QuantumEntityPrototype prototype, Shape3DConfig computed, FP computedMass, FP computedDrag, FP computedAngularDrag, bool includeDamping) {
      if (_hasPendingPrototypeEdits) {
        AdoptPrototypeEdits(prototype);
      }

      // start from the existing config so the shape fields not computed by the bake
      // (RotationOffset, CapsuleDirection, UserTag, ...) are preserved
      var shape = prototype.PhysicsCollider.Shape3D ?? new Shape3DConfig();
      shape.ShapeType = computed.ShapeType;
      shape.PositionOffset = computed.PositionOffset + _overrides.PositionOffset;

      switch (computed.ShapeType) {
        case Shape3DType.Capsule:
          shape.CapsuleRadius = computed.CapsuleRadius + _overrides.CapsuleRadius;
          shape.CapsuleHeight = computed.CapsuleHeight + _overrides.CapsuleHeight;
          break;
        case Shape3DType.Sphere:
          shape.SphereRadius = computed.SphereRadius + _overrides.SphereRadius;
          break;
        case Shape3DType.Box:
          shape.BoxExtents = computed.BoxExtents + _overrides.BoxExtents;
          break;
      }

      prototype.PhysicsCollider.Shape3D = shape;
      prototype.PhysicsBody.Mass = computedMass + _overrides.Mass;
      if (includeDamping) {
        prototype.PhysicsBody.Drag = computedDrag + _overrides.Drag;
        prototype.PhysicsBody.AngularDrag = computedAngularDrag + _overrides.AngularDrag;
      }

      WriteSnapshot(prototype);
    }

    public void ApplyJointBake(QPrototypePhysicsJoints3D joint, Prototypes.Unity.Joint3DConfig computed) {
      if (_hasPendingJointEdits) {
        AdoptJointEdits(joint);
      }

      computed.TwistLowerAngle += _overrides.TwistLowerAngle;
      computed.TwistUpperAngle += _overrides.TwistUpperAngle;
      if (computed.Swing1AngleLimits.HasValue) {
        computed.Swing1AngleLimits = computed.Swing1AngleLimits.Value + _overrides.Swing1AngleLimits;
      }
      if (computed.Swing2AngleLimits.HasValue) {
        computed.Swing2AngleLimits = computed.Swing2AngleLimits.Value + _overrides.Swing2AngleLimits;
      }

      joint.Prototype.JointConfigs = new Prototypes.Unity.Joint3DConfig[] { computed };

      _lastBaked.HasJoint = true;
      _lastBaked.TwistLowerAngle = computed.TwistLowerAngle;
      _lastBaked.TwistUpperAngle = computed.TwistUpperAngle;
      _lastBaked.Swing1AngleLimits = computed.Swing1AngleLimits.ValueOrDefault(default);
      _lastBaked.Swing2AngleLimits = computed.Swing2AngleLimits.ValueOrDefault(default);
    }

    public void AdoptPrototypeEdits(QuantumEntityPrototype prototype) {
      _hasPendingPrototypeEdits = false;

      if (_lastBaked.IsValid == false) {
        return;
      }

      var shape = prototype.PhysicsCollider.Shape3D;
      if (shape != null && shape.ShapeType == _lastBaked.ShapeType) {
        // when the user changed the shape type manually the shape edits are ignored and the bake restores the type
        _overrides.PositionOffset += shape.PositionOffset - _lastBaked.PositionOffset;
        _lastBaked.PositionOffset = shape.PositionOffset;
        switch (_lastBaked.ShapeType) {
          case Shape3DType.Capsule:
            _overrides.CapsuleRadius += shape.CapsuleRadius - _lastBaked.CapsuleRadius;
            _overrides.CapsuleHeight += shape.CapsuleHeight - _lastBaked.CapsuleHeight;
            _lastBaked.CapsuleRadius = shape.CapsuleRadius;
            _lastBaked.CapsuleHeight = shape.CapsuleHeight;
            break;
          case Shape3DType.Sphere:
            _overrides.SphereRadius += shape.SphereRadius - _lastBaked.SphereRadius;
            _lastBaked.SphereRadius = shape.SphereRadius;
            break;
          case Shape3DType.Box:
            _overrides.BoxExtents += shape.BoxExtents - _lastBaked.BoxExtents;
            _lastBaked.BoxExtents = shape.BoxExtents;
            break;
        }
      }

      _overrides.Mass += prototype.PhysicsBody.Mass - _lastBaked.Mass;
      _lastBaked.Mass = prototype.PhysicsBody.Mass;

      if (_lastBaked.ShapeType == Shape3DType.Capsule) {
        // only the capsule limbs bake the damping values; on the other parts they are never
        // written by the bake, so direct edits stick without being tracked
        _overrides.Drag += prototype.PhysicsBody.Drag - _lastBaked.Drag;
        _overrides.AngularDrag += prototype.PhysicsBody.AngularDrag - _lastBaked.AngularDrag;
        _lastBaked.Drag = prototype.PhysicsBody.Drag;
        _lastBaked.AngularDrag = prototype.PhysicsBody.AngularDrag;
      }
    }

    public void AdoptJointEdits(QPrototypePhysicsJoints3D joint) {
      _hasPendingJointEdits = false;

      if (_lastBaked.HasJoint == false) {
        return;
      }

      var configs = joint.Prototype.JointConfigs;
      if (configs == null || configs.Length == 0 || configs[0] == null) {
        return;
      }

      var current = configs[0];
      if (current.JointType != Physics3D.JointType3D.CharacterJoint) {
        // when the user changed the joint type manually the joint edits are ignored and the bake restores the type
        return;
      }

      _overrides.TwistLowerAngle += current.TwistLowerAngle - _lastBaked.TwistLowerAngle;
      _overrides.TwistUpperAngle += current.TwistUpperAngle - _lastBaked.TwistUpperAngle;
      _lastBaked.TwistLowerAngle = current.TwistLowerAngle;
      _lastBaked.TwistUpperAngle = current.TwistUpperAngle;

      // the swing limits are nullable; when the user cleared a limit the edit is ignored and the bake restores it
      if (current.Swing1AngleLimits.HasValue) {
        _overrides.Swing1AngleLimits += current.Swing1AngleLimits.Value - _lastBaked.Swing1AngleLimits;
        _lastBaked.Swing1AngleLimits = current.Swing1AngleLimits.Value;
      }
      if (current.Swing2AngleLimits.HasValue) {
        _overrides.Swing2AngleLimits += current.Swing2AngleLimits.Value - _lastBaked.Swing2AngleLimits;
        _lastBaked.Swing2AngleLimits = current.Swing2AngleLimits.Value;
      }
    }

    void WriteSnapshot(QuantumEntityPrototype prototype) {
      var shape = prototype.PhysicsCollider.Shape3D;
      // assigned field by field so the joint part of the snapshot, written by ApplyJointBake, is preserved
      _lastBaked.IsValid = true;
      _lastBaked.ShapeType = shape.ShapeType;
      _lastBaked.CapsuleRadius = shape.CapsuleRadius;
      _lastBaked.CapsuleHeight = shape.CapsuleHeight;
      _lastBaked.SphereRadius = shape.SphereRadius;
      _lastBaked.BoxExtents = shape.BoxExtents;
      _lastBaked.PositionOffset = shape.PositionOffset;
      _lastBaked.Mass = prototype.PhysicsBody.Mass;
      _lastBaked.Drag = prototype.PhysicsBody.Drag;
      _lastBaked.AngularDrag = prototype.PhysicsBody.AngularDrag;
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
