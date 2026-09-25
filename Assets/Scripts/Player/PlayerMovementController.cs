using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class PlayerMovementController : MonoBehaviour
{
    private const string MoveActionName = "Player/Move";
    private const string JumpActionName = "Player/Jump";
    private const float JumpBufferDuration = 0.15f;
    private const float CoyoteTimeDuration = 0.12f;
    private const float AnimationTransitionDuration = 0.1f;

    private static readonly int IdleState =
        Animator.StringToHash("Base Layer.Idle");

    private static readonly int PlayerAnimationState =
        Animator.StringToHash("Base Layer.PlayerAnimation");

    [Header("References")]
    [SerializeField] private Transform movementCamera;
    [SerializeField] private Transform cameraTarget;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 3f;
    [SerializeField, Min(0f)] private float rotationSpeed = 720f;
    [SerializeField, Min(0f)] private float jumpVelocity = 5f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField, Min(0f)] private float groundCheckDistance = 0.12f;
    [SerializeField, Min(0.01f)] private float groundCheckRadius = 0.08f;

    private readonly RaycastHit[] groundHits = new RaycastHit[32];

    private Rigidbody body;
    private Animator animator;
    private Collider[] bodyColliders;

    private InputAction moveAction;
    private InputAction jumpAction;

    private Quaternion cameraTargetWorldRotation;

    private float lastGroundedTime = float.NegativeInfinity;
    private float lastJumpPressedTime = float.NegativeInfinity;

    private bool enabledMoveAction;
    private bool enabledJumpAction;

    private bool animationStateInitialized;
    private bool isPlayingMovementAnimation;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        bodyColliders = GetComponentsInChildren<Collider>();

        if (movementCamera == null && Camera.main != null)
        {
            movementCamera = Camera.main.transform;
        }

        if (cameraTarget != null)
        {
            cameraTargetWorldRotation = cameraTarget.rotation;
        }
    }

    private void OnEnable()
    {
        moveAction = InputSystem.actions?.FindAction(MoveActionName);
        jumpAction = InputSystem.actions?.FindAction(JumpActionName);

        if (moveAction != null && !moveAction.enabled)
        {
            moveAction.Enable();
            enabledMoveAction = true;
        }

        if (jumpAction != null && !jumpAction.enabled)
        {
            jumpAction.Enable();
            enabledJumpAction = true;
        }
    }

    private void Update()
    {
        Vector2 moveInput =
            moveAction?.ReadValue<Vector2>() ?? Vector2.zero;

        UpdateMovementAnimation(
            moveInput.sqrMagnitude > 0.0001f
        );

        if (jumpAction != null && jumpAction.WasPressedThisFrame())
        {
            lastJumpPressedTime = Time.time;
        }
    }

    private void FixedUpdate()
    {
        bool isGrounded = CheckGrounded();

        if (isGrounded)
        {
            lastGroundedTime = Time.time;
        }

        Vector2 input =
            moveAction?.ReadValue<Vector2>() ?? Vector2.zero;

        Vector3 moveDirection =
            GetCameraRelativeDirection(input);

        // 가속 없이 항상 일정한 속도로 이동
        Vector3 currentVelocity = body.linearVelocity;

        Vector3 horizontalVelocity =
            moveDirection * moveSpeed;

        body.linearVelocity = new Vector3(
            horizontalVelocity.x,
            currentVelocity.y,
            horizontalVelocity.z
        );

        // 이동 방향을 바라봄
        if (moveDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(
                    moveDirection,
                    Vector3.up
                );

            Quaternion nextRotation =
                Quaternion.RotateTowards(
                    body.rotation,
                    targetRotation,
                    rotationSpeed * Time.fixedDeltaTime
                );

            body.MoveRotation(nextRotation);
        }

        // 점프
        bool hasBufferedJump =
            Time.time - lastJumpPressedTime <= JumpBufferDuration;

        bool canUseCoyoteTime =
            Time.time - lastGroundedTime <= CoyoteTimeDuration;

        if (hasBufferedJump && canUseCoyoteTime)
        {
            Vector3 jumpVelocityVector =
                body.linearVelocity;

            jumpVelocityVector.y = jumpVelocity;

            body.linearVelocity =
                jumpVelocityVector;

            lastJumpPressedTime =
                float.NegativeInfinity;

            lastGroundedTime =
                float.NegativeInfinity;
        }
    }

    private void LateUpdate()
    {
        if (cameraTarget != null)
        {
            cameraTarget.rotation =
                cameraTargetWorldRotation;
        }
    }

    private void OnDisable()
    {
        UpdateMovementAnimation(false);

        animationStateInitialized = false;

        if (enabledMoveAction)
        {
            moveAction?.Disable();
            enabledMoveAction = false;
        }

        if (enabledJumpAction)
        {
            jumpAction?.Disable();
            enabledJumpAction = false;
        }
    }

    private void UpdateMovementAnimation(bool isMoving)
    {
        if (animator == null ||
            (animationStateInitialized &&
             isPlayingMovementAnimation == isMoving))
        {
            return;
        }

        int targetState =
            isMoving
                ? PlayerAnimationState
                : IdleState;

        if (!animator.HasState(0, targetState))
        {
            return;
        }

        animator.CrossFade(
            targetState,
            AnimationTransitionDuration,
            0
        );

        isPlayingMovementAnimation = isMoving;
        animationStateInitialized = true;
    }

    private Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        if (input.sqrMagnitude < 0.0001f)
        {
            return Vector3.zero;
        }

        Transform reference =
            movementCamera != null
                ? movementCamera
                : transform;

        Vector3 forward =
            Vector3.ProjectOnPlane(
                reference.forward,
                Vector3.up
            ).normalized;

        Vector3 right =
            Vector3.ProjectOnPlane(
                reference.right,
                Vector3.up
            ).normalized;

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward =
                Vector3.ProjectOnPlane(
                    transform.forward,
                    Vector3.up
                ).normalized;
        }

        return Vector3.ClampMagnitude(
            forward * input.y +
            right * input.x,
            1f
        );
    }

    private bool CheckGrounded()
    {
        if (!TryGetBodyBounds(out Bounds bounds))
        {
            return false;
        }

        float castRadius =
            Mathf.Min(
                groundCheckRadius,
                bounds.extents.x,
                bounds.extents.z
            );

        castRadius =
            Mathf.Max(castRadius, 0.01f);

        float castDistance =
            bounds.extents.y +
            groundCheckDistance;

        int hitCount =
            Physics.SphereCastNonAlloc(
                bounds.center,
                castRadius,
                Vector3.down,
                groundHits,
                castDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore
            );

        for (int i = 0; i < hitCount; i++)
        {
            Transform hitTransform =
                groundHits[i].collider.transform;

            if (!hitTransform.IsChildOf(transform) &&
                groundHits[i].normal.y >= 0.5f)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetBodyBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        foreach (Collider bodyCollider in bodyColliders)
        {
            if (bodyCollider == null ||
                !bodyCollider.enabled ||
                bodyCollider.isTrigger)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = bodyCollider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(
                    bodyCollider.bounds
                );
            }
        }

        return hasBounds;
    }
}