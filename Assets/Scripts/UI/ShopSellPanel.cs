using UnityEngine;
using UnityEngine.UI;

public sealed class ShopSellPanel : MonoBehaviour
{
    [SerializeField] private InventoryTrading trading;
    [SerializeField] private ResourceBank resources;
    [SerializeField] private ResourceHud resourceHud;
    [SerializeField] private ShopQuantityRow[] rows;
    [SerializeField] private Text totalText;
    [SerializeField] private Button sellButton;
    [SerializeField] private Button resetButton;
    private static readonly SaleItem[] Items =
    {
        SaleItem.Wood, SaleItem.Fish, SaleItem.Corn,
        SaleItem.Pumpkin, SaleItem.Food, SaleItem.Jerimum,
        SaleItem.Milk, SaleItem.Eggs
    };

    private void Awake()
    {
        if (sellButton != null) sellButton.onClick.AddListener(Sell);
        if (resetButton != null) resetButton.onClick.AddListener(ResetSelection);
    }

    private void OnEnable()
    {
        if (resources != null) resources.Changed += RefreshDisplay;
        if (rows != null)
            foreach (ShopQuantityRow row in rows)
                if (row != null) row.Changed += RefreshDisplay;
        RefreshDisplay();
    }

    private void OnDisable()
    {
        if (resources != null) resources.Changed -= RefreshDisplay;
        if (rows != null)
            foreach (ShopQuantityRow row in rows)
                if (row != null) row.Changed -= RefreshDisplay;
    }

    public void ResetSelection()
    {
        if (rows == null) return;
        foreach (ShopQuantityRow row in rows)
            if (row != null) row.ResetQuantity();
        RefreshDisplay();
    }

    public void Sell()
    {
        if (trading == null || rows == null || rows.Length != Items.Length) return;
        int[] quantities = new int[(int)SaleItem.Eggs + 1];
        for (int i = 0; i < rows.Length; i++)
            quantities[(int)Items[i]] = rows[i] == null ? 0 : rows[i].Quantity;
        long coins = 0;
        for (int i = 0; i < rows.Length; i++)
            coins += (long)quantities[(int)Items[i]] * trading.GetPrice(Items[i]);
        if (trading.TrySellBatch(quantities))
        {
            AudioManager.Play(SoundCue.ShopSale);
            if (resourceHud != null && sellButton != null && coins > 0 && coins <= int.MaxValue)
                resourceHud.FlyFromRect(ResourceKind.Coins, (int)coins, sellButton.transform as RectTransform);
            ResetSelection();
        }
    }

    public void RefreshDisplay()
    {
        if (trading == null || rows == null) return;
        long total = 0;
        for (int i = 0; i < rows.Length && i < Items.Length; i++)
        {
            ShopQuantityRow row = rows[i];
            if (row == null) continue;
            SaleItem item = Items[i];
            int price = trading.GetPrice(item);
            row.SetPrice(price);
            row.SetStock(trading.GetStock(item));
            row.SetLimit(trading.GetStock(item));
            total += (long)row.Quantity * price;
        }
        if (totalText != null) totalText.text = "RECEBER: " + total + " moedas";
        if (sellButton != null) sellButton.interactable = total > 0;
    }
}
