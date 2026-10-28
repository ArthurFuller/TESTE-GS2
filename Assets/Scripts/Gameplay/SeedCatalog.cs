using System;
using UnityEngine;

[Serializable]
public sealed class CropSettings
{
    public CropType type;
    public string displayName;
    [Min(0)] public int coinCost;
    [Min(1)] public int seedsPerPurchase;
    [Min(1)] public int growthDays;
    [Min(0)] public int foodYield;
    [Min(0)] public int scoreYield;
}

public sealed class SeedCatalog : MonoBehaviour
{
    [SerializeField] private CropSettings corn = new CropSettings
    {
        type = CropType.Corn, displayName = "Milho", coinCost = 2, seedsPerPurchase = 2,
        growthDays = 2, foodYield = 4, scoreYield = 100
    };
    [SerializeField] private CropSettings pumpkin = new CropSettings
    {
        type = CropType.Pumpkin, displayName = "Abóbora", coinCost = 3, seedsPerPurchase = 1,
        growthDays = 3, foodYield = 7, scoreYield = 200
    };

    public CropSettings Get(CropType type)
    {
        return type == CropType.Corn ? corn : type == CropType.Pumpkin ? pumpkin : null;
    }
}
