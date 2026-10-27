using System;
using UnityEngine;

public sealed class ResourceBank : MonoBehaviour
{
    [SerializeField, Min(0)] private int startingWood;
    [SerializeField, Min(0)] private int startingFood = 2;

    public int Wood { get; private set; }
    public int Food { get; private set; }
    public int Score { get; private set; }
    public int CornSeeds { get; private set; }
    public int PumpkinSeeds { get; private set; }
    public event Action Changed;

    private void Awake()
    {
        Wood = startingWood;
        Food = startingFood;
        Score = 0;
        CornSeeds = 0;
        PumpkinSeeds = 0;
    }

    public void AddWood(int amount)
    {
        Wood += Mathf.Max(0, amount);
        Changed?.Invoke();
    }

    public void AddFood(int amount)
    {
        Food += Mathf.Max(0, amount);
        Changed?.Invoke();
    }

    public int ConsumeFood(int amount)
    {
        int consumed = Mathf.Min(Food, Mathf.Max(0, amount));
        Food -= consumed;
        Changed?.Invoke();
        return consumed;
    }

    public void AddScore(int amount)
    {
        Score += amount;
        Changed?.Invoke();
    }

    public bool TrySpendWood(int amount)
    {
        if (amount < 0 || Wood < amount)
            return false;
        Wood -= amount;
        Changed?.Invoke();
        return true;
    }

    public void AddSeeds(CropType type, int amount)
    {
        if (type == CropType.Corn) CornSeeds += Mathf.Max(0, amount);
        if (type == CropType.Pumpkin) PumpkinSeeds += Mathf.Max(0, amount);
        Changed?.Invoke();
    }

    public bool TrySpendSeed(CropType type)
    {
        if (type == CropType.Corn && CornSeeds > 0) CornSeeds--;
        else if (type == CropType.Pumpkin && PumpkinSeeds > 0) PumpkinSeeds--;
        else return false;
        Changed?.Invoke();
        return true;
    }

    public int Seeds(CropType type)
    {
        return type == CropType.Corn ? CornSeeds : type == CropType.Pumpkin ? PumpkinSeeds : 0;
    }
}
