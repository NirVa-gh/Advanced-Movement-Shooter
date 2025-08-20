using UnityEngine;

public class CameraLean : MonoBehaviour
{
    [SerializeField] private float attackDamping = 0.12f;  // ������� ���������, ����� ��������� �����
    [SerializeField] private float decayDamping = 0.30f;  // ��������� ���������
    [SerializeField] private float strength = 2.0f;   // �������� �� ������� ���������
    [SerializeField] private float maxLean = 15f;    // �������� ������� (� ��������)

    private Vector3 _dampedAcceleration;
    private Vector3 _dampedAccelerationVel;

    public void Initialize() { }

    public void UpdateLean(float deltaTime, Vector3 acceleration, Vector3 up)
    {
        // 1) ���� ������ �������������� ������������
        var planar = Vector3.ProjectOnPlane(acceleration, up);

        // 2) ����� ������ ������� ����������� �� �����/������
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

        // ��� ������
        Debug.DrawRay(transform.position, acceleration, Color.red);
        Debug.DrawRay(transform.position, _dampedAcceleration * 10f, Color.blue);

        // 3) ���� ������� ���� � ���������� ������ � �������
        if (_dampedAcceleration.sqrMagnitude < 1e-6f)
        {
            transform.localRotation = Quaternion.identity;
            return;
        }

        // 4) ������ ������ � ������ �������� ������
        var basisForward = transform.parent ? transform.parent.forward : transform.forward;
        var basisRight = Vector3.Cross(up, basisForward).normalized;

        // 5) ���������� ��������� ������ => ������������� ����
        float lateral = Vector3.Dot(_dampedAcceleration, basisRight);
        float angle = Mathf.Clamp(lateral * strength, -maxLean, maxLean);

        // 6) ������ ������ �������� ������ Z (����)
        transform.localRotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }
}
