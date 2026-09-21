using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class RagdollBalanceController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform modelRoot;
    [SerializeField] Rigidbody torsoRigidbody;

    [Header("Pose Stabilization")]
    [SerializeField] float poseSpring = 1200f;
    [SerializeField] float poseDamper = 90f;
    [SerializeField] float legPoseSpring = 2200f;
    [SerializeField] float kneePoseSpring = 2800f;
    [SerializeField] float maxTorque = 120f;
    [Range(0f, 1f)]
    [SerializeField] float footPoseStrength = 0.85f;

    [Header("Upright Balance")]
    [SerializeField] float uprightTorque = 200f;
    [SerializeField] float uprightDamping = 25f;

    [Header("Physics Setup")]
    [SerializeField] float rigidbodyAngularDrag = 1f;
    [SerializeField] bool configureRigidbodiesOnAwake = true;

    struct Muscle
    {
        public Rigidbody Rigidbody;
        public Transform PoseParent;
        public Quaternion TargetLocalRotation;
        public float RotationSpring;
        public float RotationStrength;
    }

    Muscle[] _muscles;
    Rigidbody[] _allRigidbodies;
    CharacterJoint[] _joints;
    bool _isReady;

    void Awake()
    {
        if (modelRoot == null && transform.childCount > 0)
            modelRoot = transform.GetChild(0);

        if (modelRoot == null)
            return;

        _allRigidbodies = modelRoot.GetComponentsInChildren<Rigidbody>();
        _joints = modelRoot.GetComponentsInChildren<CharacterJoint>();
        SetKinematic(true);
        ConfigureRigidbodies();
    }

    void Start()
    {
        StartCoroutine(InitializeStandingPose());
    }

    IEnumerator InitializeStandingPose()
    {
        yield return new WaitForFixedUpdate();
        Physics.SyncTransforms();

        CacheMuscles();
        ConfigureJoints();
        SetKinematic(false);
        _isReady = true;
    }

    void CacheMuscles()
    {
        if (modelRoot == null)
            return;

        _muscles = new Muscle[_allRigidbodies.Length];

        for (int i = 0; i < _allRigidbodies.Length; i++)
        {
            Rigidbody rb = _allRigidbodies[i];
            CharacterJoint joint = rb.GetComponent<CharacterJoint>();
            Transform poseParent = joint != null && joint.connectedBody != null
                ? joint.connectedBody.transform
                : rb.transform.parent;
            string boneName = rb.name.ToLowerInvariant();

            _muscles[i] = new Muscle
            {
                Rigidbody = rb,
                PoseParent = poseParent,
                TargetLocalRotation = poseParent != null
                    ? Quaternion.Inverse(poseParent.rotation) * rb.rotation
                    : rb.rotation,
                RotationSpring = GetRotationSpring(boneName),
                RotationStrength = GetRotationStrength(boneName)
            };

            if (torsoRigidbody == null && joint == null)
                torsoRigidbody = rb;
        }
    }

    void ConfigureRigidbodies()
    {
        if (!configureRigidbodiesOnAwake || _allRigidbodies == null)
            return;

        for (int i = 0; i < _allRigidbodies.Length; i++)
        {
            Rigidbody rb = _allRigidbodies[i];
            rb.angularDamping = rigidbodyAngularDrag;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }
    }

    void ConfigureJoints()
    {
        if (_joints == null)
            return;

        for (int i = 0; i < _joints.Length; i++)
        {
            CharacterJoint joint = _joints[i];
            joint.enableProjection = true;
            joint.projectionDistance = 0.05f;
            joint.projectionAngle = 3f;
            joint.enablePreprocessing = true;
        }
    }

    void FixedUpdate()
    {
        if (!_isReady || _muscles == null)
            return;

        StabilizePose();
        StabilizeUpright();
    }

    void StabilizePose()
    {
        for (int i = 0; i < _muscles.Length; i++)
        {
            Muscle muscle = _muscles[i];
            Rigidbody rb = muscle.Rigidbody;

            if (rb == null || muscle.PoseParent == null)
                continue;

            Quaternion targetRotation = muscle.PoseParent.rotation * muscle.TargetLocalRotation;
            Quaternion deltaRotation = targetRotation * Quaternion.Inverse(rb.rotation);
            deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);

            if (angle > 180f)
                angle -= 360f;

            if (Mathf.Abs(angle) < 0.01f)
                continue;

            float strength = muscle.RotationStrength;
            float spring = muscle.RotationSpring;
            Vector3 torque = axis.normalized * (angle * Mathf.Deg2Rad * spring * strength);
            torque -= rb.angularVelocity * (poseDamper * strength);
            torque = Vector3.ClampMagnitude(torque, maxTorque);
            rb.AddTorque(torque, ForceMode.Acceleration);
        }
    }

    void StabilizeUpright()
    {
        if (torsoRigidbody == null)
            return;

        Vector3 up = torsoRigidbody.transform.up;
        Vector3 tiltAxis = Vector3.Cross(up, Vector3.up);

        if (tiltAxis.sqrMagnitude < 0.0001f)
            return;

        float tiltAngle = Vector3.Angle(up, Vector3.up);
        Vector3 correctiveTorque = tiltAxis.normalized * (tiltAngle * Mathf.Deg2Rad * uprightTorque);
        correctiveTorque -= torsoRigidbody.angularVelocity * uprightDamping;
        correctiveTorque = Vector3.ClampMagnitude(correctiveTorque, maxTorque);
        torsoRigidbody.AddTorque(correctiveTorque, ForceMode.Acceleration);
    }

    void SetKinematic(bool isKinematic)
    {
        if (_allRigidbodies == null)
            return;

        for (int i = 0; i < _allRigidbodies.Length; i++)
            _allRigidbodies[i].isKinematic = isKinematic;
    }

    float GetRotationSpring(string boneName)
    {
        if (IsKneeBone(boneName))
            return kneePoseSpring;

        if (IsLegBone(boneName))
            return legPoseSpring;

        if (IsStomachBone(boneName))
            return poseSpring * 1.3f;

        return poseSpring;
    }

    float GetRotationStrength(string boneName)
    {
        if (IsFootBone(boneName))
            return footPoseStrength;

        return 1f;
    }

    static bool IsKneeBone(string boneName)
    {
        return boneName.Contains("leg1") || boneName.Contains("leg2");
    }

    static bool IsLegBone(string boneName)
    {
        return boneName.Contains("leg");
    }

    static bool IsStomachBone(string boneName)
    {
        return boneName.Contains("stomach");
    }

    static bool IsFootBone(string boneName)
    {
        return boneName.Contains("foot");
    }
}
