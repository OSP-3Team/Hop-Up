using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Shop
{
    public class ShopEntryUI : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text priceText;

        [SerializeField] private Color normalColor = new Color(0.22f, 0.22f, 0.25f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.4f, 0.32f, 0.12f, 1f);
        [SerializeField] private Color priceNormalColor = Color.white;
        [SerializeField] private Color priceShortColor = new Color(1f, 0.3f, 0.3f, 1f); // 코인 부족

        public ShopEntry Entry { get; private set; }

        public void Setup(ShopEntry entry, Action<ShopEntry> onClick)
        {
            Entry = entry;
            nameText.text = entry.displayName;
            priceText.text = $"{entry.price} 코인";

            // 항목에 아이콘이 없으면 연결된 아이템의 아이콘을 씀
            Sprite icon = entry.icon;
            if (icon == null && entry.item != null) icon = entry.item.icon;

            iconImage.sprite = icon;
            iconImage.enabled = icon != null;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke(entry));

            SetSelected(false);
            SetPriceShort(false);
        }

        public void SetSelected(bool selected)
        {
            background.color = selected ? selectedColor : normalColor;
        }

        // 코인이 모자라면 가격을 빨간색으로
        public void SetPriceShort(bool isShort)
        {
            priceText.color = isShort ? priceShortColor : priceNormalColor;
        }
    }
}