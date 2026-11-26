using System.Collections.Generic;
using UnityEngine;

namespace HopUp.FarmEconomy
{
    /// <summary>
    /// 임시 시험용. 플레이어 입력 코드 없이 밭(심기·수확·물주기), 상점(밭 확장), 판매를 눈으로 확인하는 패널.
    /// 밭 칸 색: 회색=잠김, 갈색=빈 밭, 연두=성장 중, 주황=수확 가능.
    /// 확인이 끝나면 EconomyHub에서 이 컴포넌트를 제거하거나 파일을 삭제해도 된다.
    /// </summary>
    public sealed class FarmPlantingTester : MonoBehaviour
    {
        [SerializeField] string seedItemId = "seed_normal";
        [SerializeField] bool colorPlots = true;

        static readonly Color LockedColor = new Color(0.45f, 0.45f, 0.45f);
        static readonly Color EmptyColor = new Color(0.45f, 0.30f, 0.20f);
        static readonly Color GrowingColor = new Color(0.55f, 0.80f, 0.35f);
        static readonly Color ReadyColor = new Color(1.00f, 0.60f, 0.15f);

        FarmPlot[] plots = new FarmPlot[0];
        MaterialPropertyBlock block;
        int selected;
        string message = "";
        bool panelOpen = true;

        void Start()
        {
            plots = FindObjectsByType<FarmPlot>(FindObjectsSortMode.None);
            System.Array.Sort(plots, (a, b) => a.PlotIndex.CompareTo(b.PlotIndex));

            var seen = new HashSet<int>();
            foreach (FarmPlot p in plots)
            {
                if (!seen.Add(p.PlotIndex))
                    Debug.LogWarning("[FarmPlantingTester] Plot Index가 겹칩니다: " + p.PlotIndex + " (" + p.name + ")", p);
            }
            Debug.Log("[FarmPlantingTester] FarmPlot " + plots.Length + "개 발견");
        }

        void Update()
        {
            if (!colorPlots) return;
            if (block == null) block = new MaterialPropertyBlock();

            foreach (FarmPlot p in plots)
            {
                if (p == null || p.Model == null) continue;
                Renderer r = p.GetComponentInChildren<Renderer>();
                if (r == null) continue;

                Color c = LockedColor;
                if (p.IsUnlocked)
                {
                    switch (p.Model.Phase)
                    {
                        case PlotPhase.Growing: c = GrowingColor; break;
                        case PlotPhase.Ready: c = ReadyColor; break;
                        default: c = EmptyColor; break;
                    }
                }
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", c);
                block.SetColor("_Color", c);
                r.SetPropertyBlock(block);
            }
        }

        void OnGUI()
        {
            // F1 키로 패널 열기/닫기 (IMGUI 이벤트라서 새 Input System만 켜져 있어도 동작한다)
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.F1)
            {
                panelOpen = !panelOpen;
                e.Use();
            }

            EconomyHub hub = EconomyHub.Instance;
            if (hub == null)
            {
                GUI.Label(new Rect(10, 10, 500, 24), "EconomyHub가 씬에 없습니다");
                return;
            }

            if (!panelOpen)
            {
                if (GUI.Button(new Rect(10, 10, 170, 28), "농장 패널 열기 (F1)")) panelOpen = true;
                return;
            }

            GUILayout.BeginArea(new Rect(10, 10, 380, 590), GUI.skin.box);
            if (GUILayout.Button("패널 닫기 (F1)")) panelOpen = false;
            GUILayout.Label("농장 시험 패널 (임시)");
            GUILayout.Label("코인 " + hub.Wallet.Coins + " / 당근 " + hub.Carrots.Total
                            + " / 씨앗 " + hub.Inventory.CountOf(seedItemId) + " / 지대 단계 " + hub.Zone.Stage);
            GUILayout.Label("열린 밭 " + hub.Field.UnlockedNormalPlots + "칸 (지금 상한 " + hub.Field.NormalPlotCap + ")");

            GUILayout.Space(6);
            if (GUILayout.Button("씨앗 3개 받기")) message = "씨앗 " + hub.Inventory.TryAdd(seedItemId, 3) + "개 받음";
            if (GUILayout.Button("코인 +100")) { hub.Wallet.Add(100); message = "코인 +100"; }

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-", GUILayout.Width(40))) selected = Mathf.Max(0, selected - 1);
            GUILayout.Label("선택한 칸 번호: " + selected, GUILayout.Width(150));
            if (GUILayout.Button("+", GUILayout.Width(40))) selected = Mathf.Min(hub.Field.PlotCount - 1, selected + 1);
            GUILayout.EndHorizontal();

            PlotModel plot = hub.Field.GetPlot(selected);
            if (plot != null)
            {
                string state = hub.Field.IsUnlocked(plot) ? plot.Phase.ToString() : "잠김";
                GUILayout.Label("상태: " + state + " / 진행 " + Mathf.RoundToInt(plot.Progress01 * 100f) + "%"
                                + (plot.Watered ? " / 물 줌" : ""));
            }

            if (GUILayout.Button("심기")) message = "심기: " + hub.Field.TryPlantFromInventory(selected, seedItemId);
            if (GUILayout.Button("물주기")) message = "물주기: " + hub.Field.TryWater(selected);
            if (GUILayout.Button("수확")) message = "수확: " + hub.Field.HarvestToStock(selected);
            if (GUILayout.Button("성장 +30초 (빨리 감기)")) { hub.Field.Tick(30f); message = "성장 +30초"; }

            GUILayout.Space(6);
            if (GUILayout.Button("당근 전부 판매")) message = "판매로 " + hub.Sales.SellAll() + "코인";
            if (GUILayout.Button("밭 1칸 구매"))
            {
                PurchaseResult res = hub.Shop.TryPurchase(hub.Shop.Find(ShopService.PlotItemId), 1);
                message = "밭 확장: " + res.Status + " (" + res.CoinsSpent + "코인)";
            }

            GUILayout.Space(6);
            if (GUILayout.Button("75m 도달 처리")) { hub.Zone.NotifyHeight(75f); message = "지대 단계 " + hub.Zone.Stage; }
            if (GUILayout.Button("150m 도달 처리")) { hub.Zone.NotifyHeight(150f); message = "지대 단계 " + hub.Zone.Stage; }

            GUILayout.Space(6);
            GUILayout.Label(message);
            GUILayout.EndArea();
        }
    }
}
