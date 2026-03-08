using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Управляемая чёрная дыра без дна.
/// Объекты проваливаются в триггер и увеличивают радиус дыры.
/// Управление через новую Input System.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class BlackHoleController : MonoBehaviour
{
    [Header("Размер")]
    [Tooltip("Начальный радиус дыры")]
    [Min(0.1f)]
    public float initialRadius = 1f;

    [Tooltip("На сколько увеличивается радиус за один поглощённый объект (по массе)")]
    public float growthPerMass = 0.1f;

    [Tooltip("Минимальный и максимальный радиус")]
    public Vector2 radiusClamp = new Vector2(0.5f, 20f);

    [Tooltip("Объект можно засосать только если его размер (ConsumableObject.size) меньше радиуса дыры. Множитель: 1 = строго меньше радиуса")]
    [Range(0.5f, 1f)]
    public float maxConsumableSizeRatio = 0.95f;

    [Header("Управление (Input System)")]
    [Tooltip("Скорость движения дыры")]
    public float moveSpeed = 5f;

    [Tooltip("Управление мышью (позиция в мире на плоскости Y=0)")]
    public bool controlByMouse = true;

    [Tooltip("Action Move (Vector2) — используется при выключенном управлении мышью (WASD / геймпад)")]
    public InputActionReference moveActionRef;

    [Tooltip("Action Point (Vector2) — позиция указателя на экране для режима мыши. Если не задан — используется Mouse.current.position")]
    public InputActionReference pointerPositionActionRef;

    [Header("Притяжение (опционально)")]
    [Tooltip("Притягивать объекты к центру до попадания в триггер")]
    public bool pullObjects = true;

    [Tooltip("Радиус притяжения (кратно текущему радиусу)")]
    [Min(1f)]
    public float pullRadiusMultiplier = 3f;

    [Tooltip("Сила притяжения")]
    public float pullForce = 10f;

    [Header("Визуал")]
    [Tooltip("Трансформ для масштабирования (дыра). Если null — масштабируется этот объект.")]
    public Transform holeVisual;

    // Текущий радиус (логика)
    float _currentRadius;
    SphereCollider _sphereCollider;
    Camera _mainCam;
    Plane _groundPlane;
    Rigidbody _rb;

    public float CurrentRadius => _currentRadius;

    void Awake()
    {
        _sphereCollider = GetComponent<SphereCollider>();
        if (_sphereCollider == null)
            _sphereCollider = gameObject.AddComponent<SphereCollider>();
        _sphereCollider.isTrigger = true;
        _sphereCollider.radius = 1f; // масштаб через radius ниже

        _rb = GetComponent<Rigidbody>();
        if (_rb != null)
        {
            _rb.isKinematic = true;
            _rb.useGravity = false;
        }

        _currentRadius = initialRadius;
        _mainCam = Camera.main;
        _groundPlane = new Plane(Vector3.up, Vector3.zero);
    }

    void OnEnable()
    {
        if (moveActionRef != null)
            moveActionRef.action?.Enable();
        if (pointerPositionActionRef != null)
            pointerPositionActionRef.action?.Enable();
    }

    void OnDisable()
    {
        if (moveActionRef != null)
            moveActionRef.action?.Disable();
        if (pointerPositionActionRef != null)
            pointerPositionActionRef.action?.Disable();
    }

    void Start()
    {
        ApplyRadius();
    }

    void Update()
    {
        HandleMovement();
        ApplyRadius();
    }

    void FixedUpdate()
    {
        if (pullObjects)
            ApplyPull();
    }

    void HandleMovement()
    {
        Vector3 targetPos = transform.position;

        if (controlByMouse && _mainCam != null)
        {
            Vector2 screenPos = GetPointerScreenPosition();
            Ray ray = _mainCam.ScreenPointToRay(screenPos);
            if (_groundPlane.Raycast(ray, out float enter))
                targetPos = ray.GetPoint(enter);
        }
        else
        {
            Vector2 move = GetMoveInput();
            targetPos = transform.position + new Vector3(move.x, 0f, move.y) * (moveSpeed * Time.deltaTime);
        }

        transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
    }

    Vector2 GetPointerScreenPosition()
    {
        if (pointerPositionActionRef != null && pointerPositionActionRef.action != null && pointerPositionActionRef.action.enabled)
        {
            var pos = pointerPositionActionRef.action.ReadValue<Vector2>();
            return pos;
        }
        if (Mouse.current != null)
            return Mouse.current.position.ReadValue();
        return Vector2.zero;
    }

    Vector2 GetMoveInput()
    {
        if (moveActionRef != null && moveActionRef.action != null && moveActionRef.action.enabled)
            return moveActionRef.action.ReadValue<Vector2>();
        return Vector2.zero;
    }

    void ApplyRadius()
    {
        _currentRadius = Mathf.Clamp(_currentRadius, radiusClamp.x, radiusClamp.y);
        // SphereCollider.radius в единицах локального масштаба; при scale 1 — radius в метрах
        _sphereCollider.radius = _currentRadius;
        Transform visual = holeVisual != null ? holeVisual : transform;
        Vector3 scale = visual.localScale;
        scale.x = _currentRadius * 2f;
        scale.z = _currentRadius * 2f;
        if (holeVisual == null)
            scale.y = Mathf.Max(scale.y, _currentRadius * 0.5f);
        else
            scale.y = _currentRadius * 0.5f;
        visual.localScale = scale;
    }

    void ApplyPull()
    {
        float pullRadius = _currentRadius * pullRadiusMultiplier;
        Collider[] cols = Physics.OverlapSphere(transform.position, pullRadius);
        Vector3 center = transform.position;

        foreach (Collider col in cols)
        {
            if (col.attachedRigidbody == null || col.attachedRigidbody == _rb)
                continue;
            if (col.isTrigger)
                continue;

            ConsumableObject consumable = col.GetComponent<ConsumableObject>();
            if (consumable == null)
                continue; // притягиваем только объекты с ConsumableObject

            // Не притягиваем объекты, которые больше дыры — их всё равно не засосём
            if (consumable.GetConsumableSize() > _currentRadius * maxConsumableSizeRatio)
                continue;

            Vector3 toCenter = center - col.transform.position;
            float dist = toCenter.magnitude;
            if (dist < 0.01f)
                continue;
            // Притяжение только снаружи триггера дыры
            if (dist < _currentRadius)
                continue;

            float strength = 1f - (dist / pullRadius) * 0.5f;
            col.attachedRigidbody.AddForce(toCenter.normalized * (pullForce * strength * Time.fixedDeltaTime), ForceMode.VelocityChange);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        ConsumableObject consumable = other.GetComponent<ConsumableObject>();
        if (consumable == null)
            return;
        // Не засасываем объекты больше дыры
        if (consumable.GetConsumableSize() > _currentRadius * maxConsumableSizeRatio)
            return;
        consumable.ConsumeBy(this);
    }

    /// <summary>
    /// Вызвать из ConsumableObject при поглощении — увеличивает дыру.
    /// </summary>
    public void GrowBy(float massValue)
    {
        _currentRadius += growthPerMass * massValue;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        float r = Application.isPlaying ? _currentRadius : initialRadius;
        Gizmos.color = new Color(0f, 0f, 0.5f, 0.3f);
        Gizmos.DrawSphere(transform.position, r);
        if (pullObjects)
        {
            Gizmos.color = new Color(0.5f, 0f, 0.5f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, r * (Application.isPlaying ? pullRadiusMultiplier : 3f));
        }
    }
#endif
}
