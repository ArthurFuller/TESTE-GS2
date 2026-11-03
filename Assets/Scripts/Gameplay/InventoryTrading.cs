using UnityEngine;

public sealed class InventoryTrading : MonoBehaviour
{
    [SerializeField] private ResourceBank resources;
    [SerializeField] private SeedMerchant merchant;
    [SerializeField] private GameFeedback feedback;
    [Header("Preços provisórios por unidade")]
    [SerializeField, Min(0)] private int woodPrice = 1;
    [SerializeField, Min(0)] private int fishPrice = 2;
    [SerializeField, Min(0)] private int cornPrice = 1;
    [SerializeField, Min(0)] private int pumpkinPrice = 2;
    [SerializeField, Min(0)] private int foodPrice = 1;
    [SerializeField, Min(0)] private int cornSeedPrice = 1;
    [SerializeField, Min(0)] private int pumpkinSeedPrice = 2;
    [SerializeField, Min(0)] private int jerimumPrice = 2;
    [SerializeField, Min(0)] private int jerimumSeedPrice = 2;
    [Header("Comida por unidade consumida - provisório")]
    [SerializeField, Min(0)] private int foodPerFish = 2;
    [SerializeField, Min(0)] private int foodPerCorn = 1;
    [SerializeField, Min(0)] private int foodPerPumpkin = 1;
    [SerializeField, Min(0)] private int foodPerJerimum = 1;

    public int GetStock(SaleItem item) => resources == null ? 0 : item switch
    {
        SaleItem.Wood => resources.Wood,
        SaleItem.Fish => resources.Fish,
        SaleItem.Corn => resources.Corn,
        SaleItem.Pumpkin => resources.Pumpkin,
        SaleItem.Jerimum => resources.Jerimum,
        SaleItem.Food => resources.Food,
        SaleItem.CornSeeds => resources.CornSeeds,
        SaleItem.PumpkinSeeds => resources.PumpkinSeeds,
        _ => resources.JerimumSeeds
    };

    public int GetPrice(SaleItem item) => item switch
    {
        SaleItem.Wood => woodPrice,
        SaleItem.Fish => fishPrice,
        SaleItem.Corn => cornPrice,
        SaleItem.Pumpkin => pumpkinPrice,
        SaleItem.Jerimum => jerimumPrice,
        SaleItem.Food => foodPrice,
        SaleItem.CornSeeds => cornSeedPrice,
        SaleItem.PumpkinSeeds => pumpkinSeedPrice,
        _ => jerimumSeedPrice
    };

    public bool TrySellBatch(int[] quantities)
    {
        if (merchant == null || !merchant.IsAvailable || !merchant.IsOpen || resources == null)
        {
            if (feedback != null) feedback.ShowMessage("Visite a barraca aberta para vender.");
            return false;
        }
        int[] prices = new int[9];
        for (int i = 0; i < prices.Length; i++) prices[i] = GetPrice((SaleItem)i);
        if (!resources.TrySellBatch(quantities, prices))
        {
            if (feedback != null) feedback.ShowMessage("Confira as quantidades escolhidas para venda.");
            return false;
        }
        if (feedback != null) feedback.ShowMessage("Venda concluída.");
        return true;
    }

    public bool TryPrepare(int fish, int corn, int pumpkin, int jerimum, bool plus, out int food)
    {
        food = 0;
        return resources != null && resources.TryPrepareFood(fish, corn, pumpkin, jerimum,
            foodPerFish + (plus ? 1 : 0), foodPerCorn + (plus ? 1 : 0),
            foodPerPumpkin + (plus ? 1 : 0), foodPerJerimum + (plus ? 1 : 0), out food);
    }

    public int EstimateFood(int fish, int corn, int pumpkin, int jerimum, bool plus = false) =>
        fish * (foodPerFish + (plus ? 1 : 0)) +
        corn * (foodPerCorn + (plus ? 1 : 0)) +
        pumpkin * (foodPerPumpkin + (plus ? 1 : 0)) +
        jerimum * (foodPerJerimum + (plus ? 1 : 0));
}
