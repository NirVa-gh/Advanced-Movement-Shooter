using UnityEngine;
using KinematicCharacterController;
using System;
using UnityEngine.Rendering;
using System.Runtime.CompilerServices;
using System.Xml.Schema;
using UnityEngine.UIElements;
public struct CharacterInput
{
    public Quaternion Rotation;
    public Vector2 Move;
    public bool Jump;
    public bool JumpSustain;
    public CrouchInput Crouch;

}
public enum CrouchInput
{
    None, Toggle
}

public struct CharacterState
{
    public bool Grouded;
    public Stance Stance;
    public Vector3 Velocity;
    public Vector3 Acceleration;
}
public enum Stance
{
    Stand, Crouch, Slide
}
public class PlayerCharacter : MonoBehaviour, ICharacterController
{
    [SerializeField] private KinematicCharacterMotor motor;
    [SerializeField] private Transform root;
    [SerializeField] private Transform cameraTarget;

    [Space]
    [SerializeField] private float walkSpeed = 20f;
    [SerializeField] private float crouchSpeed = 7f;

    [Space]
    [SerializeField] private float airSpeed = 15f;
    [SerializeField] private float airAcceleration = 70f;

    [Space]
    [SerializeField] private float jumpSpeed = 20f;
    [SerializeField] private float coyoteTime = 0.2f;
    [SerializeField] private float jumpSustainGravity = 0.4f;
    [SerializeField] private float gravity = -90f;

    [Space]
    [SerializeField] private float standHeight = 2f;
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private float crouchHeightResponce = 15f;

    [SerializeField] private float walkResponce = 5f;
    [SerializeField] private float crouchResponce = 15f;

    [Space]
    [SerializeField] private float slideStartSpeed = 25f;
    [SerializeField] private float slideEndSpeed = 15f;
    [SerializeField] private float slideFriction = 0.8f;
    [SerializeField] private float slideSteerAcceleration = 5f;
    [SerializeField] private float slideGravity = 90f;
    [SerializeField] private float _slideAirTimer = 0f;
    [SerializeField] private float maxSlideAirTime = 0.25f;
    [Space]
    [SerializeField] private bool _onOffDebug = false;

    [Space]
    [Range(0,1)]
    [SerializeField] private float standCameraTargetHeight = 0.9f;
    [Range(0, 1)]
    [SerializeField] private float crouchCameraTargetHeight = 0.7f;

    private CharacterState _state;
    private CharacterState _lastState;
    private CharacterState _tempState;

    private Quaternion _requestedRotation;
    private Vector3 _requestedMovement;
    private bool _requestedJump;
    private bool _requestedSusteinedJump;
    private bool _requestedCrouch;
    private bool _isSliding = false;
    private float _timeSinceJumpUngrounded;
    private float _timeSinceJumpRequest;
    private bool _ungroundedDueToJump;

    private Collider[] _uncrouchOverlapResult;

