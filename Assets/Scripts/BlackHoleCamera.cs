using UnityEngine;

/// <summary>
/// Камера следует за дырой и отдаляется по мере её роста.
/// </summary>
public class BlackHoleCamera : MonoBehaviour
{
    [Tooltip("Объект дыры (BlackHoleController)")]
    public BlackHoleController hole;

    [Tooltip("Базовая дистанция камеры от дыры")]
    public float baseDistance = 10f;

    [Tooltip("На сколько отдалять камеру за каждую единицу радиуса дыры")]
    public float distancePerRadius = 2f;

    [Tooltip("Высота камеры над плоскостью Y=0")]
    public float height = 5f;

    [Tooltip("Сглаживание движения камеры (0 = без сглаживания)")]
    [Range(0f, 1f)]
    public float smoothTime = 0.15f;

    Vector3 _velocity;
    Vector3 _directionFromHole; // направление от дыры к камере (сохраняем угол)
    Quaternion _initialRotation;
    bool _initialized;

    void LateUpdate()
    {
        if (hole == null)
            return;

        if (!_initialized)
        {
            Vector3 holePoss = hole.transform.position;
            _directionFromHole = (transform.position - holePoss).normalized;
            if (_directionFromHole.sqrMagnitude < 0.01f)
                _directionFromHole = -Vector3.forward;
            _initialRotation = transform.rotation;
            _initialized = true;
        }

        Vector3 holePos = hole.transform.position;
        float distance = baseDistance + hole.CurrentRadius * distancePerRadius;
        Vector3 targetPos = holePos + _directionFromHole * distance;

        if (smoothTime <= 0f)
            transform.position = targetPos;
        else
            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _velocity, smoothTime);

        transform.rotation = _initialRotation;
    }
}
