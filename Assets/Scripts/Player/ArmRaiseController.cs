using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class ArmRaiseController : MonoBehaviour
{
    [Header("Arm Bones")]
    [Tooltip("비워두면 아래 이름으로 자식에서 찾습니다.")]
    [SerializeField] private Transform leftUpperArm;
    [SerializeField] private Transform rightUpperArm;
    [SerializeField] private string leftUpperArmName = "Arm1.L";
    [SerializeField] private string rightUpperArmName = "Arm1.R";

    [Header("Raise")]
    [Tooltip("캐릭터 기준 팔을 들 방향. x = 바깥쪽, y = 위, z = 앞")]
    [SerializeField] private Vector3 raisedDirection = new Vector3(0f, 1f, 0.35f);
    [SerializeField, Min(0f)] private float raiseSpring = 40f;
    [SerializeField, Min(0f)] private float raiseDamper = 2f;

    [Tooltip("팔 뼈에 ConfigurableJoint가 없을 때 Transform을 직접 돌리는 속도")]
    [SerializeField, Min(0f)] private float transformRaiseSpeed = 8f;

    private ArmRig leftArm;
    private ArmRig rightArm;

    private void Awake()
    {
        leftArm = CreateRig(leftUpperArm, leftUpperArmName, -1f);
        rightArm = CreateRig(rightUpperArm, rightUpperArmName, 1f);
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;

        bool leftPressed = mouse != null && mouse.leftButton.isPressed;
        bool rightPressed = mouse != null && mouse.rightButton.isPressed;

        if (leftArm != null)
        {
            leftArm.IsRaised = leftPressed;
        }

        if (rightArm != null)
        {
            rightArm.IsRaised = rightPressed;
        }
    }

    private void FixedUpdate()
    {
        leftArm?.UpdateJoint(this);
        rightArm?.UpdateJoint(this);
    }

    private void LateUpdate()
    {
        leftArm?.UpdateTransform(this, Time.deltaTime);
        rightArm?.UpdateTransform(this, Time.deltaTime);
    }

    private void OnDisable()
    {
        if (leftArm != null)
        {
            leftArm.IsRaised = false;
            leftArm.RestoreJoint();
        }

        if (rightArm != null)
        {
            rightArm.IsRaised = false;
            rightArm.RestoreJoint();
        }
    }

    private ArmRig CreateRig(Transform bone, string boneName, float side)
    {
        if (bone == null)
        {
            bone = FindChildRecursive(transform, boneName);
        }

        if (bone == null)
        {
            Debug.LogWarning(
                $"[ArmRaiseController] '{boneName}' 뼈를 찾지 못해 해당 팔은 동작하지 않습니다.",
                this
            );
            return null;
        }

        return new ArmRig(bone, transform, side);
    }

    private Quaternion GetRaisedRootRotation(ArmRig arm)
    {
        Vector3 direction = new Vector3(
            raisedDirection.x * arm.Side,
            raisedDirection.y,
            raisedDirection.z
        );

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.up;
        }

        return Quaternion.FromToRotation(arm.RestDirectionInRoot, direction.normalized) *
               arm.RestRotationInRoot;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform found = FindChildRecursive(child, childName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private sealed class ArmRig
    {
        public readonly float Side;
        public readonly Vector3 RestDirectionInRoot;
        public readonly Quaternion RestRotationInRoot;

        public bool IsRaised;

        private readonly Transform bone;
        private readonly Transform root;
        private readonly ConfigurableJoint joint;
        private readonly Rigidbody connectedBody;

        private readonly Quaternion startRotationInConnected;
        private readonly Quaternion jointSpace;

        private readonly JointDrive originalAngularXDrive;
        private readonly JointDrive originalAngularYZDrive;
        private readonly JointDrive originalSlerpDrive;
        private readonly Quaternion originalTargetRotation;

        private bool jointDriven;
        private float transformBlend;

        public ArmRig(Transform bone, Transform root, float side)
        {
            this.bone = bone;
            this.root = root;
            Side = side;

            Vector3 localBoneAxis =
                bone.childCount > 0
                    ? bone.InverseTransformDirection(
                        bone.GetChild(0).position - bone.position
                    ).normalized
                    : Vector3.up;

            if (localBoneAxis.sqrMagnitude < 0.0001f)
            {
                localBoneAxis = Vector3.up;
            }

            Quaternion inverseRoot = Quaternion.Inverse(root.rotation);
            RestDirectionInRoot = inverseRoot * bone.TransformDirection(localBoneAxis);
            RestRotationInRoot = inverseRoot * bone.rotation;

            joint = bone.GetComponent<ConfigurableJoint>();

            if (joint == null)
            {
                return;
            }

            connectedBody = joint.connectedBody;
            startRotationInConnected = GetRotationInConnected(bone.rotation);

            Vector3 right = joint.axis.normalized;
            Vector3 forward = Vector3.Cross(joint.axis, joint.secondaryAxis).normalized;
            Vector3 up = Vector3.Cross(forward, right).normalized;
            jointSpace = Quaternion.LookRotation(forward, up);

            originalAngularXDrive = joint.angularXDrive;
            originalAngularYZDrive = joint.angularYZDrive;
            originalSlerpDrive = joint.slerpDrive;
            originalTargetRotation = joint.targetRotation;
        }

        public void UpdateJoint(ArmRaiseController owner)
        {
            if (joint == null)
            {
                return;
            }

            if (!IsRaised)
            {
                RestoreJoint();
                return;
            }

            if (!jointDriven)
            {
                ApplyRaiseDrives(owner.raiseSpring, owner.raiseDamper);
                jointDriven = true;
            }

            Quaternion desiredWorld = root.rotation * owner.GetRaisedRootRotation(this);
            Quaternion desiredInConnected = GetRotationInConnected(desiredWorld);

            joint.targetRotation =
                Quaternion.Inverse(jointSpace) *
                Quaternion.Inverse(desiredInConnected) *
                startRotationInConnected *
                jointSpace;
        }

        public void UpdateTransform(ArmRaiseController owner, float deltaTime)
        {
            if (joint != null)
            {
                return;
            }

            transformBlend = Mathf.MoveTowards(
                transformBlend,
                IsRaised ? 1f : 0f,
                owner.transformRaiseSpeed * deltaTime
            );

            if (transformBlend <= 0f)
            {
                return;
            }

            Quaternion desiredWorld = root.rotation * owner.GetRaisedRootRotation(this);
            bone.rotation = Quaternion.Slerp(bone.rotation, desiredWorld, transformBlend);
        }

        public void RestoreJoint()
        {
            if (joint == null || !jointDriven)
            {
                return;
            }

            joint.angularXDrive = originalAngularXDrive;
            joint.angularYZDrive = originalAngularYZDrive;
            joint.slerpDrive = originalSlerpDrive;
            joint.targetRotation = originalTargetRotation;
            jointDriven = false;
        }

        private void ApplyRaiseDrives(float spring, float damper)
        {
            joint.angularXDrive = WithSpring(originalAngularXDrive, spring, damper);
            joint.angularYZDrive = WithSpring(originalAngularYZDrive, spring, damper);
            joint.slerpDrive = WithSpring(originalSlerpDrive, spring, damper);
        }

        private Quaternion GetRotationInConnected(Quaternion worldRotation)
        {
            return connectedBody != null
                ? Quaternion.Inverse(connectedBody.rotation) * worldRotation
                : worldRotation;
        }

        private static JointDrive WithSpring(JointDrive drive, float spring, float damper)
        {
            drive.positionSpring = spring;
            drive.positionDamper = damper;
            return drive;
        }
    }
}
