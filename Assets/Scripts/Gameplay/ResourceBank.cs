using System;
using UnityEngine;

public sealed class ResourceBank : MonoBehaviour
{
    [SerializeField, Min(0)] private int startingWood;
    [SerializeField, Min(0)] private int startingFood = 2;
    [SerializeField, Min(0)] private int startingCoins = 2;

    public int Wood { get; private set; }
    public int Food { get; private set; }
    public int Reputation { get; private set; }
    public int Coins { get; private set; }
    public int CornSeeds { get; private set; }
    public int PumpkinSeeds { get; private set; }
    public int Fish { get; private set; }
    public int Corn { get; private set; }
    public int Pumpkin { get; private set; }
    public event Action Changed;

    private void Awake()
    {
        Wood = startingWood;
        Food = startingFood;
        Reputation = 0;
        Coins = startingCoins;
        CornSeeds = 0;
        PumpkinSeeds = 0;
        Fish = 0;
        Corn = 0;
        Pumpkin = 0;
    }

    public void AddWood(int amount)
    {
        Wood += Mathf.Max(0, amount);
        Changed?.Invoke();
    }

    public void AddFood(int amount)
    {
        Food += Mathf.Max(0, amount);
        Changed?.Invoke();
    }

    public void AddFish(int amount) { Fish += Mathf.Max(0, amount); Changed?.Invoke(); }
    public void AddCrop(CropType crop, int amount)
    {
        if (crop == CropType.Corn) Corn += Mathf.Max(0, amount);
        else if (crop == CropType.Pumpkin) Pumpkin += Mathf.Max(0, amount);
        Changed?.Invoke();
    }

    public bool TryPrepareFood(int fish, int corn, int pumpkin, int foodPerFish, int foodPerCorn, int foodPerPumpkin,
        out int preparedFood)
    {
        preparedFood = 0;
        if (fish < 0 || corn < 0 || pumpkin < 0 || fish > Fish || corn > Corn || pumpkin > Pumpkin ||
            fish + corn + pumpkin == 0) return false;
        Fish -= fish;
        Corn -= corn;
        Pumpkin -= pumpkin;
        preparedFood = fish * Mathf.Max(0, foodPerFish) + corn * Mathf.Max(0, foodPerCorn) +
            pumpkin * Mathf.Max(0, foodPerPumpkin);
        Changed?.Invoke();
        return true;
    }

    public bool TryDeliverFish(int amount) => TryRemove(SaleItem.Fish, amount);
    public bool TryDeliverCrop(CropType crop, int amount) => crop switch
    {
        CropType.Corn => TryRemove(SaleItem.Corn, amount),
        CropType.Pumpkin => TryRemove(SaleItem.Pumpkin, amount),
        _ => false
    };

    public bool TrySellBatch(int[] quantities, int[] prices)
    {
        if (quantities == null || prices == null || quantities.Length != 7 || prices.Length != 7) return false;
        long total = 0;
        int selected = 0;
        for (int i = 0; i < quantities.Length; i++)
        {
            int quantity = quantities[i];
            if (quantity < 0 || prices[i] < 0 || quantity > StockForSale((SaleItem)i)) return false;
            if (quantity == 0) continue;
            selected++;
            total += (long)quantity * prices[i];
        }
        if (selected == 0 || total > int.MaxValue - Coins) return false;
        Wood -= quantities[(int)SaleItem.Wood];
        Fish -= quantities[(int)SaleItem.Fish];
        Corn -= quantities[(int)SaleItem.Corn];
        Pumpkin -= quantities[(int)SaleItem.Pumpkin];
        Food -= quantities[(int)SaleItem.Food];
        CornSeeds -= quantities[(int)SaleItem.CornSeeds];
        PumpkinSeeds -= quantities[(int)SaleItem.PumpkinSeeds];
        Coins += (int)total;
        Changed?.Invoke();
        return true;
    }

    private int StockForSale(SaleItem item) => item switch
    {
        SaleItem.Wood => Wood,
        SaleItem.Fish => Fish,
        SaleItem.Corn => Corn,
        SaleItem.Pumpkin => Pumpkin,
        SaleItem.Food => Food,
        SaleItem.CornSeeds => CornSeeds,
        _ => PumpkinSeeds
    };

    public int ConsumeFood(int amount)
    {
        int consumed = Mathf.Min(Food, Mathf.Max(0, amount));
        Food -= consumed;
        Changed?.Invoke();
        return consumed;
    }

    public void AddReputation(int amount)
    {
        Reputation += amount;
        Changed?.Invoke();
    }

    public void AddCoins(int amount)
    {
        Coins += Mathf.Max(0, amount);
        Changed?.Invoke();
    }

    public bool TrySpendCoins(int amount)
    {
        if (amount < 0 || Coins < amount) return false;
        Coins -= amount;
        Changed?.Invoke();
        return true;
    }

    public bool TryDeliverWood(int amount) => TryRemove(SaleItem.Wood, amount);
    public bool TryDeliverFood(int amount) => TryRemove(SaleItem.Food, amount);
    public bool TrySpendWood(int amount) => TryRemove(SaleItem.Wood, amount);

    public void AddSeeds(CropType type, int amount)
    {
        if (type == CropType.Corn) CornSeeds += Mathf.Max(0, amount);
        if (type == CropType.Pumpkin) PumpkinSeeds += Mathf.Max(0, amount);
        Changed?.Invoke();
    }

    public bool TrySpendSeed(CropType type) => TryDeliverSeeds(type, 1);

    public bool TryDeliverSeeds(CropType type, int amount) => type switch
    {
        CropType.Corn => TryRemove(SaleItem.CornSeeds, amount),
        CropType.Pumpkin => TryRemove(SaleItem.PumpkinSeeds, amount),
        _ => false
    };

    private bool TryRemove(SaleItem item, int amount)
    {
        if (amount < 0 || StockForSale(item) < amount) return false;
        switch (item)
        {
            case SaleItem.Wood: Wood -= amount; break;
            case SaleItem.Fish: Fish -= amount; break;
            case SaleItem.Corn: Corn -= amount; break;
            case SaleItem.Pumpkin: Pumpkin -= amount; break;
            case SaleItem.Food: Food -= amount; break;
            case SaleItem.CornSeeds: CornSeeds -= amount; break;
            case SaleItem.PumpkinSeeds: PumpkinSeeds -= amount; break;
            default: return false;
        }
        Changed?.Invoke();
        return true;
    }

    public int Seeds(CropType type)
    {
        return type == CropType.Corn ? CornSeeds : type == CropType.Pumpkin ? PumpkinSeeds : 0;
    }
}
