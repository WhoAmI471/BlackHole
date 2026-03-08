using UnityEngine;

/// <summary>
/// Объект, который может провалиться в чёрную дыру и увеличить её.
/// При попадании в триггер дыры — уничтожается и добавляет дыре рост.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ConsumableObject : MonoBehaviour
{
    [Tooltip("Масса объекта (влияет на то, насколько вырастет дыра)")]
    [Min(0.01f)]
    public float mass = 1f;

    [Tooltip("Размер объекта (радиус). Объект можно засосать только если размер < радиуса дыры. Если 0 — считается по коллайдеру")]
    [Min(0f)]
    public float size = 0f;

    [Tooltip("Задержка перед уничтожением (сек), чтобы объект визуально \"провалился\"")]
    public float sinkDelay = 0.2f;

    [Header("Визуал проваливания (опционально)")]
    [Tooltip("Уменьшать scale при проваливании")]
    public bool shrinkWhenConsumed = true;

    [Tooltip("При поглощении двигать объект к центру дыры")]
    public bool moveToCenterWhenConsumed = true;

    bool _consumed;

    /// <summary>
    /// Размер объекта для сравнения с радиусом дыры (можно засосать только если меньше дыры).
    /// </summary>
    public float GetConsumableSize()
    {
        if (size > 0f)
            return size;
        Collider c = GetComponent<Collider>();
        if (c != null)
        {
            Vector3 ext = c.bounds.extents;
            return Mathf.Max(ext.x, ext.z);
        }
        return transform.lossyScale.x * 0.5f;
    }

    /// <summary>
    /// Вызывается BlackHoleController при попадании в триггер.
    /// </summary>
    public void ConsumeBy(BlackHoleController hole)
    {
        if (_consumed)
            return;
        _consumed = true;

        hole.GrowBy(mass);

        if (sinkDelay <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        StartCoroutine(SinkAndDestroy(hole));
    }

    System.Collections.IEnumerator SinkAndDestroy(BlackHoleController hole)
    {
        float elapsed = 0f;
        Vector3 initialScale = transform.localScale;
        Vector3 startPos = transform.position;
        Vector3 centerPos = hole.transform.position;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        while (elapsed < sinkDelay)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / sinkDelay;

            if (moveToCenterWhenConsumed)
                transform.position = Vector3.Lerp(startPos, centerPos, t);
            if (shrinkWhenConsumed)
                transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, t);

            yield return null;
        }

        Destroy(gameObject);
    }
}
