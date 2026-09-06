namespace Quantum {

  using Photon.Deterministic;
  using System;
  using System.Collections.Generic;
  using UnityEngine;

  [ExecuteInEditMode]
  public partial class QuantumRagdoll : QuantumMonoBehaviour {

    public const float ChestMass = 0.15f;
    public const float HipsMass = 0.35f;
    public const float HeadMass = 0.08f;
    public const float ArmMass = 0.025f;
    public const float ElbowMass = 0.025f;
    public const float LegMass = 0.10f;
    public const float KneeMass = 0.06f;

    /// <summary>
    /// How frequently the ragdoll is updated
    /// </summary>
    [InlineHelp][SerializeField] BakeMode _autoBakeMode = BakeMode.OnValidate;

    /// <summary>
    /// How frequently the ragdoll is updated
    /// </summary>
    [InlineHelp][SerializeField] Animator _animator;

    /// <summary>
    /// The filter for parts that will be updated or ignored during baking.
    /// </summary>
    [InlineHelp][SerializeField] PartsToUpdate _partsToUpdate = PartsToUpdate.Chest | PartsToUpdate.Hips | PartsToUpdate.Head | PartsToUpdate.Arms | PartsToUpdate.Legs;

    [InlineHelp][SerializeField] public RagdollParameters Settings;

    List<RagdollLimbUpdate> _limbUpdates = new List<RagdollLimbUpdate>();

    /// <summary>
    /// Stores the information of the limb hierarchy and the target position and rotation after the simulation.
    /// These values will be applied to the transform in the LateUpdate.
    /// </summary>
    [Serializable]
    public struct RagdollLimbUpdate : IComparable<RagdollLimbUpdate> {
      public Transform LimbTransform;
      public Vector3 Position;
      public Quaternion Rotation;
      public int Depth;

      public int CompareTo(RagdollLimbUpdate other) {
        return Depth.CompareTo(other.Depth);
      }
    }

    [Serializable]
    public enum BakeMode {
      /// <summary>
      /// Apply the ragdoll settings every frame in Unity Edit Mode.
      /// </summary>
      Immediate,
      /// <summary>
      /// Update the ragdoll only when some field in this component is updated.
      /// </summary>
      OnValidate,
      /// <summary>
      /// Disable the ragdoll update.
      /// </summary>
      Disabled
    }

    [Serializable]
    public enum BoneDirection {
      Y,
      Z,
      NegativeY,
      NegativeZ,
    }

    [Flags]
    public enum PartsToUpdate {
      None = 0,
      Chest = 1 << 0,
      Hips = 1 << 1,
      Legs = 1 << 2,
      Head = 1 << 4, 
      Arms = 1 << 8,  
    }

    [Serializable]
    public struct RagdollParameters {

      [InlineHelp][SerializeField] public BoneDirection _limbUpDirection;
      [InlineHelp][SerializeField] public float _limbThickness;
      [InlineHelp][SerializeField] public float _chestThickness;
      [InlineHelp][SerializeField] public float _chestForwardOffset;
      [InlineHelp][SerializeField] public float _bellyThickness;
      [InlineHelp][SerializeField] public float _bellyForwardOffset;
      [InlineHelp][SerializeField] public float _jointDistance;
      [InlineHelp][SerializeField] public float _headSize;
      [InlineHelp][SerializeField] public float _headDistance;
      [InlineHelp][SerializeField] public float _totalMass;
      [InlineHelp][SerializeField] public float _generalDrag;
      [InlineHelp][SerializeField] public AssetRef<PhysicsMaterial> _physicsMaterial;
      [InlineHelp][SerializeField] public Transform _root;
      [InlineHelp][SerializeField] public Transform _hip;
      [InlineHelp][SerializeField] public Transform _chest;
      [SerializeField] public Transform _head;
      [SerializeField] public Transform _leftLeg;
      [SerializeField] public Transform _leftKnee;
      [SerializeField] public Transform _leftFoot;
      [SerializeField] public Transform _rightLeg;
      [SerializeField] public Transform _rightKnee;
      [SerializeField] public Transform _rightFoot;
      [SerializeField] public Transform _leftArm;
      [SerializeField] public Transform _leftElbow;
      [SerializeField] public Transform _leftHand;
      [SerializeField] public Transform _rightArm;
      [SerializeField] public Transform _rightElbow;
      [SerializeField] public Transform _rightHand;

      T AddOrGet<T>(GameObject gameObject) where T : MonoBehaviour {
        if (gameObject.TryGetComponent<T>(out var cmp)) {
          return cmp;
        }
#if UNITY_EDITOR
        // if the component exists on the prefab asset but was removed on this instance, revert the
        // removed-component override and reuse the original component instead of adding a new one,
        // which would create a redundant added-component override on top of the removal
        if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(gameObject)) {
          var instanceRoot = UnityEditor.PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject);
          if (instanceRoot != null) {
            foreach (var removed in UnityEditor.PrefabUtility.GetRemovedComponents(instanceRoot)) {
              if (removed.containingInstanceGameObject == gameObject && removed.assetComponent is T) {
                removed.Revert(UnityEditor.InteractionMode.AutomatedAction);
                break;
              }
            }
            if (gameObject.TryGetComponent<T>(out cmp)) {
              return cmp;
            }
          }
        }
#endif
        return gameObject.AddComponent<T>();
      }

      void CreateLimb(Transform from, Transform to, float massPercent, int depth) {
        if (from == null || to == null) return;

        var limb = AddOrGet<QuantumEntityPrototype>(from.gameObject);
        var view = AddOrGet<QuantumRagdollLimbView>(limb.gameObject);
        view.SetRoot(_root.GetComponent<QuantumRagdoll>(), depth);

        var radiusScaleFactor = Math.Min(limb.transform.lossyScale.x, limb.transform.lossyScale.z);
        var extentScaleFactor = from.transform.lossyScale.y;

        limb.TransformMode = QuantumEntityPrototypeTransformMode.Transform3D;
        limb.PhysicsCollider.IsEnabled = true;
        limb.PhysicsCollider.Material = _physicsMaterial;
        var capsuleHeight = (Vector3.Distance(from.position, to.position) - _jointDistance) / extentScaleFactor;
        var up = Vector3.zero;
        switch (_limbUpDirection) {
          case BoneDirection.Y: up = Vector3.up; break;
          case BoneDirection.Z: up = Vector3.forward; break;
          case BoneDirection.NegativeY: up = Vector3.down; break;
          case BoneDirection.NegativeZ: up = Vector3.back; break;
        }

        var computedShape = new Shape3DConfig() {
          ShapeType = Shape3DType.Capsule,
          CapsuleRadius = (_limbThickness).ToFP(),
          CapsuleHeight = (capsuleHeight).ToFP(),
          PositionOffset = (up * ((capsuleHeight / 2) + (_jointDistance / extentScaleFactor / 2))).ToFPVector3(),
        };
        limb.PhysicsBody.IsEnabled = true;
        // bakes the computed values plus the limb override deltas captured from direct user edits
        view.ApplyBake(limb, computedShape, (massPercent * _totalMass).ToFP(), (_generalDrag).ToFP(), (_generalDrag).ToFP());
      }

      void CreateLimbAndConnectAt(Transform start, Transform end, Transform connectedAt, float massPercent, int depth, Vector3 achorOffset, Vector3 connectedOffset, Vector3 twist, Vector3 swing, FP lowAngle, FP upperAngle, FP swing1, FP swing2, bool computeRelative = false) {
        if (start == null || end == null || connectedAt == null) return;
        CreateLimb(start, end, massPercent, depth);

        var entity = start.gameObject.GetComponent<QuantumEntityPrototype>();
        var joint = AddOrGet<QPrototypePhysicsJoints3D>(entity.gameObject);
        var view = start.gameObject.GetComponent<QuantumRagdollLimbView>();

        var part = start.gameObject.GetComponent<QuantumEntityPrototype>();
        var connection = connectedAt.gameObject.GetComponent<QuantumEntityPrototype>();

        if (computeRelative) {
          connectedOffset = InverseTransformPoint(connectedAt, part.transform.position) + achorOffset;
        }

        var computedJoint = new Prototypes.Unity.Joint3DConfig() {
          JointType = Quantum.Physics3D.JointType3D.CharacterJoint,
          ConnectedEntity = connection,
          Anchor = achorOffset.ToFPVector3(),
          ConnectedAnchor = connectedOffset.ToFPVector3(),
          SwingAxis = swing.ToFPVector3(),
          UseTwistAngleLimits = true,
          TwistAxis = twist.ToFPVector3(),
          TwistLowerAngle = lowAngle,
          TwistUpperAngle = upperAngle,
          Swing1AngleLimits = swing1,
          Swing2AngleLimits = swing2
        };
        // bakes the computed joint config plus the limb override deltas on the character joint angle limits
        view.ApplyJointBake(joint, computedJoint);
      }

      static public Vector3 InverseTransformPoint(Transform transform, Vector3 worldPosition) {
        return Quaternion.Inverse(transform.rotation) * (worldPosition - transform.position);
      }

      static public Vector3 InverseTransformDirection(Transform transform, Vector3 worldDirection) {
        return Quaternion.Inverse(transform.rotation) * worldDirection;
      }

      public Vector3 FixAxis(Vector3 limbDirection, Vector3 worldDirection) {
        return Vector3.Dot(limbDirection, worldDirection) > 0 ? worldDirection : -worldDirection;
      }
      public void BuildRagDoll(PartsToUpdate partsToUpdate) {

        if (_root == null) return;

        bool UpdateChest = (partsToUpdate & PartsToUpdate.Chest) != 0;
        bool UpdateHips = (partsToUpdate & PartsToUpdate.Hips) != 0;
        bool UpdateLegs = (partsToUpdate & PartsToUpdate.Legs) != 0;
        bool UpdateHead = (partsToUpdate & PartsToUpdate.Head) != 0;
        bool UpdateArms = (partsToUpdate & PartsToUpdate.Arms) != 0;

        var hipsPosition = InverseTransformPoint(_root, new Vector3(0f, -0.3f, 0));
        var chestPosition = InverseTransformPoint(_root, new Vector3(0f, 0.8f, 0));

        if (_hip != null) {
          hipsPosition = InverseTransformPoint(_root, _hip.transform.position);
        }

        if (_chest != null) {
          chestPosition = InverseTransformPoint(_root, _chest.transform.position);
        }

        var rightArmPosition = InverseTransformPoint(_root, new Vector3(0.8f, 0.2f, 0));
        var leftArmPosition = InverseTransformPoint(_root, new Vector3(-0.8f, 0.2f, 0));
        var rightLegPosition = InverseTransformPoint(_root, new Vector3(0.4f, -1f, 0));
        var leftLegPosition = InverseTransformPoint(_root, new Vector3(-0.4f, -1f, 0));

        if (_rightArm != null) {
          rightArmPosition = InverseTransformPoint(_root, _rightArm.transform.position);
        }
        if (_leftArm != null) {
          leftArmPosition = InverseTransformPoint(_root, _leftArm.transform.position);
        }
        if (_rightLeg != null) {
          rightLegPosition = InverseTransformPoint(_root, _rightLeg.transform.position);
        }
        if (_leftLeg != null) {
          leftLegPosition = InverseTransformPoint(_root, _leftLeg.transform.position);
        }

        var midShoulders = (rightArmPosition + leftArmPosition) / 2;
        var midLegs = (rightLegPosition + leftLegPosition) / 2;
        var midBody = (midShoulders + midLegs) / 2;

        // hips
        if (_hip != null && UpdateHips) {
          var hipWidthExtent = (Vector3.Distance(rightLegPosition, leftLegPosition) / 2);

          var hipHeightExtent = Vector3.Distance(midBody, midLegs) / 2;
          hipHeightExtent -= _jointDistance * _hip.transform.lossyScale.y;

          var hips = AddOrGet<QuantumEntityPrototype>(_hip.gameObject);
          var view = AddOrGet<QuantumRagdollLimbView>(_hip.gameObject);
          view.SetRoot(_root.GetComponent<QuantumRagdoll>(), 0);

          // update the offset of the hips to match with the distance of the chest and legs
          var center = (midBody + midLegs) / 2;
          var offset = -(hipsPosition - (midBody + midLegs) / 2) / hips.transform.lossyScale.y;
          offset += Vector3.forward * _bellyForwardOffset;

          var boxExtents = new Vector3(hipWidthExtent, hipHeightExtent, (_limbThickness + _bellyThickness) * hips.transform.lossyScale.z);
          boxExtents.x /= hips.transform.lossyScale.x;
          boxExtents.y /= hips.transform.lossyScale.y;
          boxExtents.z /= hips.transform.lossyScale.z;

          // add the shape config with transform, collider, body and box shape
          hips.TransformMode = QuantumEntityPrototypeTransformMode.Transform3D;
          hips.PhysicsCollider.IsEnabled = true;
          hips.PhysicsCollider.Material = _physicsMaterial;
          hips.PhysicsBody.IsEnabled = true;
          var computedShape = new Shape3DConfig() {
            ShapeType = Shape3DType.Box,
            PositionOffset = offset.ToFPVector3(),
            BoxExtents = boxExtents.ToFPVector3()
          };
          view.ApplyBake(hips, computedShape, (HipsMass * _totalMass).ToFP());
        }

        // chest
        if (_chest != null && UpdateChest) {
          var chest = AddOrGet<QuantumEntityPrototype>(_chest.gameObject);
          var view = AddOrGet<QuantumRagdollLimbView>(_chest.gameObject);
          var chestWidthExtent = (Vector3.Distance(rightArmPosition, leftArmPosition) / 2) - (_limbThickness + _jointDistance) * chest.transform.lossyScale.x;
          var chestHeightExtent = (Vector3.Distance(midBody, midShoulders) / 2);

          view.SetRoot(_root.GetComponent<QuantumRagdoll>(), 1);

          // add the shape config with transform, collider, body and box shape
          var offset = -(chestPosition - (midBody + midShoulders) / 2) / chest.transform.lossyScale.y;
          offset += Vector3.forward * _chestForwardOffset;

          var boxExtents = new Vector3(chestWidthExtent, chestHeightExtent, (_limbThickness + _chestThickness) * chest.transform.lossyScale.z);
          boxExtents.x /= chest.transform.lossyScale.x;
          boxExtents.y /= chest.transform.lossyScale.y;
          boxExtents.z /= chest.transform.lossyScale.z;

          chest.TransformMode = QuantumEntityPrototypeTransformMode.Transform3D;
          chest.PhysicsCollider.IsEnabled = true;
          chest.PhysicsCollider.Material = _physicsMaterial;
          chest.PhysicsBody.IsEnabled = true;
          var computedShape = new Shape3DConfig() {
            ShapeType = Shape3DType.Box,
            PositionOffset = offset.ToFPVector3(),
            BoxExtents = boxExtents.ToFPVector3()
          };
          view.ApplyBake(chest, computedShape, (ChestMass * _totalMass).ToFP());

          // connects the chest to the hips since the hips is the center of mass
          var anchorOffset = midBody - InverseTransformPoint(_root, _chest.position);
          var connectedOffset = midBody - InverseTransformPoint(_root, _hip.position);

          if (_hip != null) {
            var joint = AddOrGet<QPrototypePhysicsJoints3D>(_chest.gameObject);
            var computedJoint = new Prototypes.Unity.Joint3DConfig() {
              JointType = Quantum.Physics3D.JointType3D.CharacterJoint,
              ConnectedEntity = _hip.gameObject.GetComponent<QuantumEntityPrototype>(),
              Anchor = anchorOffset.ToFPVector3(),
              ConnectedAnchor = connectedOffset.ToFPVector3(),
              SwingAxis = FPVector3.Forward,
              UseTwistAngleLimits = true,
              TwistAxis = FPVector3.Right,
              TwistLowerAngle = -20,
              TwistUpperAngle = 20,
              Swing1AngleLimits = FP._10,
              Swing2AngleLimits = FP._3
            };
            view.ApplyJointBake(joint, computedJoint);
          }
        }

        // head
        if (_head != null && UpdateHead) {

          var headPosition = InverseTransformPoint(_root, _head.transform.position);

          var head = AddOrGet<QuantumEntityPrototype>(_head.gameObject);
          var view = AddOrGet<QuantumRagdollLimbView>(_head.gameObject);
          view.SetRoot(_root.GetComponent<QuantumRagdoll>(), 2);

          head.TransformMode = QuantumEntityPrototypeTransformMode.Transform3D;
          head.PhysicsCollider.IsEnabled = true;
          head.PhysicsCollider.Material = _physicsMaterial;
          head.PhysicsBody.IsEnabled = true;
          var computedShape = new Shape3DConfig() {
            ShapeType = Shape3DType.Sphere,
            SphereRadius = (_headSize).ToFP(),
            PositionOffset = FPVector3.Up * _headDistance.ToFP(),
          };
          view.ApplyBake(head, computedShape, (_totalMass * HeadMass).ToFP());

          // link the head into the chest
          if (_chest != null) {
            var headAnchor = Vector3.zero;
            var chestAnchor = headPosition - chestPosition;
            var joint = AddOrGet<QPrototypePhysicsJoints3D>(_head.gameObject);
            var computedJoint = new Prototypes.Unity.Joint3DConfig() {
              JointType = Quantum.Physics3D.JointType3D.CharacterJoint,
              ConnectedEntity = _chest.gameObject.GetComponent<QuantumEntityPrototype>(),
              Anchor = headAnchor.ToFPVector3(),
              ConnectedAnchor = chestAnchor.ToFPVector3(),
              SwingAxis = FPVector3.Forward,
              UseTwistAngleLimits = true,
              TwistAxis = FPVector3.Right,
              TwistLowerAngle = -40,
              TwistUpperAngle = 25,
              Swing1AngleLimits = FP._25,
              Swing2AngleLimits = FP._3
            };
            view.ApplyJointBake(joint, computedJoint);
          }
        }

        // arms

        if (_rightArm != null && _rightElbow != null && UpdateArms) {

          // the right shoulder: link arm to chest
          CreateLimbAndConnectAt(_rightArm, _rightElbow, _chest, ArmMass, 2,
            Vector3.zero,
            Vector3.zero,
            twist: FixAxis(_rightArm.up, Vector3.up),
            swing: FixAxis(_rightArm.forward, Vector3.forward),
            lowAngle: -90,
            upperAngle: 50,
            swing1: 90,
            swing2: 15,
            computeRelative: true
          );

          if (_rightHand != null) {
            // the right elbow: link forearm to arm
            CreateLimbAndConnectAt(_rightElbow, _rightHand, _rightArm, ElbowMass, 3,
              Vector3.zero,
              Vector3.zero,
              twist: FixAxis(_rightElbow.forward, Vector3.forward),
              swing: FixAxis(_rightElbow.up, Vector3.up),
              lowAngle: -90,
              upperAngle: 3,
              swing1: 3,
              swing2: 3,
              computeRelative: true
            );
          }
        }

        if (_leftArm != null && _leftElbow != null && UpdateArms) {
          // the left shoulder: link arm to chest
          CreateLimbAndConnectAt(_leftArm, _leftElbow, _chest, ArmMass, 2,
            Vector3.zero,
            Vector3.zero,
            twist: FixAxis(_leftArm.up, Vector3.up),
            swing: FixAxis(_leftArm.forward, Vector3.forward),
            lowAngle: -90,
            upperAngle: 50,
            swing1: 90,
            swing2: 15,
            computeRelative: true
          );

          if (_leftHand != null) {
            // the left elbow: link forearm to arm
            CreateLimbAndConnectAt(_leftElbow, _leftHand, _leftArm, ElbowMass, 3,
              Vector3.zero,
              Vector3.zero,
              twist: FixAxis(_leftElbow.forward, Vector3.forward),
              swing: FixAxis(_leftElbow.up, Vector3.up),
              lowAngle: -90,
              upperAngle: 3,
              swing1: 3,
              swing2: 3,
              computeRelative: true
            );
          }
        }

        // legs 

        if (_rightLeg != null && _rightKnee != null && UpdateLegs) { 

          // the right leg
          CreateLimbAndConnectAt(_rightLeg, _rightKnee, _hip, LegMass, 1,
            Vector3.zero,
            Vector3.zero,
            twist: FixAxis(_rightLeg.right, Vector3.right),
            swing: FixAxis(_rightLeg.forward, Vector3.forward),
            lowAngle: -50,
            upperAngle: 90,
            swing1: 50,
            swing2: 3,
            computeRelative: true
          );

          if (_rightFoot != null) {
            // the right knee
            CreateLimbAndConnectAt(_rightKnee, _rightFoot, _rightLeg, KneeMass, 2,
              Vector3.zero,
              Vector3.zero,
              twist: FixAxis(_rightKnee.right, Vector3.right),
              swing: FixAxis(_rightKnee.forward, Vector3.forward),
              lowAngle: -80,
              upperAngle: 0,
              swing1: 3,
              swing2: 3,
              computeRelative: true
            );
          }
        }

        if (_leftLeg != null && _leftKnee != null && UpdateLegs) {
          // the right leg
          CreateLimbAndConnectAt(_leftLeg, _leftKnee, _hip, LegMass, 1,
            Vector3.zero,
            Vector3.zero,
            twist: FixAxis(_leftLeg.right, Vector3.right),
            swing: FixAxis(_leftLeg.forward, Vector3.forward),
            lowAngle: -50,
            upperAngle: 90,
            swing1: 50,
            swing2: 3,
            computeRelative: true
          );

          if (_leftFoot != null) {
            // the right knee
            CreateLimbAndConnectAt(_leftKnee, _leftFoot, _leftLeg, KneeMass, 2,
              Vector3.zero,
              Vector3.zero,
              twist: FixAxis(_leftKnee.right, Vector3.right),
              swing: FixAxis(_leftKnee.forward, Vector3.forward),
              lowAngle: -80,
              upperAngle: 0,
              swing1: 3,
              swing2: 3,
              computeRelative: true
            );
          }
        }
      }

    }

    public void AddLimbUpdating(Transform transform, Vector3 position, Quaternion rotation, int depth) {
      _limbUpdates.Add(new RagdollLimbUpdate() {
        LimbTransform = transform,
        Position = position,
        Rotation = rotation,
        Depth = depth
      });
    }

    private Transform GetBone(params HumanBodyBones[] bones) {
      foreach (var bone in bones) {
        Transform t = _animator.GetBoneTransform(bone);
        if (t != null)
          return t;
      }

      return null;
    }

    private void TrySetAnimator(Animator animator) {
      if (animator != null) {
        Settings._hip = GetBone(HumanBodyBones.Hips);
        Settings._chest = GetBone(HumanBodyBones.Chest);
        Settings._head = GetBone(HumanBodyBones.Head);

        // left arm and elbow
        Settings._leftArm = GetBone(HumanBodyBones.LeftUpperArm);
        Settings._leftElbow = GetBone(HumanBodyBones.LeftLowerArm);

        // left hand
        Settings._leftHand = GetBone(
            HumanBodyBones.LeftIndexDistal,
            HumanBodyBones.LeftHand
        );

        // right arm and elbow
        Settings._rightArm = GetBone(HumanBodyBones.RightUpperArm);
        Settings._rightElbow = GetBone(HumanBodyBones.RightLowerArm);

        // right hand
        Settings._rightHand = GetBone(
            HumanBodyBones.RightIndexDistal,
            HumanBodyBones.RightHand
        );

        // left leg and knee
        Settings._leftLeg = GetBone(HumanBodyBones.LeftUpperLeg);
        Settings._leftKnee = GetBone(HumanBodyBones.LeftLowerLeg);

        // left foot
        Settings._leftFoot = GetBone(
            HumanBodyBones.LeftToes,
            HumanBodyBones.LeftFoot
        );

        // left leg and knee
        Settings._rightLeg = GetBone(HumanBodyBones.RightUpperLeg);
        Settings._rightKnee = GetBone(HumanBodyBones.RightLowerLeg);

        // right foot
        Settings._rightFoot = GetBone(
            HumanBodyBones.RightToes,
            HumanBodyBones.RightFoot
        );
      }
    }

    public void BuildRagdoll() {
      TrySetAnimator(_animator);
      Settings.BuildRagDoll(_partsToUpdate);
#if UNITY_EDITOR
      ScheduleStaleLimbCleanup();
#endif
    }

