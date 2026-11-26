using UnityEngine;
using UnityEngine.InputSystem;

namespace HopUp.FarmEconomy
{
    /// <summary>
    /// 플레이어가 화면 가운데(조준점)로 밭을 보고
    ///   좌클릭 : 다 자랐으면 수확, 비어 있으면 씨앗 심기
    ///   F 키   : 물주기
    /// 를 하게 해 주는 컴포넌트. Player 오브젝트(또는 아무 빈 오브젝트)에 붙인다.
    /// FarmPlantingTester(임시 패널)와 같이 둬도 되고, 이게 잘 되면 Tester는 지워도 된다.
    /// </summary>
    public sealed class FarmPlantingInteractor : MonoBehaviour
    {
        [Tooltip("비워두면 Camera.main 을 쓴다")]
        [SerializeField] Camera viewCamera;
        [SerializeField] string seedItemId = "seed_normal";
        [SerializeField] float reach = 6f;
        [SerializeField] bool logResult = true;

        [Tooltip("켜면 좌클릭으로도 심기/수확 (원래 방식). 발판 설치와 겹치면 끄고 R 키만 쓰면 됨")]
        [SerializeField] bool useLeftClick = true;

        FarmPlot looking;   // 지금 조준점이 가리키는 밭
        PlayerController player;
        bool uiMode;        // Tab으로 켜는 "커서 풀기" 모드

        void Awake()
        {
            player = GetComponent<PlayerController>();
        }

        void Update()
        {
            if (TabKeyDown()) ToggleUiMode();
            if (uiMode) { looking = null; return; }

            Camera cam = viewCamera != null ? viewCamera : Camera.main;
            if (cam == null) return;

            looking = FindPlotUnderCrosshair(cam);
            if (looking == null) return;

            EconomyHub hub = EconomyHub.Instance;
            if (hub == null) return;

            if (ActionKeyDown())
            {
                bool ready = looking.Model != null && looking.Model.Phase == PlotPhase.Ready;
                if (ready)
                {
                    var r = hub.Field.HarvestToStock(looking.PlotIndex);
                    Log("수확: " + r);
                }
                else
                {
                    var r = hub.Field.TryPlantFromInventory(looking.PlotIndex, seedItemId);
                    Log("심기: " + r);
                }
            }
            else if (WaterKeyDown())
            {
                var r = hub.Field.TryWater(looking.PlotIndex);
                Log("물주기: " + r);
            }
        }

        FarmPlot FindPlotUnderCrosshair(Camera cam)
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit[] hits = Physics.RaycastAll(ray, reach, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit h in hits)
            {
                FarmPlot p = h.collider.GetComponentInParent<FarmPlot>();
                if (p != null) return p;
            }
            return null;
        }

        // R 키(기본) 또는 좌클릭(useLeftClick 켰을 때) = 심기/수확
        bool ActionKeyDown()
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) return true;
            return useLeftClick && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        }

        static bool TabKeyDown()
        {
            return Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
        }

        // Tab: 커서를 풀어서 시험 패널을 누를 수 있게 / 다시 잠가서 플레이
        void ToggleUiMode()
        {
            uiMode = !uiMode;
            Cursor.lockState = uiMode ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = uiMode;
            if (player != null) player.InputEnabled = !uiMode;
        }

        static bool WaterKeyDown()
        {
            return Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
        }

        void Log(string msg)
        {
            if (logResult) Debug.Log("[FarmPlantingInteractor] 밭 " + looking.PlotIndex + " " + msg, looking);
        }

        // 씬 뷰에서 조준 중인 밭을 눈으로 확인하는 용도
        void OnGUI()
        {
            GUI.Label(new Rect(Screen.width - 260f, 10f, 250f, 24f),
                      uiMode ? "[Tab] 커서 풀림 - 패널 클릭 가능" : "[Tab] 커서 풀기");
            if (looking == null) return;
            GUI.Label(new Rect(Screen.width * 0.5f - 80f, Screen.height * 0.5f + 20f, 260f, 24f),
                      "밭 #" + looking.PlotIndex + "  [좌클릭 또는 R: 심기/수확, F: 물주기]");
        }
    }
}
