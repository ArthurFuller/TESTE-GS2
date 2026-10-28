using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum DeliveryItem { Wood, Food, CornSeeds, PumpkinSeeds }

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
    [SerializeField] private Text hudText;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button deliverButton;
    [SerializeField] private Button nextButton;
    [SerializeField, Range(1, 5)] private int minimumOffers = 2;
    [SerializeField, Range(1, 5)] private int maximumOffers = 3;
    [SerializeField, Min(1)] private int campaignDays = 12;
    [SerializeField] private int lowReputationThreshold = 0;
    [SerializeField] private int highReputationThreshold = 300;

    private readonly List<DeliveryMission> missions = new List<DeliveryMission>();
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
        int expired = missions.RemoveAll(m => day > m.dueDay || (!m.accepted && day > lastGeneratedDay));
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
        if (missions.Count == 0 || resources == null) return;
        DeliveryMission mission = missions[selectedIndex];
        if (!mission.accepted || currentDay > mission.dueDay) return;
        bool delivered = mission.item switch
        {
            DeliveryItem.Wood => resources.TryDeliverWood(mission.quantity),
            DeliveryItem.Food => resources.TryDeliverFood(mission.quantity),
            DeliveryItem.CornSeeds => resources.TryDeliverSeeds(CropType.Corn, mission.quantity),
            DeliveryItem.PumpkinSeeds => resources.TryDeliverSeeds(CropType.Pumpkin, mission.quantity),
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
        missions.RemoveAt(selectedIndex);
        selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, missions.Count - 1));
        Refresh();
    }

    private void GenerateOffers()
    {
        int tier = resources.Reputation < lowReputationThreshold ? 0 :
            resources.Reputation >= highReputationThreshold ? 2 : 1;
        var pool = new List<DeliveryItem> { DeliveryItem.Wood, DeliveryItem.Food, DeliveryItem.CornSeeds };
        if (tier > 0) pool.Add(DeliveryItem.PumpkinSeeds);
        int count = Mathf.Clamp(UnityEngine.Random.Range(minimumOffers, maximumOffers + 1), 1, pool.Count);
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
        return tier == 2 ? 2 : 1;
    }

    private static string RandomTitle(DeliveryItem item)
    {
        string[] titles = item switch
        {
            DeliveryItem.Wood => new[] { "Reparos na comunidade", "Lenha para as famílias", "Madeira para o abrigo" },
            DeliveryItem.Food => new[] { "Alimentos para a feira", "Provisões da comunidade", "Comida para os viajantes" },
            DeliveryItem.CornSeeds => new[] { "Milho para o vizinho", "Sementes para o roçado" },
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
        _ => 0
    };

    private static string ItemName(DeliveryItem item) => item switch
    {
        DeliveryItem.Wood => "madeira",
        DeliveryItem.Food => "comida",
        DeliveryItem.CornSeeds => "sementes de milho",
        _ => "sementes de abóbora"
    };

    private void Refresh()
    {
        if (resources == null) return;
        int activeCount = missions.FindAll(m => m.accepted).Count;
        if (hudText != null)
            hudText.text = activeCount > 0 ? "Pedidos ativos: " + activeCount :
                marketOpen && missions.Count > 0 ? "Novas encomendas na barraca" : "Pedidos: nenhum ativo";
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
