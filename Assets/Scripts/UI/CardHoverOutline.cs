using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class CardHoverOutline : MonoBehaviour
{
    private Image cardImage;
    private Material materialInstance;
    private Material originalMaterial;
    private Sprite currentSprite;
    private Tween fade;

    private void OnEnable()
    {
        if (Application.isPlaying) Initialize((RectTransform)transform);
    }

    public static CardHoverOutline Ensure(RectTransform card)
    {
        CardHoverOutline effect = card.GetComponent<CardHoverOutline>();
        if (effect != null)
        {
            effect.Initialize(card);
            return effect;
        }
        effect = card.gameObject.AddComponent<CardHoverOutline>();
        effect.Initialize(card);
        return effect;
    }

    private void Initialize(RectTransform card)
    {
        if (materialInstance != null) return;
        Image image = card.GetComponent<Image>();
        Shader shader = Resources.Load<Shader>("CardHoverOutline");
        if (image == null || shader == null) return;

        Transform oldOutline = card.Find("Contorno branco");
        if (oldOutline != null) Destroy(oldOutline.gameObject);

        cardImage = image;
        originalMaterial = image.material;
        materialInstance = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        materialInstance.SetFloat("_Glow", 0f);
        SyncArt();
        image.material = materialInstance;
    }

    public void SetHighlighted(bool highlighted)
    {
        if (materialInstance == null) return;
        SyncArt();
        fade?.Kill();
        fade = DOTween.To(() => materialInstance.GetFloat("_Glow"), value =>
        {
            materialInstance.SetFloat("_Glow", value);
            cardImage.SetMaterialDirty();
        }, highlighted ? 1f : 0f, 0.14f).SetEase(Ease.OutSine);
    }

    private void LateUpdate()
    {
        if (cardImage != null && cardImage.sprite != currentSprite) SyncArt();
    }

    private void SyncArt()
    {
        currentSprite = cardImage.sprite;
        materialInstance.SetTexture("_ArtTex", currentSprite != null ?
            currentSprite.texture : Texture2D.whiteTexture);
        cardImage.SetMaterialDirty();
    }

    private void OnDisable()
    {
        fade?.Kill();
        if (materialInstance != null) materialInstance.SetFloat("_Glow", 0f);
        if (cardImage != null) cardImage.SetMaterialDirty();
    }

    private void OnDestroy()
    {
        fade?.Kill();
        if (cardImage != null && cardImage.material == materialInstance)
            cardImage.material = originalMaterial;
        if (materialInstance != null) Destroy(materialInstance);
    }
}