    public void Initialize()
    {
        _state.Stance = Stance.Stand;
        _lastState = _state;
        _uncrouchOverlapResult = new Collider[8];
        motor.CharacterController = this;
    }
    public void UpdateInput(CharacterInput input)
    {
        _requestedRotation = input.Rotation;                                    // Обновляем запрошенное вращение
        _requestedMovement = new Vector3(input.Move.x, 0f, input.Move.y);       // Преобразуем 2D-ввод в 3D-вектор
        _requestedMovement = Vector3.ClampMagnitude(_requestedMovement, 1f);    // Ограничиваем длину вектора до 1
        _requestedMovement = input.Rotation * _requestedMovement;               // Применяем вращение к вектору движения

        var wasRequestingJump = _requestedJump;
        _requestedJump = _requestedJump || input.Jump;
        if (_requestedJump && !wasRequestingJump)
            _timeSinceJumpRequest = 0f;
        
        // Обновляем флаг прыжка
        _requestedSusteinedJump = input.JumpSustain;
        _requestedCrouch = input.Crouch switch
        {
            CrouchInput.Toggle => !_requestedCrouch,
            CrouchInput.None => _requestedCrouch,
            _ => _requestedCrouch
        };
    }
    #region ICharacterController
    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        var forward = Vector3.ProjectOnPlane(_requestedRotation * Vector3.forward, motor.CharacterUp); // Проецируем направление вперед на плоскость
        if (forward != Vector3.zero)
            currentRotation = Quaternion.LookRotation(forward, motor.CharacterUp); // Обновляем вращение персонажа
    }
    public void UpdateBody(float deltaTime)
    {
        var currentHeight = motor.Capsule.height;
        var normalizedHeight = currentHeight / standHeight;
        var cameraTargetHeight = currentHeight *
            (_state.Stance is Stance.Stand 
            ? standCameraTargetHeight 
            : crouchCameraTargetHeight
            );
        var rootTargetScale = new Vector3(1f, normalizedHeight, 1f);

        cameraTarget.localPosition = Vector3.Lerp
            (
            a: cameraTarget.localPosition, 
            b: new Vector3 (0f,cameraTargetHeight,0f), 
            t: 1f - Mathf.Exp(-deltaTime * crouchHeightResponce)
            );
        root.localScale = Vector3.Lerp
            (
            a: root.localScale,
            b: rootTargetScale,
            t: 1f - Mathf.Exp(-deltaTime * crouchHeightResponce)
            );
    }
    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        _state.Acceleration = Vector3.zero;

        if (motor.GroundingStatus.IsStableOnGround) // Если персонаж на земле
        {
            _timeSinceJumpRequest = 0f;
            _ungroundedDueToJump = false;
            var groundedMovement = motor.GetDirectionTangentToSurface(
                direction: _requestedMovement,
                surfaceNormal: motor.GroundingStatus.GroundNormal
            ) * _requestedMovement.magnitude;

            // Start sliding
            {
                var moving = groundedMovement.sqrMagnitude > 0f;
                var crouching = _state.Stance is Stance.Crouch;
                var wasStanding = _lastState.Stance is Stance.Stand;
                var wasInAir = !_lastState.Grouded;
                if (moving && crouching && (wasStanding || wasInAir))
                {
                    _state.Stance = Stance.Slide;
                    _isSliding = true;
                    _slideAirTimer = 0f;

#if UNITY_EDITOR
                    Debug.Log($"[Slide] started. wasInAir={wasInAir}, lastSpeed={_lastState.Velocity.magnitude:F2}");
#endif
                    if (wasInAir)
                    {
                        currentVelocity = Vector3.ProjectOnPlane(
                            vector: _lastState.Velocity,
                            planeNormal: motor.GroundingStatus.GroundNormal
                        );
                    }

                    var slideSpeed = Mathf.Max(slideStartSpeed, currentVelocity.magnitude);
                    currentVelocity = motor.GetDirectionTangentToSurface(
                        direction: currentVelocity,
                        surfaceNormal: motor.GroundingStatus.GroundNormal
                    ) * slideSpeed;
                }
            }

            // Move (stand / crouch)
            if (_state.Stance is Stance.Stand or Stance.Crouch)
            {
                var speed = _state.Stance is Stance.Stand ? walkSpeed : crouchSpeed;
                var responce = _state.Stance is Stance.Stand ? walkResponce : crouchResponce;

                var targetVelocity = groundedMovement * speed;
                var moveVelocity = Vector3.Lerp
                (
                    a: currentVelocity,
                    b: targetVelocity,
                    t: 1f - Mathf.Exp(-deltaTime * responce)
                );
                _state.Acceleration = moveVelocity - currentVelocity;
                currentVelocity = moveVelocity;          
            }
            else // Slide branch
            {
                // Friction
                currentVelocity -= currentVelocity * (slideFriction * deltaTime);

                // Slope acceleration — правильно: проекция вектора гравитации на плоскость поверхности
                {
                    var gravityVector = motor.CharacterUp * gravity;
                    var slopeAcceleration = Vector3.ProjectOnPlane
                    (
                        vector: gravityVector, 
                        planeNormal: motor.GroundingStatus.GroundNormal
                    );
                    currentVelocity += slopeAcceleration * deltaTime;
                }


                // Steer towards requested movement direction
                {
                    var currentSpeed = currentVelocity.magnitude;
                    var targetVelocity = groundedMovement * currentSpeed;
                    var steerVelocity = currentVelocity;
                    var steerForce = (targetVelocity - steerVelocity) * slideSteerAcceleration * deltaTime;
                    steerVelocity += steerForce;
                    steerVelocity = Vector3.ClampMagnitude(steerVelocity, currentSpeed);
                    _state.Acceleration = steerVelocity - currentVelocity;
                    currentVelocity = steerVelocity;
                }

                if (currentVelocity.magnitude < slideEndSpeed)
                    _state.Stance = Stance.Crouch;
            }
        }
        else // Air movement (оставляем без изменений)
        {
            _timeSinceJumpUngrounded += deltaTime;
            if (_requestedMovement.sqrMagnitude > 0f)
            {
                var planarMovement = Vector3.ProjectOnPlane(
                    vector: _requestedMovement,
                    planeNormal: motor.CharacterUp
                ).normalized * _requestedMovement.magnitude;

                var currentPlanarVelocity = Vector3.ProjectOnPlane(
                    vector: currentVelocity,
                    planeNormal: motor.CharacterUp
                );

                var movementForce = planarMovement * airAcceleration * deltaTime;

                if (currentPlanarVelocity.magnitude < airSpeed)
                {
                    var targetPlanarVelocity = currentPlanarVelocity + movementForce;
                    targetPlanarVelocity = Vector3.ClampMagnitude(targetPlanarVelocity, airSpeed);
                    movementForce = targetPlanarVelocity - currentPlanarVelocity;
                }
                else if (Vector3.Dot(currentPlanarVelocity, movementForce) > 0f)
                {
                    var constraindeMovementForce = Vector3.ProjectOnPlane(
                        vector: movementForce,
                        planeNormal: currentPlanarVelocity.normalized
                    );
                    movementForce = constraindeMovementForce;
                }

                if (motor.GroundingStatus.FoundAnyGround)
                {
                    if (Vector3.Dot(movementForce, currentVelocity + movementForce) > 0f)
                    {
                        var obstructionNormal = Vector3.Cross(
                            motor.CharacterUp,
                            Vector3.Cross(motor.CharacterUp, motor.GroundingStatus.GroundNormal)
                        ).normalized;
                        movementForce = Vector3.ProjectOnPlane(movementForce, obstructionNormal);
                    }
                }
                currentVelocity += movementForce;
            }

            var effectiveGravity = gravity;
            var verticalSpeed = Vector3.Dot(currentVelocity, motor.CharacterUp);
            if (_requestedSusteinedJump && verticalSpeed > 0f)
                effectiveGravity *= jumpSustainGravity;

            currentVelocity += motor.CharacterUp * effectiveGravity * deltaTime;
        }

        // Jump 
        if (_requestedJump)
        {
            var grounded = motor.GroundingStatus.IsStableOnGround;
            var canCoyoteJump = _timeSinceJumpUngrounded < coyoteTime && !_ungroundedDueToJump;
            if (grounded || canCoyoteJump)
            {
                _requestedJump = false;
                _requestedCrouch = false;
                motor.ForceUnground(time: 0f);   // !!!Было time: 1f
                _ungroundedDueToJump = true;
                var currentVerticalSpeed = Vector3.Dot(currentVelocity, motor.CharacterUp);
                var targetVerticalSpeed = Mathf.Max(currentVerticalSpeed, jumpSpeed);
                currentVelocity += motor.CharacterUp * (targetVerticalSpeed - currentVerticalSpeed);
            }
            else
            {
                _timeSinceJumpRequest += deltaTime;
                var canJumpLater = _timeSinceJumpRequest < coyoteTime;
                _requestedJump = canJumpLater;

            }
        }
    }

    public void BeforeCharacterUpdate(float deltaTime)
    {
        _tempState = _state;
        //Crouch   
        if (_requestedCrouch && _state.Stance is Stance.Stand)
        {
            _state.Stance = Stance.Crouch;
            motor.SetCapsuleDimensions
            (
                radius: motor.Capsule.radius,
                height: crouchHeight,
                yOffset: crouchHeight * 0.5f
            );
        }
    }
    public void AfterCharacterUpdate(float deltaTime)
    {
        // Uncrouch — только если мы реально crouch (НЕ если мы в Slide)
        if (!_requestedCrouch && _state.Stance == Stance.Crouch)
        {
            // Попытка встать
            motor.SetCapsuleDimensions(
                radius: motor.Capsule.radius,
                height: standHeight,
                yOffset: standHeight * 0.5f
            );

            if (motor.CharacterOverlap(
                motor.TransientPosition,
                motor.TransientRotation,
                _uncrouchOverlapResult,
                motor.CollidableLayers,
                QueryTriggerInteraction.Ignore) > 0)
            {
                // Не можем встать — оставляем crouch
                _requestedCrouch = true;
                motor.SetCapsuleDimensions(
                    radius: motor.Capsule.radius,
                    height: crouchHeight,
                    yOffset: crouchHeight * 0.5f
                );
                _state.Stance = Stance.Crouch;
            }
            else
            {
                _state.Stance = Stance.Stand;
            }
        }

        // Обновляем состояние
        _state.Grouded = motor.GroundingStatus.IsStableOnGround;
        _state.Velocity = motor.Velocity;
        _lastState = _tempState;
    }



    public bool IsColliderValidForCollisions(Collider coll) => true;
    public void OnDiscreteCollisionDetected(Collider hitCollider) { }
    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }
    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }
    public void PostGroundingUpdate(float deltaTime)
    {
        if (_state.Stance is Stance.Slide)
        {
            if (!motor.GroundingStatus.IsStableOnGround)
            {
                if (!motor.GroundingStatus.FoundAnyGround)
                    _slideAirTimer += deltaTime;
            }
            else
            {
                _slideAirTimer = 0f;
            }

            var currentSpeed = motor.Velocity.magnitude;

            if (_slideAirTimer > maxSlideAirTime)
            {
#if UNITY_EDITOR
                Debug.Log($"[Slide] exit by air timer {_slideAirTimer:F3}s");
#endif
                _state.Stance = Stance.Crouch;
                _isSliding = false;
            }
            else if (currentSpeed < slideEndSpeed)
            {
#if UNITY_EDITOR
                Debug.Log($"[Slide] exit by speed {currentSpeed:F2} < {slideEndSpeed:F2}");
#endif
                _state.Stance = Stance.Crouch;
                _isSliding = false;
            }
        }
    }


    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport) { }
    #endregion
    public Transform GetCameraTarget() => cameraTarget;
    public CharacterState GetState() => _state;
    public CharacterState GetLastState() => _state;

    public void SetPosition(Vector3 position, bool killVelocity = true)
    {
        motor.SetPosition(position);
        if(killVelocity)
        {
            motor.BaseVelocity = Vector3.zero;
        }
    }


