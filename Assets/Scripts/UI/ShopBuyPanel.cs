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
    private ShopQuantityRow houseRow;
    [SerializeField] private Text totalText;
    [SerializeField] private Button buyButton;
    [SerializeField] private Button resetButton;
    private ShopQuantityRow[] rows;

    private void Awake()
    {
        if (fenceRow != null)
        {
            houseRow = Instantiate(fenceRow, fenceRow.transform.parent);
            houseRow.name = "Item - Casa";
            houseRow.SetItemName("Casa");
            Transform fenceIcon = houseRow.transform.Find("Ícone de Cerca");
            if (fenceIcon != null) fenceIcon.gameObject.SetActive(false);
            RectTransform rect = (RectTransform)houseRow.transform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -112f);
            RectTransform content = (RectTransform)rect.parent;
            content.sizeDelta = new Vector2(content.sizeDelta.x, 168f);
        }
        if (cornRow != null) cornRow.gameObject.SetActive(false);
        if (pumpkinRow != null) pumpkinRow.gameObject.SetActive(false);
        if (jerimumRow != null) jerimumRow.gameObject.SetActive(false);
        if (foodRow != null)
            ((RectTransform)foodRow.transform).anchoredPosition = Vector2.zero;
        if (fenceRow != null)
            ((RectTransform)fenceRow.transform).anchoredPosition = new Vector2(0f, -56f);
        rows = new[] { foodRow, fenceRow, houseRow };
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
        if (merchant == null || foodRow == null || fenceRow == null || houseRow == null) return;
        if (merchant.TryBuyCart(0, 0, 0, foodRow.Quantity, fenceRow.Quantity,
            houseRow.Quantity)) ResetSelection();
    }

    public void Refresh()
    {
        if (merchant == null || resources == null || foodRow == null || fenceRow == null ||
            houseRow == null) return;
        int[] prices = {
            merchant.GetUnitPrice(SaleItem.Food),
            merchant.FenceCoinCost,
            merchant.HouseCoinCost
        };
        int budget = resources.Coins;
        int woodBudget = resources.Wood;
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i].SetPrice(prices[i]);
            rows[i].SetWoodPrice(i == 1 ? merchant.FenceWoodCost :
                i == 2 ? merchant.HouseWoodCost : 0);
            int limit = prices[i] > 0 ? budget / prices[i] : 0;
            if (i == 1)
                limit = Mathf.Min(limit, merchant.FenceWoodCost > 0 ?
                    woodBudget / merchant.FenceWoodCost : int.MaxValue, merchant.FenceSlotsAvailable);
            if (i == 2)
                limit = Mathf.Min(limit, merchant.HouseWoodCost > 0 ?
                    woodBudget / merchant.HouseWoodCost : int.MaxValue, merchant.HouseSlotsAvailable);
            rows[i].SetLimit(limit);
            budget -= rows[i].Quantity * prices[i];
            woodBudget -= rows[i].Quantity * (i == 1 ? merchant.FenceWoodCost :
                i == 2 ? merchant.HouseWoodCost : 0);
        }
        int total = resources.Coins - budget;
        for (int i = 0; i < rows.Length; i++)
        {
            int limit = prices[i] > 0 ? rows[i].Quantity + budget / prices[i] : 0;
            if (i == 1)
                limit = Mathf.Min(limit, rows[i].Quantity +
                    (merchant.FenceWoodCost > 0 ? woodBudget / merchant.FenceWoodCost : int.MaxValue),
                    merchant.FenceSlotsAvailable);
            if (i == 2)
                limit = Mathf.Min(limit, rows[i].Quantity +
                    (merchant.HouseWoodCost > 0 ? woodBudget / merchant.HouseWoodCost : int.MaxValue),
                    merchant.HouseSlotsAvailable);
            rows[i].SetLimit(limit);
        }
        if (totalText != null) totalText.text = "TOTAL: " + total + " moedas" +
            (fenceRow.Quantity + houseRow.Quantity > 0 ? " + " +
                (fenceRow.Quantity * merchant.FenceWoodCost +
                 houseRow.Quantity * merchant.HouseWoodCost) + " madeiras" : "");
        if (buyButton != null) buyButton.interactable = total > 0 && merchant.IsAvailable;
    }
}
