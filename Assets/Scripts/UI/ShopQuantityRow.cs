using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class ShopQuantityRow : MonoBehaviour
{
    [SerializeField] private Text nameText;
    [SerializeField] private Text quantityText;
    [SerializeField] private Text amountText;
    [SerializeField] private Text unitPriceText;
    [SerializeField] private Button decreaseButton;
    [SerializeField] private Button increaseButton;
    [SerializeField] private CanvasGroup rowGroup;
    [SerializeField] private string itemName;

    private int quantity;
    private int limit;
    private int unitPrice;
    private int stock = -1;

    public event Action Changed;
    public int Quantity => quantity;

    private void Awake()
    {
        if (decreaseButton != null) decreaseButton.onClick.AddListener(Decrease);
        if (increaseButton != null) increaseButton.onClick.AddListener(Increase);
        Refresh();
    }

    public void SetPrice(int price) { unitPrice = Mathf.Max(0, price); Refresh(); }
    public void SetStock(int available) { stock = available; Refresh(); }
    public void SetLimit(int maximum)
    {
        limit = Mathf.Max(0, maximum);
        quantity = Mathf.Min(quantity, limit);
        Refresh();
    }

    public void ResetQuantity()
    {
        quantity = 0;
        Refresh();
        Changed?.Invoke();
    }

    private void Increase()
    {
        if (quantity >= limit) return;
        quantity++;
        Refresh();
        Changed?.Invoke();
    }

    private void Decrease()
    {
        if (quantity <= 0) return;
        quantity--;
        Refresh();
        Changed?.Invoke();
    }

    private void Refresh()
    {
        if (nameText != null)
            nameText.text = stock < 0 ? itemName : itemName + " · " + stock + " no estoque";
        if (quantityText != null) quantityText.text = quantity.ToString();
        if (amountText != null) amountText.text = ((long)quantity * unitPrice).ToString();
        if (unitPriceText != null)
            unitPriceText.text = unitPrice + (unitPrice == 1 ? " moeda/un." : " moedas/un.");
        if (decreaseButton != null) decreaseButton.interactable = quantity > 0;
        if (increaseButton != null) increaseButton.interactable = quantity < limit;
        if (rowGroup != null) rowGroup.alpha = limit == 0 ? 0.42f : 1f;
    }
}
