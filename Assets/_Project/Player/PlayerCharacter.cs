using UnityEngine;
using KinematicCharacterController;
using System;
using UnityEngine.Rendering;
using System.Runtime.CompilerServices;
public struct CharacterInput
{
    public Quaternion Rotation;
    public Vector2 Move;
    public bool Jump;
    public CrouchInput Crouch;
}
public enum CrouchInput
{
    None, Toggle
}
public enum Stance
{
    Stand, Crouch
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
    [SerializeField] private float jumpSpeed = 20f;
    [SerializeField] private float gravity = -90f;
    [Space]
    [SerializeField] private float standHeight = 2f;
    [SerializeField] private float crouchHeight = 1f;
    [Space]
    [Range(0,1)]
    [SerializeField] private float standCameraTargetHeight = 0.9f;
    [Range(0, 1)]
    [SerializeField] private float crouchCameraTargetHeight = 0.7f;

    private Stance _stance;
    private Quaternion _requestedRotation;
    private Vector3 _requestedMovement;
    private bool _requestedJump;
    private bool _requestedCrouch;

    private Collider[] _uncrouchOverlapResult;

    public void Initialize()
    {
        _stance = Stance.Stand;
        _uncrouchOverlapResult = new Collider[8];
        motor.CharacterController = this;
    }
    public void UpdateInput(CharacterInput input)
    {
        _requestedRotation = input.Rotation;                                    // Обновляем запрошенное вращение
        _requestedMovement = new Vector3(input.Move.x, 0f, input.Move.y);       // Преобразуем 2D-ввод в 3D-вектор
        _requestedMovement = Vector3.ClampMagnitude(_requestedMovement, 1f);    // Ограничиваем длину вектора до 1
        _requestedMovement = input.Rotation * _requestedMovement;               // Применяем вращение к вектору движения

        _requestedJump = _requestedJump || input.Jump;                          // Обновляем флаг прыжка
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
    public void UpdateBody()
    {
        var currentHeight = motor.Capsule.height - 1;
        var normalizedHeight = motor.Capsule.height / standHeight;
        var cameraTargetHeight = currentHeight * (_stance is Stance.Stand ? standCameraTargetHeight : crouchCameraTargetHeight);
        var rootTargetScale = new Vector3(1f, normalizedHeight, 1f);

        cameraTarget.localPosition = new Vector3(0f, cameraTargetHeight, 0f);
        //root.localScale = rootTargetScale;
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        if (motor.GroundingStatus.IsStableOnGround) // Если персонаж на земле
        {
            var groundedMovement = motor.GetDirectionTangentToSurface
            (
                direction: _requestedMovement,
                surfaceNormal: motor.GroundingStatus.GroundNormal
            ) * _requestedMovement.magnitude; // Получаем движение вдоль поверхности

            var speed = _stance is Stance.Stand ? walkSpeed : crouchSpeed;
            currentVelocity = groundedMovement * speed; // Обновляем скорость
        }
        else // Если персонаж в воздухе
        {
            currentVelocity += motor.CharacterUp * gravity * deltaTime; // Применяем гравитацию
        }

        if (_requestedJump) // Если запрошен прыжок
        {
            _requestedJump = false; // Сбрасываем флаг прыжка
            motor.ForceUnground(time: 0f); // Отрываем персонажа от земли
            var currentVerticalSpeed = Vector3.Dot(currentVelocity, motor.CharacterUp);
            var targetVerticalSpeed = Mathf.Max(currentVerticalSpeed, jumpSpeed);
            currentVelocity += motor.CharacterUp * (targetVerticalSpeed - currentVerticalSpeed);
        }
    }
    public void AfterCharacterUpdate(float deltaTime) 
    {
        //Uncrouch
        if (!_requestedCrouch && _stance is not Stance.Stand)
        {
            _stance = Stance.Stand;
            motor.SetCapsuleDimensions
            (
                radius: motor.Capsule.radius,
                height: standHeight,
                yOffset: -standHeight * 0
            );
            if (motor.CharacterOverlap(motor.TransientPosition,motor.TransientRotation,_uncrouchOverlapResult,motor.CollidableLayers,QueryTriggerInteraction.Ignore) > 0)
            {
                _requestedCrouch = true;
                motor.SetCapsuleDimensions
                (
                    radius: motor.Capsule.radius,
                    height: crouchHeight,
                    yOffset: -crouchHeight * 0.5f
                );
            }
            else
            {
                _stance = Stance.Stand;
            }
        }
    }
    public void BeforeCharacterUpdate(float deltaTime) 
    {
        //Crouch
       
        if (_requestedCrouch && _stance is Stance.Stand)
        {
            _stance = Stance.Crouch;
            motor.SetCapsuleDimensions
            (
                radius: motor.Capsule.radius,
                height: crouchHeight,
                yOffset: -crouchHeight * 0.5f
            );
        }
    }
    public bool IsColliderValidForCollisions(Collider coll) => true;
    public void OnDiscreteCollisionDetected(Collider hitCollider) { }
    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }
    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }
    public void PostGroundingUpdate(float deltaTime) { }
    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport) { }
    #endregion
    public Transform GetCameraTarget() => cameraTarget;
}
