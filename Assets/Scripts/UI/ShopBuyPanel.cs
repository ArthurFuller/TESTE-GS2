using UnityEngine;
using UnityEngine.UI;

public sealed class ShopBuyPanel : MonoBehaviour
{
    [SerializeField] private SeedMerchant merchant;
    [SerializeField] private ResourceBank resources;
    [SerializeField] private ShopQuantityRow cornRow;
    [SerializeField] private ShopQuantityRow pumpkinRow;
    [SerializeField] private ShopQuantityRow jerimumRow;
    [SerializeField] private ShopQuantityRow foodRow;
    [SerializeField] private ShopQuantityRow fenceRow;
    [SerializeField] private Text totalText;
    [SerializeField] private Button buyButton;
    [SerializeField] private Button resetButton;
    private ShopQuantityRow[] rows;

    private void Awake()
    {
        rows = new[] { cornRow, pumpkinRow, jerimumRow, foodRow, fenceRow };
        if (buyButton != null) buyButton.onClick.AddListener(Buy);
        if (resetButton != null) resetButton.onClick.AddListener(ResetSelection);
    }

    private void OnEnable()
    {
        if (resources != null) resources.Changed += Refresh;
        foreach (ShopQuantityRow row in rows)
            if (row != null) row.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (resources != null) resources.Changed -= Refresh;
        foreach (ShopQuantityRow row in rows)
            if (row != null) row.Changed -= Refresh;
    }

    public void ResetSelection()
    {
        foreach (ShopQuantityRow row in rows)
            if (row != null) row.ResetQuantity();
        Refresh();
    }

    public void Buy()
    {
        if (merchant == null || cornRow == null || pumpkinRow == null || jerimumRow == null ||
            foodRow == null || fenceRow == null) return;
        if (merchant.TryBuyCart(cornRow.Quantity, pumpkinRow.Quantity, jerimumRow.Quantity,
            foodRow.Quantity, fenceRow.Quantity)) ResetSelection();
    }

    public void Refresh()
    {
        if (merchant == null || resources == null || cornRow == null || pumpkinRow == null ||
            jerimumRow == null || foodRow == null || fenceRow == null) return;
        int[] prices = {
            merchant.GetUnitPrice(SaleItem.CornSeeds),
            merchant.GetUnitPrice(SaleItem.PumpkinSeeds),
            merchant.GetUnitPrice(SaleItem.JerimumSeeds),
            merchant.GetUnitPrice(SaleItem.Food),
            merchant.FenceCoinCost
        };
        int budget = resources.Coins;
        int woodBudget = resources.Wood;
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i].SetPrice(prices[i]);
            rows[i].SetWoodPrice(i == 4 ? merchant.FenceWoodCost : 0);
            int limit = prices[i] > 0 ? budget / prices[i] : 0;
            if (i == 4)
                limit = Mathf.Min(limit, merchant.FenceWoodCost > 0 ?
                    woodBudget / merchant.FenceWoodCost : int.MaxValue, merchant.FenceSlotsAvailable);
            rows[i].SetLimit(limit);
            budget -= rows[i].Quantity * prices[i];
            if (i == 4) woodBudget -= rows[i].Quantity * merchant.FenceWoodCost;
        }
        int total = resources.Coins - budget;
        for (int i = 0; i < rows.Length; i++)
        {
            int limit = prices[i] > 0 ? rows[i].Quantity + budget / prices[i] : 0;
            if (i == 4)
                limit = Mathf.Min(limit, rows[i].Quantity +
                    (merchant.FenceWoodCost > 0 ? woodBudget / merchant.FenceWoodCost : int.MaxValue),
                    merchant.FenceSlotsAvailable);
            rows[i].SetLimit(limit);
        }
        if (totalText != null) totalText.text = "TOTAL: " + total + " moedas" +
            (fenceRow.Quantity > 0 ? " + " + fenceRow.Quantity * merchant.FenceWoodCost + " madeiras" : "");
        if (buyButton != null) buyButton.interactable = total > 0 && merchant.IsAvailable;
    }
}
