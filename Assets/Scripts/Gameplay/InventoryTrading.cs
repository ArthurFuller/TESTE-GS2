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
    [Header("Comida por unidade consumida - provisório")]
    [SerializeField, Min(0)] private int foodPerFish = 2;
    [SerializeField, Min(0)] private int foodPerCorn = 1;
    [SerializeField, Min(0)] private int foodPerPumpkin = 1;

    public int GetStock(SaleItem item) => resources == null ? 0 : item switch
    {
        SaleItem.Wood => resources.Wood,
        SaleItem.Fish => resources.Fish,
        SaleItem.Corn => resources.Corn,
        SaleItem.Pumpkin => resources.Pumpkin,
        SaleItem.Food => resources.Food,
        SaleItem.CornSeeds => resources.CornSeeds,
        _ => resources.PumpkinSeeds
    };

    public int GetPrice(SaleItem item) => item switch
    {
        SaleItem.Wood => woodPrice,
        SaleItem.Fish => fishPrice,
        SaleItem.Corn => cornPrice,
        SaleItem.Pumpkin => pumpkinPrice,
        SaleItem.Food => foodPrice,
        SaleItem.CornSeeds => cornSeedPrice,
        _ => pumpkinSeedPrice
    };

    public bool TrySellOne(SaleItem item)
    {
        if (merchant == null || !merchant.IsAvailable || !merchant.IsOpen)
        {
            if (feedback != null) feedback.ShowMessage("Visite a barraca aberta para vender.");
            return false;
        }
        int price = GetPrice(item);
        if (resources == null || !resources.TrySell(item, price))
        {
            if (feedback != null) feedback.ShowMessage("Item indisponível no estoque.");
            return false;
        }
        if (feedback != null) feedback.ShowMessage("Venda concluída: +" + price + " moeda(s).");
        return true;
    }

    public bool TryPrepare(int fish, int corn, int pumpkin, out int food)
    {
        food = 0;
        return resources != null && resources.TryPrepareFood(fish, corn, pumpkin, foodPerFish, foodPerCorn,
            foodPerPumpkin, out food);
    }

    public int EstimateFood(int fish, int corn, int pumpkin) => fish * foodPerFish +
        corn * foodPerCorn + pumpkin * foodPerPumpkin;
}
