using UnityEngine;

public class CameraSpring : MonoBehaviour
{
    [Min(0.01f)]
    [SerializeField] private float halfTime = 0.075f;
    [Space]
    [SerializeField] private float frequency = 18f;
    [Space]
    [SerializeField] private float angularDisplacement = 2f;
    [Space]
    [SerializeField] private float linearDisplacement = 0.05f;
    [Space]

    private Vector3 _springPosition;
    private Vector3 _springVelocity;
    public void Initialize()
    {


        _springPosition = transform.position;
        _springVelocity = Vector3.zero;
        Debug.Log("transform.position - " + transform.position + " _springPosition - " + _springPosition);

    }
    public void UpdateSpring(float deltaTime, Vector3 up)
    {
        transform.localPosition = Vector3.zero;

        Spring(ref _springPosition, ref _springVelocity, transform.position, halfTime, frequency, deltaTime);
        var relativeSpringPosition = _springPosition - transform.position;
        var springHeight = Vector3.Dot(relativeSpringPosition, up);

        transform.localEulerAngles = new Vector3(-springHeight * angularDisplacement, 0f, 0f);
        transform.localPosition = relativeSpringPosition * linearDisplacement;
    }
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, _springPosition);
        Gizmos.DrawSphere(_springPosition, 0.3f);
    }
    public void Spring(ref Vector3 current, ref Vector3 velocity, Vector3 target, float halfLife, float frequency, float timeStep)
    {
        Debug.Log("Updating Spring");
        var dampingRatio = -Mathf.Log(0.5f) / (frequency * halfTime);
        var f = 1.0f + 2.0f * timeStep * dampingRatio * frequency;
        var oo = frequency * frequency;
        var hoo = timeStep * oo;
        var hhoo = timeStep * hoo;
        var detInv = 1.0f / (f + hhoo);
        Vector3 detX = f * current + timeStep * velocity + hhoo * target;
        Vector3 detV = velocity + hoo * (target - current);
        current = detX * detInv;
        velocity = detV * detInv;
    }
}
