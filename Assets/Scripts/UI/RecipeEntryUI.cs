using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using HopUp.FarmEconomy;

namespace Project.UI
{
    public class RecipeEntryUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text effectText;
        [SerializeField] private TMP_Text ingredientsText;
        [SerializeField] private Button craftButton;
        [SerializeField] private TMP_Text craftButtonLabel;

        public RecipeDef Recipe { get; private set; }

        public void Setup(RecipeDef recipe, Action<RecipeDef> onCraft)
        {
            Recipe = recipe;
            craftButton.onClick.RemoveAllListeners();
            craftButton.onClick.AddListener(() => onCraft?.Invoke(Recipe));
        }

        public void Refresh(string displayName, string effect, string ingredients, bool canCraft)
        {
            nameText.text = displayName;
            effectText.text = effect;
            ingredientsText.text = ingredients;

            // 재료가 모자라면 제작 불가로 표시하고 누를 수 없게 함
            craftButton.interactable = canCraft;
            craftButtonLabel.text = canCraft ? "제작" : "제작 불가";
            craftButtonLabel.alpha = canCraft ? 1f : 0.5f;
        }
    }
}