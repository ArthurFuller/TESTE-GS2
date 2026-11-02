using UnityEngine;
using UnityEngine.UI;

public sealed class ShopTabs : MonoBehaviour
{
    [SerializeField] private CanvasGroup buyPage;
    [SerializeField] private CanvasGroup sellPage;
    [SerializeField] private CanvasGroup ordersPage;
    [SerializeField] private Button buyTab;
    [SerializeField] private Button sellTab;
    [SerializeField] private Button ordersTab;
    [SerializeField] private ShopSellPanel sellPanel;

    public void ShowDefault(bool marketOpen)
    {
        if (buyTab != null) buyTab.interactable = marketOpen;
        if (sellTab != null) sellTab.interactable = marketOpen;
        Select(marketOpen ? buyPage : ordersPage);
    }

    public void ShowBuy() => Select(buyPage);
    public void ShowSell()
    {
        if (sellPanel != null) sellPanel.RefreshDisplay();
        Select(sellPage);
    }
    public void ShowOrders() => Select(ordersPage);

    private void Select(CanvasGroup selected)
    {
        SetPage(buyPage, selected == buyPage);
        SetPage(sellPage, selected == sellPage);
        SetPage(ordersPage, selected == ordersPage);
    }

    private static void SetPage(CanvasGroup page, bool active)
    {
        if (page == null) return;
        page.alpha = active ? 1 : 0;
        page.interactable = active;
        page.blocksRaycasts = active;
    }
}
