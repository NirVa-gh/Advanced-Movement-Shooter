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
    [SerializeField] private float jumpSustainGravity = 0.4f;
    [SerializeField] private float gravity = -90f;

    [Space]
    [SerializeField] private float standHeight = 2f;
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private float crouchHeightResponce = 15f;

    [SerializeField] private float walkResponce = 15f;
    [SerializeField] private float crouchResponce = 15f;

    [Space]
    [SerializeField] private float slideStartSpeed = 25f;
    [SerializeField] private float slideEndSpeed = 15f;
    [SerializeField] private float slideFriction = 0.8f;

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
        _requestedRotation = input.Rotation;                                    // ��������� ����������� ��������
        _requestedMovement = new Vector3(input.Move.x, 0f, input.Move.y);       // ����������� 2D-���� � 3D-������
        _requestedMovement = Vector3.ClampMagnitude(_requestedMovement, 1f);    // ������������ ����� ������� �� 1
        _requestedMovement = input.Rotation * _requestedMovement;               // ��������� �������� � ������� ��������

        _requestedJump = _requestedJump || input.Jump;                          // ��������� ���� ������
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
        var forward = Vector3.ProjectOnPlane(_requestedRotation * Vector3.forward, motor.CharacterUp); // ���������� ����������� ������ �� ���������
        if (forward != Vector3.zero)
            currentRotation = Quaternion.LookRotation(forward, motor.CharacterUp); // ��������� �������� ���������
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
        if (motor.GroundingStatus.IsStableOnGround) // ���� �������� �� �����
        {
            var groundedMovement = motor.GetDirectionTangentToSurface
            (
                direction: _requestedMovement,
                surfaceNormal: motor.GroundingStatus.GroundNormal
            ) * _requestedMovement.magnitude; // �������� �������� ����� �����������
            //Slide
            {
                var moving = groundedMovement.sqrMagnitude > 0f;
                var crouching = _state.Stance is Stance.Crouch;
                var wasStanding = _lastState.Stance is Stance.Stand;
                var wasInAir = !_lastState.Grouded;
                if(moving && crouching && (wasStanding || wasInAir))
                {
                    _state.Stance = Stance.Slide;

                    var slideSpeed = Mathf.Max(slideStartSpeed, currentVelocity.magnitude);
                    currentVelocity = motor.GetDirectionTangentToSurface
                        (
                        direction: currentVelocity,
                        surfaceNormal: motor.GroundingStatus.GroundNormal
                        ) * slideSpeed;
                }
            }

            if (_state.Stance is Stance.Stand or Stance.Crouch)
            {
                var speed = _state.Stance is Stance.Stand
                    ? walkSpeed
                    : crouchSpeed;
                var responce = _state.Stance is Stance.Stand
                    ? walkResponce
                    : crouchResponce;

                var targetVelocity = groundedMovement * speed;
                currentVelocity = Vector3.Lerp
                    (
                    a: currentVelocity,
                    b: targetVelocity,
                    t: 1f - Mathf.Exp(-deltaTime * responce)
                    );
            }
            else
            {
                currentVelocity -= currentVelocity * (slideFriction * deltaTime);
                if (currentVelocity.magnitude < slideEndSpeed)
                    _state.Stance = Stance.Crouch;
            }

          
        }
        else // ���� �������� � �������
        {
            //Move in the air

            if(_requestedMovement.sqrMagnitude > 0f)
            {
                // Request movement projected onto movement plane
                var planarMovement = Vector3.ProjectOnPlane
                    (
                    vector: _requestedMovement,
                    planeNormal: motor.CharacterUp
                    ).normalized  * _requestedMovement.magnitude;

                // Current velocity on movement plane
                var currentPlanarVelocity = Vector3.ProjectOnPlane
                    (
                    vector: currentVelocity,
                    planeNormal: motor.CharacterUp
                    );

                // Calculate force
                var movementForce = planarMovement * airAcceleration * deltaTime;
                // Add planar velocity to targer velocity
                var targetPlanarVelocity = currentPlanarVelocity + movementForce;
                // Limit target velocity
                targetPlanarVelocity = Vector3.ClampMagnitude(targetPlanarVelocity, airSpeed);
                // Steer toward current velocity
                currentVelocity += targetPlanarVelocity - currentPlanarVelocity;
            }

            //Gravity
            var effectiveGravity = gravity;
            var verticalSpeed = Vector3.Dot
                (
                currentVelocity,
                motor.CharacterUp
                );

            if(_requestedSusteinedJump && verticalSpeed > 0f)
            {
                effectiveGravity *= jumpSustainGravity;
            }

            currentVelocity += motor.CharacterUp * effectiveGravity * deltaTime; // ��������� ����������
        }

        if (_requestedJump) // ���� �������� ������
        {
            _requestedJump = false; // ���������� ���� ������
            _requestedCrouch = false;
            motor.ForceUnground(time: 1f); // �������� ��������� �� �����
            var currentVerticalSpeed = Vector3.Dot(currentVelocity, motor.CharacterUp);
            var targetVerticalSpeed = Mathf.Max(currentVerticalSpeed, jumpSpeed);
            currentVelocity += motor.CharacterUp * (targetVerticalSpeed - currentVerticalSpeed);
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
        //Uncrouch
        if (!_requestedCrouch && _state.Stance is not Stance.Stand)
        {
            _state.Stance = Stance.Stand;
            motor.SetCapsuleDimensions
            (
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
                _requestedCrouch = true;
                motor.SetCapsuleDimensions
                (
                    radius: motor.Capsule.radius,
                    height: crouchHeight,
                    yOffset: crouchHeight * 0.5f
                );
            }
            else
            {
                _state.Stance = Stance.Stand;
            }
        }

        //Update state to reflect relevant motor prop
        _state.Grouded = motor.GroundingStatus.IsStableOnGround;
        _lastState = _tempState;
    }

    public bool IsColliderValidForCollisions(Collider coll) => true;
    public void OnDiscreteCollisionDetected(Collider hitCollider) { }
    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }
    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }
    public void PostGroundingUpdate(float deltaTime) 
    {
        if (!motor.GroundingStatus.IsStableOnGround && _state.Stance is Stance.Slide)
        {
            _state.Stance = Stance.Crouch;
        }
    }
    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport) { }
    #endregion
    public Transform GetCameraTarget() => cameraTarget;
    public void SetPosition(Vector3 position, bool killVelocity = true)
    {
        motor.SetPosition(position);
        if(killVelocity)
        {
            motor.BaseVelocity = Vector3.zero;
        }
    }
}
