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

    public bool TryDeliverFish(int amount)
    {
        if (amount < 0 || Fish < amount) return false;
        Fish -= amount;
        Changed?.Invoke();
        return true;
    }

    public bool TryDeliverCrop(CropType crop, int amount)
    {
        if (amount < 0) return false;
        if (crop == CropType.Corn && Corn >= amount) Corn -= amount;
        else if (crop == CropType.Pumpkin && Pumpkin >= amount) Pumpkin -= amount;
        else return false;
        Changed?.Invoke();
        return true;
    }

    public bool TrySell(SaleItem item, int price)
    {
        if (price < 0) return false;
        switch (item)
        {
            case SaleItem.Wood when Wood > 0: Wood--; break;
            case SaleItem.Fish when Fish > 0: Fish--; break;
            case SaleItem.Corn when Corn > 0: Corn--; break;
            case SaleItem.Pumpkin when Pumpkin > 0: Pumpkin--; break;
            case SaleItem.Food when Food > 0: Food--; break;
            case SaleItem.CornSeeds when CornSeeds > 0: CornSeeds--; break;
            case SaleItem.PumpkinSeeds when PumpkinSeeds > 0: PumpkinSeeds--; break;
            default: return false;
        }
        Coins += price;
        Changed?.Invoke();
        return true;
    }

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

    public bool TryDeliverWood(int amount)
    {
        if (amount < 0 || Wood < amount) return false;
        Wood -= amount;
        Changed?.Invoke();
        return true;
    }

    public bool TryDeliverFood(int amount)
    {
        if (amount < 0 || Food < amount) return false;
        Food -= amount;
        Changed?.Invoke();
        return true;
    }

    public bool TrySpendWood(int amount)
    {
        if (amount < 0 || Wood < amount)
            return false;
        Wood -= amount;
        Changed?.Invoke();
        return true;
    }

    public void AddSeeds(CropType type, int amount)
    {
        if (type == CropType.Corn) CornSeeds += Mathf.Max(0, amount);
        if (type == CropType.Pumpkin) PumpkinSeeds += Mathf.Max(0, amount);
        Changed?.Invoke();
    }

    public bool TrySpendSeed(CropType type)
    {
        if (type == CropType.Corn && CornSeeds > 0) CornSeeds--;
        else if (type == CropType.Pumpkin && PumpkinSeeds > 0) PumpkinSeeds--;
        else return false;
        Changed?.Invoke();
        return true;
    }

    public bool TryDeliverSeeds(CropType type, int amount)
    {
        if (amount < 0 || Seeds(type) < amount) return false;
        if (type == CropType.Corn) CornSeeds -= amount;
        else if (type == CropType.Pumpkin) PumpkinSeeds -= amount;
        else return false;
        Changed?.Invoke();
        return true;
    }

    public int Seeds(CropType type)
    {
        return type == CropType.Corn ? CornSeeds : type == CropType.Pumpkin ? PumpkinSeeds : 0;
    }
}
