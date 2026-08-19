using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Player")]
        [Tooltip("Move speed of the character in m/s")]
        public float MoveSpeed = 4.0f;

        [Tooltip("Sprint speed of the character in m/s")]
        public float SprintSpeed = 6.0f;

        [Tooltip("Rotation speed of the character")]
        public float RotationSpeed = 1.0f;

        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;

        [Space(10)]
        [Tooltip("The height the player can jump")]
        public float JumpHeight = 1.2f;

        [Tooltip("The character uses its own gravity value")]
        public float Gravity = -15.0f;

        [Space(10)]
        [Tooltip("Time required to pass before being able to jump again")]
        public float JumpTimeout = 0.1f;

        [Tooltip("Time required to pass before entering the fall state")]
        public float FallTimeout = 0.15f;


        // ============================================================
        // PLAYER GROUNDED
        // ============================================================

        [Header("Player Grounded")]

        [Tooltip("If the character is grounded or not")]
        public bool Grounded = true;

        [Tooltip("Useful for rough ground")]
        public float GroundedOffset = -0.14f;

        [Tooltip("The radius of the grounded check")]
        public float GroundedRadius = 0.5f;

        [Tooltip("What layers the character uses as ground")]
        public LayerMask GroundLayers;


        // ============================================================
        // LEDGE CLIMBING
        // ============================================================

        [Header("Ledge Climbing")]

        [Tooltip("Maximum vertical height the player can climb")]
        public float MaxClimbHeight = 1.5f;

        [Tooltip("How far in front of the player we look for a wall")]
        public float WallCheckDistance = 0.8f;

        [Tooltip("How far above the detected wall we search for the top")]
        public float LedgeCheckHeight = 2.0f;

        [Tooltip("How long the climb takes")]
        public float ClimbDuration = 0.9f;

        [Tooltip("How far upward the player arcs during the climb")]
        public float ClimbArcHeight = 0.20f;

        [Tooltip("How far onto the ledge the player is placed")]
        public float ClimbForwardOffset = 0.35f;

        [Tooltip("Layers that can be climbed")]
        public LayerMask ClimbLayers;

        [Tooltip("Minimum angle required for something to count as a wall")]
        [Range(0f, 90f)]
        public float MinWallAngle = 70f;

        [Tooltip("Maximum angle for the top surface")]
        [Range(0f, 90f)]
        public float MaxLedgeAngle = 45f;

        [Tooltip("How long a jump press is remembered for climbing")]
        public float JumpBufferTime = 0.1f;

        // ============================================================
        // CINEMACHINE
        // ============================================================

        [Header("Cinemachine")]

        [Tooltip("The follow target set in the Cinemachine Virtual Camera")]
        public GameObject CinemachineCameraTarget;

        [Tooltip("How far in degrees can you move the camera up")]
        public float TopClamp = 90.0f;

        [Tooltip("How far in degrees can you move the camera down")]
        public float BottomClamp = -90.0f;


        // ============================================================
        // CINEMACHINE VARIABLES
        // ============================================================

        private float _cinemachineTargetPitch;


        // ============================================================
        // PLAYER VARIABLES
        // ============================================================

        private float _speed;

        private float _rotationVelocity;

        private float _verticalVelocity;

        private float _terminalVelocity = 20.0f;


        // ============================================================
        // TIMEOUT VARIABLES
        // ============================================================

        private float _jumpTimeoutDelta;

        private float _fallTimeoutDelta;


        // ============================================================
        // JUMP BUFFER
        // ============================================================

        private bool _jumpBuffered;

        private float _jumpBufferTimer;


        // ============================================================
        // CLIMBING VARIABLES
        // ============================================================

        private bool _isClimbing;

        private Vector3 _climbStartPosition;

        private Vector3 _climbTargetPosition;

        private float _climbTimer;


        // ============================================================
        // COMPONENTS
        // ============================================================

#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
#endif

        private CharacterController _controller;

        private StarterAssetsInputs _input;

        private GameObject _mainCamera;

        private const float _threshold = 0.01f;


        // ============================================================
        // INPUT DEVICE
        // ============================================================

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return _playerInput.currentControlScheme == "KeyboardMouse";
#else
                return false;
