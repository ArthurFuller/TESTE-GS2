using UnityEngine;
using UnityEngine.UI;

public sealed class ShopSellPanel : MonoBehaviour
{
    [SerializeField] private InventoryTrading trading;
    [SerializeField] private ResourceBank resources;
    [SerializeField] private Text[] itemLabels;
    [SerializeField] private Button[] itemButtons;
    [SerializeField] private Text selectedText;
    [SerializeField] private Button sellButton;
    private SaleItem selected;

    private static readonly string[] Names =
    {
        "Madeira", "Peixe", "Milho", "Abóbora", "Comida", "Sementes de milho", "Sementes de abóbora"
    };

    private void OnEnable()
    {
        if (resources != null) resources.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (resources != null) resources.Changed -= Refresh;
    }

    private void Start() => Refresh();
    public void RefreshDisplay() => Refresh();

    public void SelectWood() => Select(SaleItem.Wood);
    public void SelectFish() => Select(SaleItem.Fish);
    public void SelectCorn() => Select(SaleItem.Corn);
    public void SelectPumpkin() => Select(SaleItem.Pumpkin);
    public void SelectFood() => Select(SaleItem.Food);
    public void SelectCornSeeds() => Select(SaleItem.CornSeeds);
    public void SelectPumpkinSeeds() => Select(SaleItem.PumpkinSeeds);

    public void SellSelected()
    {
        if (trading != null) trading.TrySellOne(selected);
        Refresh();
    }

    private void Select(SaleItem item)
    {
        selected = item;
        Refresh();
    }

    private void Refresh()
    {
        if (trading == null) return;
        for (int i = 0; i < Names.Length; i++)
        {
            var item = (SaleItem)i;
            int stock = trading.GetStock(item);
            if (itemLabels != null && i < itemLabels.Length && itemLabels[i] != null)
                itemLabels[i].text = Names[i] + "   " + stock + " un.   " + trading.GetPrice(item) + " moedas";
            if (itemButtons != null && i < itemButtons.Length && itemButtons[i] != null)
                itemButtons[i].interactable = stock > 0;
        }
        if (selectedText != null)
            selectedText.text = "Selecionado: " + Names[(int)selected] + " • " +
                trading.GetPrice(selected) + " moedas por unidade";
        if (sellButton != null) sellButton.interactable = trading.GetStock(selected) > 0;
    }
}
