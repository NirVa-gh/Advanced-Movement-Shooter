using UnityEngine;
public struct CameraInput
{
    public Vector2 Look;
}
public class PlayerCamera : MonoBehaviour
{
    [SerializeField] private float minPitch = -90f;
    [SerializeField] private float maxPitch = 80f;
    [SerializeField] private float sensitivity = 2f;
    private Vector3 _eulerAngles;

    public void Initialize(Transform target)
    {
        transform.position = target.position;
        transform.eulerAngles = _eulerAngles = target.eulerAngles;
    }
    public void UpdateRotation(CameraInput input)
    {
        _eulerAngles.x += -input.Look.y * sensitivity;
        _eulerAngles.y += input.Look.x * sensitivity;

        _eulerAngles.x = Mathf.Clamp(_eulerAngles.x, minPitch, maxPitch);

        transform.eulerAngles = _eulerAngles;
    }
    public void UpdatePosition(Transform target)
    {
        transform.position = target.position;
    }
}
