using System;
using UnityEngine;

public enum FoodIngredient { Peixe, Milho, Macaxeira, Jerimum, Leite, Ovos }

[Serializable]
public sealed class RecipeIngredient
{
    [InspectorName("Ingrediente")] public FoodIngredient type;
    [Min(1), InspectorName("Quantidade")] public int amount = 1;
}

[Serializable]
public sealed class FoodRecipe
{
    [InspectorName("Nome")] public string name;
    [InspectorName("Ingredientes")] public RecipeIngredient[] ingredients;
    [Min(1), InspectorName("Comida gerada")] public int foodYield = 1;
}

public sealed class InventoryTrading : MonoBehaviour
{
    [SerializeField] private ResourceBank resources;
    [SerializeField] private SeedMerchant merchant;
    [SerializeField] private GameFeedback feedback;
    [Header("Preços provisórios por unidade")]
    [SerializeField, Min(0)] private int woodPrice = 1;
    [SerializeField, Min(0)] private int fishPrice = 2;
    [SerializeField, Min(0)] private int cornPrice = 4;
    [SerializeField, Min(0)] private int pumpkinPrice = 1;
    [SerializeField, Min(0)] private int foodPrice = 1;
    [SerializeField, Min(0)] private int cornSeedPrice = 1;
    [SerializeField, Min(0)] private int pumpkinSeedPrice = 2;
    [SerializeField, Min(0)] private int jerimumPrice = 3;
    [SerializeField, Min(0)] private int jerimumSeedPrice = 2;
    [SerializeField, Min(0)] private int milkPrice = 2;
    [SerializeField, Min(0)] private int eggPrice = 2;
    [Header("Receitas de comida")]
    [SerializeField] private FoodRecipe[] recipes = Array.Empty<FoodRecipe>();
    [SerializeField, Min(1), InspectorName("Multiplicador Comida+")] private int foodPlusMultiplier = 2;

    public int GetStock(SaleItem item) => resources == null ? 0 : item switch
    {
        SaleItem.Wood => resources.Wood,
        SaleItem.Fish => resources.Fish,
        SaleItem.Corn => resources.Corn,
        SaleItem.Pumpkin => resources.Pumpkin,
        SaleItem.Jerimum => resources.Jerimum,
        SaleItem.Food => resources.Food,
        SaleItem.CornSeeds => resources.CornSeeds,
        SaleItem.PumpkinSeeds => resources.PumpkinSeeds,
        SaleItem.JerimumSeeds => resources.JerimumSeeds,
        SaleItem.Milk => resources.Milk,
        SaleItem.Eggs => resources.Eggs,
        _ => 0
    };

    public int GetPrice(SaleItem item) => item switch
    {
        SaleItem.Wood => woodPrice,
        SaleItem.Fish => fishPrice,
        SaleItem.Corn => cornPrice,
        SaleItem.Pumpkin => pumpkinPrice,
        SaleItem.Jerimum => jerimumPrice,
        SaleItem.Food => foodPrice,
        SaleItem.CornSeeds => cornSeedPrice,
        SaleItem.PumpkinSeeds => pumpkinSeedPrice,
        SaleItem.JerimumSeeds => jerimumSeedPrice,
        SaleItem.Milk => milkPrice,
        SaleItem.Eggs => eggPrice,
        _ => 0
    };

    public bool TrySellBatch(int[] quantities)
    {
        if (merchant == null || !merchant.IsAvailable || !merchant.IsOpen || resources == null)
        {
            if (feedback != null) feedback.ShowMessage("Visite a barraca aberta para vender.");
            return false;
        }
        int[] prices = new int[(int)SaleItem.Eggs + 1];
        for (int i = 0; i < prices.Length; i++) prices[i] = GetPrice((SaleItem)i);
        if (!resources.TrySellBatch(quantities, prices))
        {
            if (feedback != null) feedback.ShowMessage("Confira as quantidades escolhidas para venda.");
            return false;
        }
        if (feedback != null) feedback.ShowMessage("Venda concluída.");
        return true;
    }

    public bool TryPrepare(int fish, int corn, int pumpkin, int jerimum, int milk, int eggs,
        bool plus, out int food)
    {
        food = 0;
        if (!TryGetRecipe(fish, corn, pumpkin, jerimum, milk, eggs, plus,
            out _, out int output)) return false;
        return resources != null && resources.TryPrepareFood(fish, corn, pumpkin, jerimum,
            milk, eggs, output, out food);
    }

    public bool TryGetRecipe(int fish, int corn, int pumpkin, int jerimum, int milk, int eggs,
        bool plus, out string recipeName, out int output)
    {
        recipeName = null;
        output = 0;
        int[] selected = { fish, corn, pumpkin, jerimum, milk, eggs };
        foreach (int amount in selected)
            if (amount < 0) return false;
        if (recipes == null) return false;
        foreach (FoodRecipe recipe in recipes)
        {
            if (recipe == null || recipe.foodYield < 1 ||
                !MatchesRecipe(selected, recipe, out int portions)) continue;
            long total = (long)recipe.foodYield * portions * (plus ? foodPlusMultiplier : 1);
            if (total <= 0 || total > int.MaxValue) continue;
            recipeName = string.IsNullOrWhiteSpace(recipe.name) ? "Receita" : recipe.name;
            output = (int)total;
            return true;
        }
        return false;
    }

    private static bool MatchesRecipe(int[] selected, FoodRecipe recipe, out int portions)
    {
        portions = 0;
        if (recipe.ingredients == null || recipe.ingredients.Length == 0) return false;
        var required = new int[6];
        foreach (RecipeIngredient ingredient in recipe.ingredients)
        {
            if (ingredient == null || ingredient.amount < 1 ||
                (int)ingredient.type < 0 || (int)ingredient.type >= required.Length) return false;
            int index = (int)ingredient.type;
            if (required[index] > int.MaxValue - ingredient.amount) return false;
            required[index] += ingredient.amount;
        }
        for (int i = 0; i < required.Length; i++)
        {
            if (required[i] == 0)
            {
                if (selected[i] != 0) return false;
                continue;
            }
            if (selected[i] == 0 || selected[i] % required[i] != 0) return false;
            int count = selected[i] / required[i];
            if (portions == 0) portions = count;
            else if (portions != count) return false;
        }
        return portions > 0;
    }
}