#if UNITY_EDITOR
    bool _cleanupScheduled;

    private void ScheduleStaleLimbCleanup() {
      // destroying components is not allowed during OnValidate, so the cleanup runs on the next editor update
      if (_cleanupScheduled) {
        return;
      }
      _cleanupScheduled = true;
      UnityEditor.EditorApplication.delayCall += () => {
        _cleanupScheduled = false;
        if (this == null || Application.isPlaying) {
          return;
        }
        CleanupStaleLimbs();
      };
    }

    /// <summary>
    /// Removes the baked components (<see cref="QuantumEntityPrototype"/>, <see cref="QuantumRagdollLimbView"/> and
    /// <see cref="QPrototypePhysicsJoints3D"/>) from transforms that were baked by this ragdoll before but are no
    /// longer assigned as limbs in <see cref="Settings"/>.
    private void CleanupStaleLimbs() {
      if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this)) {
        // the imported prefab asset objects cannot be modified directly; the cleanup runs in the prefab stage instead
        return;
      }

      var limbs = new HashSet<Transform>();
      if (Settings._hip != null) limbs.Add(Settings._hip);
      if (Settings._chest != null) limbs.Add(Settings._chest);
      if (Settings._head != null) limbs.Add(Settings._head);
      if (Settings._leftArm != null) limbs.Add(Settings._leftArm);
      if (Settings._leftElbow != null) limbs.Add(Settings._leftElbow);
      if (Settings._rightArm != null) limbs.Add(Settings._rightArm);
      if (Settings._rightElbow != null) limbs.Add(Settings._rightElbow);
      if (Settings._leftLeg != null) limbs.Add(Settings._leftLeg);
      if (Settings._leftKnee != null) limbs.Add(Settings._leftKnee);
      if (Settings._rightLeg != null) limbs.Add(Settings._rightLeg);
      if (Settings._rightKnee != null) limbs.Add(Settings._rightKnee);

      CleanupStaleLimbsInHierarchy(transform, limbs);
      if (_animator != null && _animator.transform.IsChildOf(transform) == false) {
        CleanupStaleLimbsInHierarchy(_animator.transform, limbs);
      }
    }

    private void CleanupStaleLimbsInHierarchy(Transform hierarchyRoot, HashSet<Transform> limbs) {
      foreach (var view in hierarchyRoot.GetComponentsInChildren<QuantumRagdollLimbView>(true)) {
        if (view.Ragdoll != null && view.Ragdoll != this) continue;
        if (limbs.Contains(view.transform)) continue;
        RemoveLimbComponents(view.gameObject);
      }
    }

    private void RemoveLimbComponents(GameObject limb) {
      // the component prototypes require the entity prototype, so they are removed first
      if (limb.TryGetComponent<QPrototypePhysicsJoints3D>(out var joints)) {
        RemoveComponent(joints);
      }
      if (limb.TryGetComponent<QuantumRagdollLimbView>(out var view)) {
        RemoveComponent(view);
      }
      if (limb.TryGetComponent<QuantumEntityPrototype>(out var prototype)) {
        // the entity prototype is kept while any component prototype still requires it,
        // either one skipped above or one not baked by the ragdoll
        if (limb.GetComponent<QuantumUnityComponentPrototype>() == null) {
          RemoveComponent(prototype);
        }
      }
    }

    static void RemoveComponent(Component component) {
      if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(component) &&
          UnityEditor.PrefabUtility.IsAddedComponentOverride(component) == false) {
        return;
      }
      UnityEditor.Undo.DestroyObjectImmediate(component);
    }
