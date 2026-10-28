using UnityEngine;
using UnityEngine.UI;

public sealed class ActiveMissionCard : MonoBehaviour
{
    [SerializeField] private Text description;
    [SerializeField] private Image itemIcon;
    [SerializeField] private Text quantityText;
    [SerializeField] private Text rewardText;

    public void Bind(DeliveryMission mission, int day, int stock, Sprite icon)
    {
        if (description != null) description.text = mission.title;
        if (itemIcon != null) itemIcon.sprite = icon;
        if (quantityText != null) quantityText.text = mission.quantity + "x " + ItemName(mission.item) +
            " · estoque " + stock;
        if (rewardText != null) rewardText.text = "+" + mission.coinReward + " moedas  +" +
            mission.reputationReward + " rep.  · dia " + mission.dueDay;
    }

    private static string ItemName(DeliveryItem item) => item switch
    {
        DeliveryItem.Wood => "madeira",
        DeliveryItem.Food => "comida",
        DeliveryItem.CornSeeds => "sementes de milho",
        DeliveryItem.Fish => "peixe",
        DeliveryItem.Corn => "milho",
        DeliveryItem.Pumpkin => "abóbora",
        _ => "sementes de abóbora"
    };
}
