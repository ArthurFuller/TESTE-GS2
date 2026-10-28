using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum DeliveryItem { Wood, Food, CornSeeds, PumpkinSeeds, Fish, Corn, Pumpkin }

[Serializable]
public sealed class DeliveryMission
{
    public string title;
    public DeliveryItem item;
    public int quantity;
    public int dueDay;
    public int reputationReward;
    public int coinReward;
    public bool accepted;
}

public sealed class DeliveryMissionBoard : MonoBehaviour
{
    [SerializeField] private ResourceBank resources;
    [SerializeField] private GameFeedback feedback;
    [SerializeField] private Text orderText;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button deliverButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private ActiveMissionsPanel activePanel;
    [SerializeField, Range(1, 5)] private int minimumOffers = 2;
    [SerializeField, Range(1, 5)] private int maximumOffers = 3;
    [SerializeField, Min(1)] private int campaignDays = 12;
    [SerializeField] private int lowReputationThreshold = 0;
    [SerializeField] private int highReputationThreshold = 300;
    [SerializeField, Range(0, 3)] private int returningOffersPerMarket = 1;

    private readonly List<DeliveryMission> missions = new List<DeliveryMission>();
    private readonly List<DeliveryMission> returningOffers = new List<DeliveryMission>();
    private int currentDay = 1;
    private int selectedIndex;
    private int lastGeneratedDay;
    private bool marketOpen;

    private void OnEnable()
    {
        if (resources != null) resources.Changed += Refresh;
    }

    private void OnDisable()
    {
        if (resources != null) resources.Changed -= Refresh;
    }

