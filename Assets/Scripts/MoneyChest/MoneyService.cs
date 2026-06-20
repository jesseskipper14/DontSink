using UnityEngine;

public static class MoneyService
{
    public static bool HasActiveChest
    {
        get
        {
            MoneyChestTreasuryService treasury = MoneyChestTreasuryService.Instance;
            return treasury != null && treasury.HasActiveChest;
        }
    }

    public static int Balance
    {
        get
        {
            MoneyChestTreasuryService treasury = MoneyChestTreasuryService.Instance;
            return treasury != null ? treasury.ActiveBalance : 0;
        }
    }

    public static bool CanSpend(int amount)
    {
        if (amount <= 0)
            return true;

        MoneyChestTreasuryService treasury = MoneyChestTreasuryService.Instance;
        return treasury != null && treasury.CanSpend(amount);
    }

    public static bool TrySpend(int amount)
    {
        if (amount <= 0)
            return true;

        MoneyChestTreasuryService treasury = MoneyChestTreasuryService.Instance;
        if (treasury == null)
        {
            Debug.LogWarning($"Cannot spend {amount}. No MoneyChestTreasuryService exists.");
            return false;
        }

        return treasury.TrySpend(amount);
    }

    public static bool AddMoney(int amount)
    {
        if (amount <= 0)
            return true;

        MoneyChestTreasuryService treasury = MoneyChestTreasuryService.Instance;
        if (treasury == null)
        {
            Debug.LogWarning($"Cannot add {amount}. No MoneyChestTreasuryService exists.");
            return false;
        }

        return treasury.AddMoney(amount);
    }
}