#endif

    void OnValidate() {
      if (_autoBakeMode == BakeMode.OnValidate) {
        BuildRagdoll();
      }
      Settings._limbThickness = Mathf.Max(0.001f, Settings._limbThickness);
      Settings._chestThickness = Mathf.Max(0.001f, Settings._chestThickness);
      Settings._bellyThickness = Mathf.Max(0.001f, Settings._bellyThickness);
      Settings._jointDistance = Mathf.Max(0.001f, Settings._jointDistance);
      Settings._headSize = Mathf.Max(0.001f, Settings._headSize);
      Settings._totalMass = Mathf.Max(0.001f, Settings._totalMass);
      Settings._generalDrag = Mathf.Max(0f, Settings._generalDrag);
    }

    void Update() {
      if (Application.isPlaying == false && _autoBakeMode == BakeMode.Immediate) {
        BuildRagdoll();
      }
    }

    private void Reset() {
      Settings._limbThickness = 0.18f;
      Settings._jointDistance = 0.05f;
      Settings._headSize = 0.4f;
      Settings._headDistance = 0.1f;
      Settings._totalMass = 20;
      Settings._generalDrag = 1f;
      Settings._limbUpDirection = BoneDirection.Y;
    }

    void LateUpdate() {
      _limbUpdates.Sort();
      foreach (var update in _limbUpdates) {
        update.LimbTransform.rotation = update.Rotation;
        update.LimbTransform.position = update.Position;
      }
      _limbUpdates.Clear();
    }
  }
}
