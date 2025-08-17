using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class StanceVignette : MonoBehaviour
{
    [SerializeField] private float min = 0.1f;
    [SerializeField] private float max = 0.35f;
    [SerializeField] private float response = 10f;

    private VolumeProfile _profile;
    private Vignette _vignette;


    public void Initialize(VolumeProfile profile)
    {
        _profile = profile;

        if (_profile.TryGet(out _vignette))
        {
            _vignette.intensity.overrideState = true;
            _vignette.intensity.value = min;
        }
        else
        {
            Debug.LogError("Vignette not found in VolumeProfile! Добавь её в Volume через инспектор.");
        }
    }

    public void UpdateVignette(float deltaTime, Stance stance)
    {
        var targetIntensity = stance == Stance.Stand ? min : max;
        _vignette.smoothness.value = Mathf.Lerp(
            _vignette.smoothness.value,
            targetIntensity,
            1f - Mathf.Exp(-response * deltaTime)
        );
    }

}
