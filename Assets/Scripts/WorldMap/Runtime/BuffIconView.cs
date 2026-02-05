using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BuffIconView : MonoBehaviour
{
    [SerializeField] private string buffName;
    [SerializeField] private float remainingHours;

    private SpriteRenderer _sr;

    public void Initialize(Sprite sprite)
    {
        _sr = GetComponent<SpriteRenderer>();
        if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sprite = sprite;
        _sr.sortingOrder = 1100;
    }

    public void SetData(string name, float remainingHrs, Color tint)
    {
        buffName = name;
        remainingHours = remainingHrs;

        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        if (_sr != null) _sr.color = tint;
    }

    public string GetTooltipText()
        => $"{buffName}\n{remainingHours:0.0}h remaining";
}
