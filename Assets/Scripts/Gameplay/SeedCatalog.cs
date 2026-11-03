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
    [Min(0)] public int bonusYieldPerExtraDay = 1;
    [Min(1)] public int witherDaysAfterMaturity = 2;
}

public sealed class SeedCatalog : MonoBehaviour
{
    [SerializeField] private CropSettings corn = new CropSettings
    {
        type = CropType.Corn, displayName = "Milho", coinCost = 2, seedsPerPurchase = 2,
        growthDays = 2, foodYield = 2
    };
    [SerializeField] private CropSettings pumpkin = new CropSettings
    {
        type = CropType.Pumpkin, displayName = "Macaxeira", coinCost = 3, seedsPerPurchase = 1,
        growthDays = 2, foodYield = 4
    };
    [SerializeField] private CropSettings jerimum = new CropSettings
    {
        type = CropType.Jerimum, displayName = "Jerimum", coinCost = 3, seedsPerPurchase = 1,
        growthDays = 3, foodYield = 5
    };

    public CropSettings Get(CropType type)
    {
        return type == CropType.Corn ? corn : type == CropType.Pumpkin ? pumpkin :
            type == CropType.Jerimum ? jerimum : null;
    }
}