    public void SetDay(int day, bool isMarketDay)
    {
        currentDay = day;
        marketOpen = isMarketDay;
        foreach (DeliveryMission mission in missions)
            if (!mission.accepted && !returningOffers.Contains(mission)) returningOffers.Add(mission);
        missions.RemoveAll(m => !m.accepted);
        int expired = missions.RemoveAll(m => day > m.dueDay);
        if (expired > 0 && feedback != null) feedback.ShowMessage("Pedidos com prazo encerrado foram removidos.");
        if (marketOpen && lastGeneratedDay != day)
        {
            GenerateOffers();
            lastGeneratedDay = day;
        }
        selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, missions.Count - 1));
        Refresh();
    }

    public void Next()
    {
        if (missions.Count == 0) return;
        selectedIndex = (selectedIndex + 1) % missions.Count;
        Refresh();
    }

    public void Accept()
    {
        if (!marketOpen || missions.Count == 0) return;
        DeliveryMission mission = missions[selectedIndex];
        if (mission.accepted) return;
        mission.accepted = true;
        if (feedback != null) feedback.ShowMessage("Pedido aceito: " + mission.title);
        Refresh();
    }

    public void Deliver()
    {
        DeliverAt(selectedIndex);
    }

    public void DeliverAt(int index)
    {
        if (index < 0 || index >= missions.Count || resources == null) return;
        DeliveryMission mission = missions[index];
        if (!mission.accepted || currentDay > mission.dueDay) return;
        bool delivered = mission.item switch
        {
            DeliveryItem.Wood => resources.TryDeliverWood(mission.quantity),
            DeliveryItem.Food => resources.TryDeliverFood(mission.quantity),
            DeliveryItem.CornSeeds => resources.TryDeliverSeeds(CropType.Corn, mission.quantity),
            DeliveryItem.PumpkinSeeds => resources.TryDeliverSeeds(CropType.Pumpkin, mission.quantity),
            DeliveryItem.Fish => resources.TryDeliverFish(mission.quantity),
            DeliveryItem.Corn => resources.TryDeliverCrop(CropType.Corn, mission.quantity),
            DeliveryItem.Pumpkin => resources.TryDeliverCrop(CropType.Pumpkin, mission.quantity),
            _ => false
        };
        if (!delivered)
        {
            if (feedback != null) feedback.ShowMessage("Ainda faltam itens para a entrega.");
            return;
        }
        resources.AddReputation(mission.reputationReward);
        resources.AddCoins(mission.coinReward);
        if (feedback != null) feedback.ShowMessage("Entrega concluída! +" + mission.reputationReward +
            " reputação, +" + mission.coinReward + " moedas.");
        missions.RemoveAt(index);
        selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, missions.Count - 1));
        Refresh();
    }

    private void GenerateOffers()
    {
        int tier = resources.Reputation < lowReputationThreshold ? 0 :
            resources.Reputation >= highReputationThreshold ? 2 : 1;
        var pool = new List<DeliveryItem> { DeliveryItem.Wood, DeliveryItem.Food, DeliveryItem.CornSeeds,
            DeliveryItem.Fish, DeliveryItem.Corn };
        if (tier > 0)
        {
            pool.Add(DeliveryItem.PumpkinSeeds);
            pool.Add(DeliveryItem.Pumpkin);
        }
        int count = Mathf.Clamp(UnityEngine.Random.Range(minimumOffers, maximumOffers + 1), 1, pool.Count);
        int returning = Mathf.Min(returningOffersPerMarket, Mathf.Min(count, returningOffers.Count));
        for (int i = 0; i < returning; i++)
        {
            int pick = UnityEngine.Random.Range(0, returningOffers.Count);
            DeliveryMission offer = returningOffers[pick];
            returningOffers.RemoveAt(pick);
            offer.dueDay = Mathf.Min(campaignDays, currentDay + (tier == 0 ? 3 : 2));
            missions.Add(offer);
            pool.Remove(offer.item);
        }
        count -= returning;
        for (int i = 0; i < count; i++)
        {
            int pick = UnityEngine.Random.Range(0, pool.Count);
            DeliveryItem item = pool[pick];
            pool.RemoveAt(pick);
            int quantity = QuantityFor(item, tier);
            int duration = tier == 0 ? 4 : 3;
            missions.Add(new DeliveryMission
            {
                title = RandomTitle(item),
                item = item,
                quantity = quantity,
                dueDay = Mathf.Min(campaignDays, currentDay + duration - 1),
                reputationReward = 50 + quantity * 25 + tier * 25,
                coinReward = 2 + quantity + tier
            });
        }
    }

    private static int QuantityFor(DeliveryItem item, int tier)
    {
        if (item == DeliveryItem.Wood) return UnityEngine.Random.Range(1 + tier, 3 + tier);
        if (item == DeliveryItem.Food) return UnityEngine.Random.Range(2 + tier, 4 + tier);
        if (item == DeliveryItem.CornSeeds) return tier == 0 ? 1 : UnityEngine.Random.Range(1, 2 + tier);
        if (item == DeliveryItem.Fish) return UnityEngine.Random.Range(1 + tier, 3 + tier);
        if (item == DeliveryItem.Corn || item == DeliveryItem.Pumpkin)
            return UnityEngine.Random.Range(1 + tier, 3 + tier);
        return tier == 2 ? 2 : 1;
    }

    private static string RandomTitle(DeliveryItem item)
    {
        string[] titles = item switch
        {
            DeliveryItem.Wood => new[] { "Reparos na comunidade", "Lenha para as famílias", "Madeira para o abrigo" },
            DeliveryItem.Food => new[] { "Alimentos para a feira", "Provisões da comunidade", "Comida para os viajantes" },
            DeliveryItem.CornSeeds => new[] { "Milho para o vizinho", "Sementes para o roçado" },
            DeliveryItem.Fish => new[] { "Peixes para a comunidade", "Encomenda de peixe" },
            DeliveryItem.Corn => new[] { "Milho para a feira", "Colheita encomendada" },
            DeliveryItem.Pumpkin => new[] { "Abóboras da comunidade", "Abóboras para a feira" },
            _ => new[] { "Plantio de abóbora", "Sementes para a feira" }
        };
        return titles[UnityEngine.Random.Range(0, titles.Length)];
    }

    private int InStock(DeliveryItem item) => item switch
    {
        DeliveryItem.Wood => resources.Wood,
        DeliveryItem.Food => resources.Food,
        DeliveryItem.CornSeeds => resources.CornSeeds,
        DeliveryItem.PumpkinSeeds => resources.PumpkinSeeds,
        DeliveryItem.Fish => resources.Fish,
        DeliveryItem.Corn => resources.Corn,
        DeliveryItem.Pumpkin => resources.Pumpkin,
        _ => 0
    };

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

    private void Refresh()
    {
        if (resources == null) return;
        if (activePanel != null) activePanel.Refresh(missions, currentDay, resources);
        if (missions.Count == 0)
        {
            if (orderText != null) orderText.text = "Nenhum pedido disponível.\nNovas ofertas chegam com a barraca.";
            if (acceptButton != null) acceptButton.interactable = false;
            if (deliverButton != null) deliverButton.interactable = false;
            if (nextButton != null) nextButton.interactable = false;
            return;
        }
        DeliveryMission selected = missions[selectedIndex];
        int stock = InStock(selected.item);
        if (orderText != null)
            orderText.text = "PEDIDO " + (selectedIndex + 1) + " / " + missions.Count + "\n" +
                selected.title + "\nEntregar " + selected.quantity + " " + ItemName(selected.item) +
                " (estoque: " + stock + ")\nPrazo: dia " + selected.dueDay +
                " • " + (selected.dueDay - currentDay + 1) + " dia(s)\nRecompensa: +" +
                selected.reputationReward + " reputação, +" + selected.coinReward + " moedas\n" +
                (selected.accepted ? "Aceito: entregue aqui." : "Disponível para aceitar.");
        if (acceptButton != null) acceptButton.interactable = marketOpen && !selected.accepted;
        if (deliverButton != null) deliverButton.interactable = selected.accepted && stock >= selected.quantity;
        if (nextButton != null) nextButton.interactable = missions.Count > 1;
    }
}
