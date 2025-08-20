using UnityEngine;

public class CameraLean : MonoBehaviour
{
    [SerializeField] private float attackDamping = 0.12f;  // быстрее реагируем, когда ускорение растёт
    [SerializeField] private float decayDamping = 0.30f;  // медленнее отпускаем
    [SerializeField] private float strength = 2.0f;   // градусов на единицу ускорения
    [SerializeField] private float maxLean = 15f;    // максимум наклона (в градусах)

    private Vector3 _dampedAcceleration;
    private Vector3 _dampedAccelerationVel;

    public void Initialize() { }

    public void UpdateLean(float deltaTime, Vector3 acceleration, Vector3 up)
    {
        // 1) Берём только горизонтальную составляющую
        var planar = Vector3.ProjectOnPlane(acceleration, up);

        // 2) Задаём разные времена сглаживания на атаку/распад
        float damping = (planar.sqrMagnitude > _dampedAcceleration.sqrMagnitude)
            ? attackDamping
            : decayDamping;

        _dampedAcceleration = Vector3.SmoothDamp(
            current: _dampedAcceleration,
            target: planar,
            currentVelocity: ref _dampedAccelerationVel,
            smoothTime: Mathf.Max(0.0001f, damping),
            maxSpeed: Mathf.Infinity,
            deltaTime: deltaTime
        );

        // Для дебага
        Debug.DrawRay(transform.position, acceleration, Color.red);
        Debug.DrawRay(transform.position, _dampedAcceleration * 10f, Color.blue);

        // 3) Если слишком мало — сбрасываем наклон и выходим
        if (_dampedAcceleration.sqrMagnitude < 1e-6f)
        {
            transform.localRotation = Quaternion.identity;
            return;
        }

        // 4) Строим «право» в базисе родителя камеры
        var basisForward = transform.parent ? transform.parent.forward : transform.forward;
        var basisRight = Vector3.Cross(up, basisForward).normalized;

        // 5) Компонента ускорения вправо => положительный ролл
        float lateral = Vector3.Dot(_dampedAcceleration, basisRight);
        float angle = Mathf.Clamp(lateral * strength, -maxLean, maxLean);

        // 6) Крутим ТОЛЬКО локально вокруг Z (ролл)
        transform.localRotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }
}
