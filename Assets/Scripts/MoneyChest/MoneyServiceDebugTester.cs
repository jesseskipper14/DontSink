using UnityEngine;

public sealed class MoneyServiceDebugTester : MonoBehaviour
{
#if UNITY_EDITOR
    [ContextMenu("Debug/Add 100 Via MoneyService")]
    private void DebugAdd100()
    {
        bool result = MoneyService.AddMoney(100);
        Debug.Log($"MoneyService.AddMoney(100) result={result}, balance={MoneyService.Balance}", this);
    }

    [ContextMenu("Debug/Spend 25 Via MoneyService")]
    private void DebugSpend25()
    {
        bool result = MoneyService.TrySpend(25);
        Debug.Log($"MoneyService.TrySpend(25) result={result}, balance={MoneyService.Balance}", this);
    }

    [ContextMenu("Debug/Check Can Spend 500")]
    private void DebugCanSpend500()
    {
        bool result = MoneyService.CanSpend(500);
        Debug.Log($"MoneyService.CanSpend(500) result={result}, balance={MoneyService.Balance}", this);
    }
#endif
}