#endif
            }
        }


        // ============================================================
        // AWAKE
        // ============================================================

        private void Awake()
        {
            if (_mainCamera == null)
            {
                _mainCamera =
                    GameObject.FindGameObjectWithTag("MainCamera");
            }
        }


        // ============================================================
        // START
        // ============================================================

        private void Start()
        {
            _controller =
                GetComponent<CharacterController>();

            _input =
                GetComponent<StarterAssetsInputs>();

#if ENABLE_INPUT_SYSTEM

            _playerInput =
                GetComponent<PlayerInput>();

#else

            Debug.LogError(
                "Starter Assets package is missing dependencies. " +
                "Please use Tools/Starter Assets/Reinstall Dependencies to fix it"
            );

#endif

            _jumpTimeoutDelta =
                JumpTimeout;

            _fallTimeoutDelta =
                FallTimeout;
        }


        // ============================================================
        // UPDATE
        // ============================================================

        private void Update()
        {
            // --------------------------------------------------------
            // BUFFER JUMP INPUT
            // --------------------------------------------------------

            if (_input.jump)
            {
                _jumpBuffered = true;

                _jumpBufferTimer =
                    JumpBufferTime;
            }
            else if (_jumpBufferTimer > 0f)
            {
                _jumpBufferTimer -=
                    Time.deltaTime;

                if (_jumpBufferTimer <= 0f)
                {
                    _jumpBufferTimer = 0f;

                    _jumpBuffered = false;
                }
            }


            // --------------------------------------------------------
            // GROUNDED CHECK
            // --------------------------------------------------------

            GroundedCheck();


            // --------------------------------------------------------
            // CLIMBING
            // --------------------------------------------------------

            if (_isClimbing)
            {
                UpdateClimb();

                return;
            }


            // --------------------------------------------------------
            // TRY LEDGE CLIMB
            // --------------------------------------------------------

            if (!Grounded &&
                _jumpBuffered)
            {
                if (TryStartClimb())
                {
                    _jumpBuffered = false;

                    _jumpBufferTimer = 0f;

                    return;
                }
            }


            // --------------------------------------------------------
            // NORMAL MOVEMENT
            // --------------------------------------------------------

            JumpAndGravity();

            Move();
        }


        // ============================================================
        // LATE UPDATE
        // ============================================================

        private void LateUpdate()
        {
            CameraRotation();
        }


        // ============================================================
        // GROUNDED CHECK
        // ============================================================

        private void GroundedCheck()
        {
            Vector3 spherePosition =
                new Vector3(
                    transform.position.x,
                    transform.position.y -
                    GroundedOffset,
                    transform.position.z
                );

            Grounded =
                Physics.CheckSphere(
                    spherePosition,
                    GroundedRadius,
                    GroundLayers,
                    QueryTriggerInteraction.Ignore
                );
        }


        // ============================================================
        // CAMERA ROTATION
        // ============================================================

        private void CameraRotation()
        {
            if (_input.look.sqrMagnitude > 0.000001f)
            {
                float deltaTimeMultiplier =
                    IsCurrentDeviceMouse
                        ? 1.0f
                        : Time.deltaTime;

                _cinemachineTargetPitch +=
                    _input.look.y *
                    RotationSpeed *
                    deltaTimeMultiplier;

                _rotationVelocity =
                    _input.look.x *
                    RotationSpeed *
                    deltaTimeMultiplier;

                _cinemachineTargetPitch =
                    ClampAngle(
                        _cinemachineTargetPitch,
                        BottomClamp,
                        TopClamp
                    );

                CinemachineCameraTarget
                    .transform
                    .localRotation =
                    Quaternion.Euler(
                        _cinemachineTargetPitch,
                        0.0f,
                        0.0f
                    );

                transform.Rotate(
                    Vector3.up *
                    _rotationVelocity
                );
            }
        }


        // ============================================================
        // MOVEMENT
        // ============================================================

        private void Move()
        {
            float targetSpeed =
                _input.sprint
                    ? SprintSpeed
                    : MoveSpeed;

            if (_input.move == Vector2.zero)
            {
                targetSpeed = 0.0f;
            }

            float currentHorizontalSpeed =
                new Vector3(
                    _controller.velocity.x,
                    0.0f,
                    _controller.velocity.z
                ).magnitude;

            float speedOffset = 0.1f;

            float inputMagnitude =
                _input.analogMovement
                    ? _input.move.magnitude
                    : 1f;

            if (currentHorizontalSpeed <
                    targetSpeed - speedOffset ||
                currentHorizontalSpeed >
                    targetSpeed + speedOffset)
            {
                _speed =
                    Mathf.Lerp(
                        currentHorizontalSpeed,
                        targetSpeed *
                        inputMagnitude,
                        Time.deltaTime *
                        SpeedChangeRate
                    );

                _speed =
                    Mathf.Round(
                        _speed *
                        1000f
                    ) / 1000f;
            }
            else
            {
                _speed =
                    targetSpeed;
            }


            Vector3 inputDirection =
                new Vector3(
                    _input.move.x,
                    0.0f,
                    _input.move.y
                ).normalized;


            if (_input.move != Vector2.zero)
            {
                inputDirection =
                    transform.right *
                    _input.move.x +
                    transform.forward *
                    _input.move.y;
            }


            _controller.Move(
                inputDirection.normalized *
                (_speed *
                Time.deltaTime)

                +

                new Vector3(
                    0.0f,
                    _verticalVelocity,
                    0.0f
                ) *
                Time.deltaTime
            );
        }


        // ============================================================
        // JUMP AND GRAVITY
        // ============================================================

        private void JumpAndGravity()
        {
            if (Grounded)
            {
                _fallTimeoutDelta =
                    FallTimeout;


                // Keep player slightly grounded.

                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity =
                        -2f;
                }


                // Normal jump.

                if (_input.jump &&
                    _jumpTimeoutDelta <= 0.0f)
                {
                    _verticalVelocity =
                        Mathf.Sqrt(
                            JumpHeight *
                            -2f *
                            Gravity
                        );


                    // Consume jump buffer.

                    _jumpBuffered = false;

                    _jumpBufferTimer = 0f;
                }


                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -=
                        Time.deltaTime;
                }
            }
            else
            {
                _jumpTimeoutDelta =
                    JumpTimeout;


                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -=
                        Time.deltaTime;
                }


                // Starter Assets clears jump while airborne.
                // Our buffer keeps the input for climbing.

                _input.jump = false;
            }


            // Apply gravity.

            if (_verticalVelocity >
                -_terminalVelocity)
            {
                _verticalVelocity +=
                    Gravity *
                    Time.deltaTime;


                if (_verticalVelocity <
                    -_terminalVelocity)
                {
                    _verticalVelocity =
                        -_terminalVelocity;
                }
            }
        }


        // ============================================================
        // LEDGE CLIMBING
        // ============================================================

        private bool TryStartClimb()
        {
            float controllerHeight =
                _controller.height;

            float controllerRadius =
                _controller.radius;


            // --------------------------------------------------------
            // WALL CHECK
            // --------------------------------------------------------

            Vector3 wallOrigin =
                transform.position +
                Vector3.up *
                (controllerHeight *
                0.55f);

            Vector3 forward =
                transform.forward;


            if (!Physics.Raycast(
                    wallOrigin,
                    forward,
                    out RaycastHit wallHit,
                    WallCheckDistance,
                    ClimbLayers,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }


            // --------------------------------------------------------
            // WALL ANGLE
            // --------------------------------------------------------

            float wallAngle =
                Vector3.Angle(
                    wallHit.normal,
                    Vector3.up
                );


            if (wallAngle <
                MinWallAngle)
            {
                return false;
            }


            // --------------------------------------------------------
            // FIND TOP OF LEDGE
            // --------------------------------------------------------

            Vector3 topSearchPosition =
                wallHit.point
                - wallHit.normal *
                0.15f
                + Vector3.up *
                LedgeCheckHeight;


            if (!Physics.Raycast(
                    topSearchPosition,
                    Vector3.down,
                    out RaycastHit ledgeHit,
                    LedgeCheckHeight +
                    0.5f,
                    ClimbLayers,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }


            // --------------------------------------------------------
            // LEDGE ANGLE
            // --------------------------------------------------------

            float ledgeAngle =
                Vector3.Angle(
                    ledgeHit.normal,
                    Vector3.up
                );


            if (ledgeAngle >
                MaxLedgeAngle)
            {
                return false;
            }


            // --------------------------------------------------------
            // LEDGE HEIGHT
            // --------------------------------------------------------

            float ledgeHeight =
                ledgeHit.point.y -
                transform.position.y;


            if (ledgeHeight <= 0f ||
                ledgeHeight >
                MaxClimbHeight)
            {
                return false;
            }


            // --------------------------------------------------------
            // TARGET POSITION
            // --------------------------------------------------------

            Vector3 targetPosition =
                ledgeHit.point
                + wallHit.normal *
                ClimbForwardOffset;


            // CharacterController position is its center.

            targetPosition.y +=
                controllerHeight *
                0.5f - 0.05f;


            // --------------------------------------------------------
            // CHECK PLAYER FIT
            // --------------------------------------------------------

            if (!CanFitAtPosition(
                    targetPosition,
                    controllerRadius,
                    controllerHeight))
            {
                return false;
            }


            // --------------------------------------------------------
            // START CLIMB
            // --------------------------------------------------------

            _isClimbing = true;


            _climbStartPosition =
                transform.position;


            _climbTargetPosition =
                targetPosition;


            _climbTimer = 0f;


            // Stop falling while climbing.

            _verticalVelocity = 0f;


            // Disable CharacterController once.

            _controller.enabled = false;


            return true;
        }


        // ============================================================
        // CHECK PLAYER FIT
        // ============================================================

        private bool CanFitAtPosition(
            Vector3 position,
            float radius,
            float height)
        {
            Vector3 bottom =
                position +
                Vector3.down *
                (height * 0.5f -
                radius);


            Vector3 top =
                position +
                Vector3.up *
                (height * 0.5f -
                radius);


            return !Physics.CheckCapsule(
                bottom,
                top,
                radius * 0.95f,
                ClimbLayers,
                QueryTriggerInteraction.Ignore
            );
        }


        // ============================================================
        // UPDATE CLIMB
        // ============================================================

        private void UpdateClimb()
        {
            _climbTimer +=
                Time.deltaTime;


            float t =
                Mathf.Clamp01(
                    _climbTimer /
                    ClimbDuration
                );


            // Smooth acceleration and deceleration.

            float smoothT =
                t *
                t *
                (3f -
                2f *
                t);


            // Move from the starting position to the ledge.

            Vector3 position =
                Vector3.Lerp(
                    _climbStartPosition,
                    _climbTargetPosition,
                    smoothT
                );


            // --------------------------------------------------------
            // CLIMB ARC
            // --------------------------------------------------------

            float climbArc =
                Mathf.Sin(
                    t *
                    Mathf.PI
                ) *
                ClimbArcHeight;


            position.y +=
                climbArc;


            transform.position =
                position;


            // --------------------------------------------------------
            // FINISHED
            // --------------------------------------------------------

            if (t >= 1f)
            {
                FinishClimb();
            }
        }


        // ============================================================
        // FINISH CLIMB
        // ============================================================

        private void FinishClimb()
        {
            // Make absolutely sure we're at the target.

            transform.position =
                _climbTargetPosition;


            // Re-enable CharacterController.

            _controller.enabled = true;


            _isClimbing = false;


            // Keep player gently grounded.

            


            _speed = 0f;


            // Consume jump input.

            _input.jump = false;


            // Clear jump buffer.

            _jumpBuffered = false;

            _jumpBufferTimer = 0f;
        }


        // ============================================================
        // ANGLE CLAMP
        // ============================================================

        private static float ClampAngle(
            float lfAngle,
            float lfMin,
            float lfMax)
        {
            if (lfAngle < -360f)
            {
                lfAngle += 360f;
            }

            if (lfAngle > 360f)
            {
                lfAngle -= 360f;
            }

            return Mathf.Clamp(
                lfAngle,
                lfMin,
                lfMax
            );
        }


        // ============================================================
        // GIZMOS
        // ============================================================

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen =
                new Color(
                    0.0f,
                    1.0f,
                    0.0f,
                    0.35f
                );


            Color transparentRed =
                new Color(
                    1.0f,
                    0.0f,
                    0.0f,
                    0.35f
                );


            Gizmos.color =
                Grounded
                    ? transparentGreen
                    : transparentRed;


            // --------------------------------------------------------
            // GROUND CHECK
            // --------------------------------------------------------

            Gizmos.DrawSphere(
                new Vector3(
                    transform.position.x,
                    transform.position.y -
                    GroundedOffset,
                    transform.position.z
                ),
                GroundedRadius
            );


            // --------------------------------------------------------
            // WALL DETECTION
            // --------------------------------------------------------

            Gizmos.color =
                Color.blue;


            Vector3 wallOrigin =
                transform.position +
                Vector3.up *
                (_controller != null
                    ? _controller.height *
                      0.55f
                    : 1f);


            Gizmos.DrawRay(
                wallOrigin,
                transform.forward *
                WallCheckDistance
            );


            // --------------------------------------------------------
            // MAX CLIMB HEIGHT
            // --------------------------------------------------------

            Gizmos.color =
                Color.yellow;


            Gizmos.DrawWireCube(
                transform.position +
                Vector3.up *
                MaxClimbHeight,
                new Vector3(
                    0.5f,
                    0.05f,
                    0.5f
                )
            );
        }
    }
}