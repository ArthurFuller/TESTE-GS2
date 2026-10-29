using UnityEngine;
using UnityEngine.UI;

public sealed class ShopBuyPanel : MonoBehaviour
{
    [SerializeField] private SeedMerchant merchant;
    [SerializeField] private ResourceBank resources;
    [SerializeField] private ShopQuantityRow cornRow;
    [SerializeField] private ShopQuantityRow pumpkinRow;
    [SerializeField] private ShopQuantityRow foodRow;
    [SerializeField] private Text totalText;
    [SerializeField] private Button buyButton;
    [SerializeField] private Button resetButton;
    private ShopQuantityRow[] rows;

    private void Awake()
    {
        rows = new[] { cornRow, pumpkinRow, foodRow };
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
        if (merchant == null || cornRow == null || pumpkinRow == null || foodRow == null) return;
        if (merchant.TryBuyCart(cornRow.Quantity, pumpkinRow.Quantity, foodRow.Quantity)) ResetSelection();
    }

    public void Refresh()
    {
        if (merchant == null || resources == null || cornRow == null || pumpkinRow == null || foodRow == null) return;
        int[] prices = {
            merchant.GetUnitPrice(SaleItem.CornSeeds),
            merchant.GetUnitPrice(SaleItem.PumpkinSeeds),
            merchant.GetUnitPrice(SaleItem.Food)
        };
        int budget = resources.Coins;
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i].SetPrice(prices[i]);
            rows[i].SetLimit(prices[i] > 0 ? budget / prices[i] : 0);
            budget -= rows[i].Quantity * prices[i];
        }
        int total = resources.Coins - budget;
        for (int i = 0; i < rows.Length; i++)
            rows[i].SetLimit(prices[i] > 0 ? rows[i].Quantity + budget / prices[i] : 0);
        if (totalText != null) totalText.text = "TOTAL: " + total + " moedas";
        if (buyButton != null) buyButton.interactable = total > 0 && merchant.IsAvailable;
    }
}
