using UnityEngine;
public struct CameraInput
{
    public Vector2 Look;
}
public class PlayerCamera : MonoBehaviour
{
    private Vector3 _eulerAngels;
    public void Initialize(Transform target)
    {
        transform.position = target.position;
        transform.eulerAngles = _eulerAngels = target.eulerAngles;
    }
    public void UpdateRotation(CameraInput input)
    {
        _eulerAngels += new Vector3(-input.Look.y, input.Look.x);
        transform.eulerAngles = _eulerAngels;
    }
    public void UpdatePosition(Transform target)
    {
        transform.position = target.position;
    }
}
