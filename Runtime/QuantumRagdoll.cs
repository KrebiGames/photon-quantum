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
    [InlineHelp]
    [SerializeField]
    BakeMode _autoBakeMode = BakeMode.OnValidate;

    /// <summary>
    /// The filter for parts that will be updated or ignored during baking.
    /// </summary>
    [InlineHelp]
    [SerializeField]
    public PartsToUpdate _partsToUpdate = PartsToUpdate.Chest | PartsToUpdate.Hips | PartsToUpdate.Head | PartsToUpdate.Arms | PartsToUpdate.Legs;

    [InlineHelp]
    [SerializeField] public RagdollParameters Settings;

    List<RagdollLimbUpdate> LimbUpdates = new List<RagdollLimbUpdate>();

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

      /// <summary>
      /// 
      /// </summary>
      /// <param name="other"></param>
      /// <returns></returns>
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
      [InlineHelp][SerializeField] public float _jointDistance;
      [InlineHelp][SerializeField] public float _headSize;
      [InlineHelp][SerializeField] public float _headDistance;
      [InlineHelp][SerializeField] public float _totalMass;


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
        var capsuleHeight = (Vector3.Distance(from.position, to.position) - _jointDistance) / extentScaleFactor;
        var up = Vector3.zero;
        switch (_limbUpDirection) {
          case BoneDirection.Y: up = Vector3.up; break;
          case BoneDirection.Z: up = Vector3.forward; break;
          case BoneDirection.NegativeY: up = Vector3.down; break;
          case BoneDirection.NegativeZ: up = Vector3.back; break;
        }

        limb.PhysicsCollider.Shape3D = new Shape3DConfig() {
          ShapeType = Shape3DType.Capsule,
          CapsuleRadius = (_limbThickness / radiusScaleFactor).ToFP(),
          CapsuleHeight = (capsuleHeight).ToFP(),
          PositionOffset = (up * ((capsuleHeight / 2) + (_jointDistance / extentScaleFactor / 2))).ToFPVector3(),
        };
        limb.PhysicsBody.IsEnabled = true;
        limb.PhysicsBody.Mass = (massPercent * _totalMass).ToFP();
      }

      void CreateLimbAndConnectAt(Transform start, Transform end, Transform connectedAt, float massPercent, int depth, Vector3 achorOffset, Vector3 connectedOffset, Vector3 twist, Vector3 swing, FP lowAngle, FP upperAngle, FP swing1, FP swing2, bool computeRelative = false) {
        if (start == null || end == null || connectedAt == null) return;
        CreateLimb(start, end, massPercent, depth);

        var entity = start.gameObject.GetComponent<QuantumEntityPrototype>();
        var joint = AddOrGet<QPrototypePhysicsJoints3D>(entity.gameObject);

        var part = start.gameObject.GetComponent<QuantumEntityPrototype>();
        var connection = connectedAt.gameObject.GetComponent<QuantumEntityPrototype>();

        if (computeRelative) {
          connectedOffset = InverseTransformPoint(connectedAt, part.transform.position) + achorOffset;
        }

        joint.Prototype.JointConfigs = new Prototypes.Unity.Joint3DConfig[]{
          new (){
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
          }
        };
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
          chestPosition = _root.InverseTransformPoint(_chest.transform.position);
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
        var hipWidthExtent = (Vector3.Distance(rightLegPosition, leftLegPosition) / 2);
        hipWidthExtent += _jointDistance;

        var hipHeightExtent = Vector3.Distance(midBody, midLegs) / 2;
        hipHeightExtent -= _jointDistance;

        if (_hip != null && UpdateHips) {
          var hips = AddOrGet<QuantumEntityPrototype>(_hip.gameObject);
          var view = AddOrGet<QuantumRagdollLimbView>(_hip.gameObject);
          view.SetRoot(_root.GetComponent<QuantumRagdoll>(), 0);

          // update the offset of the hips to match with the distance of the chest and legs
          var offset = (-Vector3.up * hipHeightExtent) - midLegs;
          offset += hipsPosition - Vector3.up * _jointDistance;
          offset += Vector3.forward * 0.05f;

          offset.x /= hips.transform.lossyScale.x;
          offset.y /= hips.transform.lossyScale.y;
          offset.z /= hips.transform.lossyScale.z;

          var boxExtents = Vector3.right * hipWidthExtent + Vector3.up * hipHeightExtent + (Vector3.forward * _limbThickness);
          boxExtents.x /= hips.transform.lossyScale.x;
          boxExtents.y /= hips.transform.lossyScale.y;
          boxExtents.z /= hips.transform.lossyScale.z;

          // add the shape config with transform, collider, body and box shape
          hips.TransformMode = QuantumEntityPrototypeTransformMode.Transform3D;
          hips.PhysicsCollider.IsEnabled = true;
          hips.PhysicsBody.Mass = (HipsMass * _totalMass).ToFP();
          hips.PhysicsBody.IsEnabled = true;
          hips.PhysicsCollider.Shape3D = new Shape3DConfig() {
            ShapeType = Shape3DType.Box,
            PositionOffset = -offset.ToFPVector3(),
            BoxExtents = boxExtents.ToFPVector3()
          };
        }

        // chest
        if (_chest != null && UpdateChest) {
          var chestWidthExtent = (Vector3.Distance(rightArmPosition, leftArmPosition) / 2) - (_limbThickness);
          var chestHeightExtent = (Vector3.Distance(midBody, midShoulders) / 2);

          var chest = AddOrGet<QuantumEntityPrototype>(_chest.gameObject);
          var view = AddOrGet<QuantumRagdollLimbView>(_chest.gameObject);
          view.SetRoot(_root.GetComponent<QuantumRagdoll>(), 1);

          // add the shape config with transform, collider, body and box shape
          var offset = -Vector3.up * 1.1f * -chestHeightExtent / _chest.transform.lossyScale.y;
          //var offset = Vector3.zero;

          var boxExtents = Vector3.right * chestWidthExtent + Vector3.up * chestHeightExtent + (Vector3.forward * _limbThickness);
          boxExtents.x /= chest.transform.lossyScale.x;
          boxExtents.y /= chest.transform.lossyScale.y;
          boxExtents.z /= chest.transform.lossyScale.z;

          chest.TransformMode = QuantumEntityPrototypeTransformMode.Transform3D;
          chest.PhysicsCollider.IsEnabled = true;
          chest.PhysicsBody.IsEnabled = true;
          chest.PhysicsBody.Mass = (ChestMass * _totalMass).ToFP();
          chest.PhysicsCollider.Shape3D = new Shape3DConfig() {
            ShapeType = Shape3DType.Box,
            PositionOffset = offset.ToFPVector3(),
            BoxExtents = boxExtents.ToFPVector3()
          };

          // connects the chest to the hips since the hips is the center of mass
          var anchorOffset =  InverseTransformPoint(_root, _chest.position) - midBody;
          var connectedOffset = midBody - InverseTransformPoint(_root, _hip.position);

          if (_hip != null) {
            var joint = AddOrGet<QPrototypePhysicsJoints3D>(_chest.gameObject);
            joint.Prototype.JointConfigs = new Prototypes.Unity.Joint3DConfig[]{
              new (){
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
              }
            };
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
          var radiusScaleFactor = Math.Min(_head.transform.lossyScale.x, _head.transform.lossyScale.z);
          head.PhysicsCollider.Shape3D = new Shape3DConfig() {
            ShapeType = Shape3DType.Sphere,
            SphereRadius = (_headSize / radiusScaleFactor).ToFP(),
            PositionOffset = FPVector3.Up * (_headDistance / _head.transform.lossyScale.y).ToFP(),
          };
          head.PhysicsBody.IsEnabled = true;
          head.PhysicsBody.Mass = (_totalMass * HeadMass).ToFP();

          // link the head into the chest
          if (_chest != null) {
            var headAnchor = Vector3.down * head.PhysicsCollider.Shape3D.SphereRadius.AsFloat;
            var chestAnchor = headPosition - chestPosition + headAnchor;
            var joint = AddOrGet<QPrototypePhysicsJoints3D>(_head.gameObject);
            joint.Prototype.JointConfigs = new Prototypes.Unity.Joint3DConfig[]{
              new (){
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
              }
            };
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
            lowAngle: -70,
            upperAngle: 10,
            swing1: 50,
            swing2: 3,
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
            lowAngle: -70,
            upperAngle: 10,
            swing1: 50,
            swing2: 3,
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
            lowAngle: -20,
            upperAngle: 70,
            swing1: 30,
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
            lowAngle: -20,
            upperAngle: 70,
            swing1: 30,
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
      LimbUpdates.Add(new RagdollLimbUpdate() {
        LimbTransform = transform,
        Position = position,
        Rotation = rotation,
        Depth = depth
      });
    }

    void LateUpdate() {
      LimbUpdates.Sort();
      foreach (var update in LimbUpdates) {
        update.LimbTransform.rotation = update.Rotation;
        update.LimbTransform.position = update.Position;
      }
      LimbUpdates.Clear();
    }

    public void BuildRagdoll() {
      Settings.BuildRagDoll(_partsToUpdate);
    }

    void OnValidate() {
      if(_autoBakeMode == BakeMode.OnValidate) {
        Settings.BuildRagDoll(_partsToUpdate);
      }
    }

    void Update() {
      if (Application.isPlaying == false && _autoBakeMode == BakeMode.Immediate) {
        Settings.BuildRagDoll(_partsToUpdate);
      }
    }
  }
}
