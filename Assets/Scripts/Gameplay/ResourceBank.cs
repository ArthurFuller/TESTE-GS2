using System;
using UnityEngine;

public sealed class ResourceBank : MonoBehaviour
{
    [Header("Recursos iniciais")]
    [SerializeField, Min(0), InspectorName("Madeira")] private int startingWood;
    [SerializeField, Min(0), InspectorName("Comida pronta")] private int startingFood = 2;
    [SerializeField, Min(0), InspectorName("Moedas")] private int startingCoins = 2;
    [SerializeField, Min(0), InspectorName("Peixes")] private int startingFish;
    [SerializeField, Min(0), InspectorName("Milho colhido")] private int startingCorn;
    [SerializeField, Min(0), InspectorName("Macaxeira colhida")] private int startingPumpkin;
    [SerializeField, Min(0), InspectorName("Jerimum colhido")] private int startingJerimum;
    [SerializeField, Min(0), InspectorName("Leite")] private int startingMilk;
    [SerializeField, Min(0), InspectorName("Ovos")] private int startingEggs;
    [SerializeField, Min(0), InspectorName("Sementes de milho")] private int startingCornSeeds;
    [SerializeField, Min(0), InspectorName("Sementes de macaxeira")] private int startingPumpkinSeeds;
    [SerializeField, Min(0), InspectorName("Sementes de jerimum")] private int startingJerimumSeeds;

    public int Wood { get; private set; }
    public int Food { get; private set; }
    public int Coins { get; private set; }
    public int CornSeeds { get; private set; }
    public int PumpkinSeeds { get; private set; }
    public int JerimumSeeds { get; private set; }
    public int Fish { get; private set; }
    public int Corn { get; private set; }
    public int Pumpkin { get; private set; }
    public int Jerimum { get; private set; }
    public int Milk { get; private set; }
    public int Eggs { get; private set; }
    public event Action Changed;

    private void Awake()
    {
        Wood = startingWood;
        Food = startingFood;
        Coins = startingCoins;
        CornSeeds = startingCornSeeds;
        PumpkinSeeds = startingPumpkinSeeds;
        JerimumSeeds = startingJerimumSeeds;
        Fish = startingFish;
        Corn = startingCorn;
        Pumpkin = startingPumpkin;
        Jerimum = startingJerimum;
        Milk = startingMilk;
        Eggs = startingEggs;
    }

    public void AddWood(int amount)
    {
        Wood += Mathf.Max(0, amount);
        Changed?.Invoke();
    }

    public void AddFood(int amount)
    {
        amount = Mathf.Max(0, amount);
        Food += amount;
        Changed?.Invoke();
    }

    public void AddFish(int amount) { Fish += Mathf.Max(0, amount); Changed?.Invoke(); }
    public void AddAnimalProducts(int milk, int eggs)
    {
        Milk += Mathf.Max(0, milk);
        Eggs += Mathf.Max(0, eggs);
        Changed?.Invoke();
    }

    public bool TryDeliverAnimalProduct(SaleItem item)
    {
        if (item == SaleItem.Milk && Milk > 0) Milk--;
        else if (item == SaleItem.Eggs && Eggs > 0) Eggs--;
        else return false;
        Changed?.Invoke();
        return true;
    }
    public void AddCrop(CropType crop, int amount)
    {
        if (crop == CropType.Corn) Corn += Mathf.Max(0, amount);
        else if (crop == CropType.Pumpkin) Pumpkin += Mathf.Max(0, amount);
        else if (crop == CropType.Jerimum) Jerimum += Mathf.Max(0, amount);
        Changed?.Invoke();
    }

    public bool TryPrepareFood(int fish, int corn, int pumpkin, int jerimum, int milk, int eggs,
        int recipeYield, out int preparedFood)
    {
        preparedFood = 0;
        if (fish < 0 || corn < 0 || pumpkin < 0 || jerimum < 0 || milk < 0 || eggs < 0 ||
            fish > Fish || corn > Corn || pumpkin > Pumpkin || jerimum > Jerimum ||
            milk > Milk || eggs > Eggs ||
            (long)fish + corn + pumpkin + jerimum + milk + eggs == 0 || recipeYield <= 0 ||
            recipeYield > int.MaxValue - Food) return false;
        Fish -= fish;
        Corn -= corn;
        Pumpkin -= pumpkin;
        Jerimum -= jerimum;
        Milk -= milk;
        Eggs -= eggs;
        preparedFood = recipeYield;
        Changed?.Invoke();
        return true;
    }

    public bool TryDeliverFish(int amount) => TryRemove(SaleItem.Fish, amount);
    public bool TryDeliverCrop(CropType crop, int amount) => crop switch
    {
        CropType.Corn => TryRemove(SaleItem.Corn, amount),
        CropType.Pumpkin => TryRemove(SaleItem.Pumpkin, amount),
        CropType.Jerimum => TryRemove(SaleItem.Jerimum, amount),
        _ => false
    };

    public bool TrySellBatch(int[] quantities, int[] prices)
    {
        int itemCount = (int)SaleItem.Eggs + 1;
        if (quantities == null || prices == null || quantities.Length != itemCount ||
            prices.Length != itemCount) return false;
        if (quantities[(int)SaleItem.CornSeeds] != 0 ||
            quantities[(int)SaleItem.PumpkinSeeds] != 0 ||
            quantities[(int)SaleItem.JerimumSeeds] != 0) return false;
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
        RemoveFood(quantities[(int)SaleItem.Food]);
        Jerimum -= quantities[(int)SaleItem.Jerimum];
        Milk -= quantities[(int)SaleItem.Milk];
        Eggs -= quantities[(int)SaleItem.Eggs];
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
        SaleItem.PumpkinSeeds => PumpkinSeeds,
        SaleItem.Jerimum => Jerimum,
        SaleItem.JerimumSeeds => JerimumSeeds,
        SaleItem.Milk => Milk,
        SaleItem.Eggs => Eggs,
        _ => 0
    };

    public int ConsumeFood(int amount)
    {
        int consumed = Mathf.Min(Food, Mathf.Max(0, amount));
        RemoveFood(consumed);
        if (consumed > 0) Changed?.Invoke();
        return consumed;
    }

    private void RemoveFood(int amount)
    {
        Food -= amount;
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
        if (type == CropType.Jerimum) JerimumSeeds += Mathf.Max(0, amount);
        Changed?.Invoke();
    }

    public bool TrySpendSeed(CropType type) => TryDeliverSeeds(type, 1);

    public bool TryDeliverSeeds(CropType type, int amount) => type switch
    {
        CropType.Corn => TryRemove(SaleItem.CornSeeds, amount),
        CropType.Pumpkin => TryRemove(SaleItem.PumpkinSeeds, amount),
        CropType.Jerimum => TryRemove(SaleItem.JerimumSeeds, amount),
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
            case SaleItem.Food: RemoveFood(amount); break;
            case SaleItem.CornSeeds: CornSeeds -= amount; break;
            case SaleItem.PumpkinSeeds: PumpkinSeeds -= amount; break;
            case SaleItem.Jerimum: Jerimum -= amount; break;
            case SaleItem.JerimumSeeds: JerimumSeeds -= amount; break;
            default: return false;
        }
        Changed?.Invoke();
        return true;
    }

    public int Seeds(CropType type)
    {
        return type == CropType.Corn ? CornSeeds : type == CropType.Pumpkin ? PumpkinSeeds :
            type == CropType.Jerimum ? JerimumSeeds : 0;
    }
}