#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        if(_onOffDebug)
        {
            // --- 1. Отрисовка позиции ---
            Gizmos.color = _state.Grouded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.2f);

            // --- 2. Отрисовка вектора скорости ---
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, transform.position + _state.Velocity);

            // --- 3. Подготовка текста ---
            string debugInfo =
                $"=== PLAYER DEBUG ===\n" +
                $"Stance: {_state.Stance}\n" +
                $"Grounded: {_state.Grouded}\n" +
                $"Velocity:({_state.Velocity.magnitude:F2} m/s)\n" +
                $"Requested Jump: {_requestedJump}\n" +
                $"Sustained Jump: {_requestedSusteinedJump}\n" +
                $"Requested Crouch: {_requestedCrouch}\n" +
                $"Walk Speed: {walkSpeed}\n" +
                $"Crouch Speed: {crouchSpeed}\n" +
                $"Air Speed: {airSpeed}\n" +
                $"Jump Speed: {jumpSpeed}\n" +
                $"Gravity: {gravity}\n" +
                $"Slide Start Speed: {slideStartSpeed}\n" +
                $"Slide End Speed: {slideEndSpeed}\n" +
                $"Slide Friction: {slideFriction}\n" +
                $"MotorSpeed: {motor.Velocity.magnitude:F2}\n" +
                $"IsStableOnGround: {motor.GroundingStatus.IsStableOnGround}\n" +
                $"FoundAnyGround: {motor.GroundingStatus.FoundAnyGround}\n" +
                $"slideAirTimer: {_slideAirTimer:F3}\n";

            // --- 4. Отрисовка текста в сцене ---
            UnityEditor.Handles.BeginGUI();
            {
                var pos = transform.position + Vector3.up * 2.5f;
                var guiPos = UnityEditor.HandleUtility.WorldToGUIPoint(pos);

                GUIStyle style = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 12,
                    normal = { textColor = Color.white },
                    alignment = TextAnchor.UpperLeft
                };

                GUIContent content = new GUIContent(debugInfo);
                Vector2 size = style.CalcSize(content);

                GUI.Box(new Rect(guiPos.x, guiPos.y, size.x, size.y), debugInfo, style);
            }
            UnityEditor.Handles.EndGUI();
        }
    }
#endif
}
