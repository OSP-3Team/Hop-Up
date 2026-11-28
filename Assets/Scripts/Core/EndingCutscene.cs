using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.Core
{
    /// <summary>
    /// Ending 씬의 연출 시간표. 조작 없이 보기만 한다. 세이브는 건드리지 않는다(MainGame에서 넘어오기 전에 저장함).
    /// 지구(돌고 있음) → 카메라가 내려와 달 위의 우리 토끼 → 달 토끼 둘이 깡충깡충 다가와 셋이 같이 뜀 → 멀리서 셋과 지구 → THE END → 메인 메뉴.
    /// 이동(다가오기·깡충)은 위치·회전으로 만들고, 손 흔들기 같은 제스처만 토끼의 Animator 클립(EndingBunny.controller)으로 재생한다.
    /// 오브젝트 배치와 참조 연결은 Tools > Hop-Up > Setup Ending(EndingSetupMenu)이 해 준다.
    /// 카메라는 장면마다 Cam_* 빈 오브젝트의 위치·회전으로 옮겨 간다(씬 화면에서 그 오브젝트를 옮겨 구도를 고침).
    /// </summary>
    public class EndingCutscene : MonoBehaviour
    {
        [Header("오브젝트")]
        [SerializeField, Tooltip("달 바닥 충돌체(MeshCollider). 토끼 발을 이 높이에 맞춤. 비우면 놓인 높이 그대로")]
        private Collider ground;
        [SerializeField] private Transform earth;
        [SerializeField] private Transform playerBunny;
        [SerializeField, Tooltip("달 토끼 두 마리. 시작 위치에서 우리 토끼 옆으로 다가옴")]
        private Transform[] moonBunnies;

        [Header("카메라 자리 (위치·회전을 그대로 씀)")]
        [SerializeField] private Transform camEarth;
        [SerializeField] private Transform camArrive;
        [SerializeField] private Transform camMeet;
        [SerializeField] private Transform camWide;

        [Header("시간(초)")]
        [SerializeField] private float earthDuration = 3f;
        [SerializeField] private float arriveDuration = 4f;
        [SerializeField] private float meetDuration = 6f;
        [SerializeField] private float wideDuration = 5f;
        [SerializeField, Tooltip("THE END를 보여 주는 시간. Space/Esc로 넘길 수 있음")]
        private float endTextDuration = 4f;

        [Header("움직임")]
        [SerializeField, Tooltip("지구 자전 속도(도/초)")]
        private float earthSpinSpeed = 6f;
        [SerializeField] private float hopHeight = 0.4f;
        [SerializeField, Tooltip("한 번 뛰는 데 걸리는 시간(초)")]
        private float hopPeriod = 0.45f;
        [SerializeField, Tooltip("달 토끼가 우리 토끼 옆에 설 간격(m). 첫째는 1배, 둘째는 2배 거리")]
        private float standGap = 1.2f;

        [SerializeField, Tooltip("엔딩 뒤 돌아갈 씬 이름")]
        private string mainMenuScene = "MainMenu";

        // 시작 직후엔 건너뛰기를 받지 않음: MainGame에서 점프하며 올라오던 Space 연타가 엔딩을 넘겨 버리지 않게
        private const float SkipLockTime = 1f;
        private const float FadeInTime = 0.5f;
        private const float FadeOutTime = 1f;
        private const float TurnTime = 1f;      // 우리 토끼가 지구 쪽에서 달 토끼 쪽으로 도는 시간
        private const float WalkPart = 0.6f;    // 만남 장면 중 달 토끼가 다가오는 데 쓰는 비율. 나머지에 셋이 같이 뜀

        private async void Start()
        {
            Camera cam = Camera.main;
            if (cam == null || earth == null || playerBunny == null || moonBunnies == null || moonBunnies.Length < 2
                || moonBunnies[0] == null || moonBunnies[1] == null
                || camEarth == null || camArrive == null || camMeet == null || camWide == null)
            {
                Debug.LogWarning("[Ending] 참조가 비어 있어 연출 없이 메인 메뉴로 갑니다. Tools > Hop-Up > Setup Ending을 다시 실행하세요.", this);
                SceneManager.LoadScene(mainMenuScene);
                return;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Transform camTr = cam.transform;

            Image fade = OpeningSequence.CreateFadeOverlay(out _);
            OpeningSequence.SetAlpha(fade, 1f);
            TMP_Text endText = CreateEndText(fade.transform);

            // 달 토끼가 설 자리: 우리 토끼에서 달 토끼들이 오는 쪽으로 standGap, 2·standGap
            Vector3 playerPos = OnGround(playerBunny.position);
            playerBunny.position = playerPos;
            for (int i = 0; i < 2; i++) moonBunnies[i].position = OnGround(moonBunnies[i].position);
            Vector3 comeDir = Vector3.ProjectOnPlane(moonBunnies[0].position - playerPos, Vector3.up);
            comeDir = comeDir.sqrMagnitude > 0.01f ? comeDir.normalized : playerBunny.right;
            Vector3[] from = { moonBunnies[0].position, moonBunnies[1].position };
            Vector3[] to = { playerPos + comeDir * standGap, playerPos + comeDir * (standGap * 2f) };
            Quaternion playerFrom = playerBunny.rotation;
            Quaternion playerTo = Quaternion.LookRotation(comeDir);
            Quaternion moonFace = Quaternion.LookRotation(-comeDir);

            float tArrive = earthDuration;
            float tMeet = tArrive + arriveDuration;
            float tWide = tMeet + meetDuration;
            float total = tWide + wideDuration;
            float walkTime = Mathf.Max(meetDuration * WalkPart, 0.0001f);
            float hopTogether = tMeet + walkTime;
            float walkHops = Mathf.Max(1f, Mathf.Round(walkTime / Mathf.Max(hopPeriod, 0.0001f)));
            Animator playerAnim = playerBunny.GetComponent<Animator>();
            Animator[] moonAnims = { moonBunnies[0].GetComponent<Animator>(), moonBunnies[1].GetComponent<Animator>() };
            bool playerWaved = false, moonWaved = false;

            try
            {
                for (float t = 0f; t < total; t += Time.deltaTime)
                {
                    if (t > SkipLockTime && OpeningSequence.SkipPressed()) break;

                    earth.Rotate(0f, earthSpinSpeed * Time.deltaTime, 0f, Space.Self);

                    // 카메라: 지구 → (내려와) 도착 → 만남 → 넓게
                    if (t < tArrive) Place(camTr, camEarth, camEarth, 0f);
                    else if (t < tMeet) Place(camTr, camEarth, camArrive, (t - tArrive) / arriveDuration);
                    else if (t < tWide) Place(camTr, camArrive, camMeet, (t - tMeet) / Mathf.Min(meetDuration, 1.5f));
                    else Place(camTr, camMeet, camWide, (t - tWide) / wideDuration);

                    // 만남: 달 토끼가 뛰어 오고, 우리 토끼가 돌아서고, 다 오면 셋이 같이 뜀
                    if (t >= tMeet)
                    {
                        float walk = Mathf.Clamp01((t - tMeet) / walkTime);
                        bool together = t >= hopTogether && t < hopTogether + 2f * hopPeriod;
                        // 다가오는 동안은 walkHops번 뛰어 도착하는 순간 땅에 닿게 함 (멈출 때 공중에서 뚝 떨어지지 않게)
                        float y = walk < 1f ? hopHeight * Mathf.Abs(Mathf.Sin(Mathf.PI * walkHops * walk))
                                : together ? Hop(t - hopTogether) : 0f;
                        for (int i = 0; i < 2; i++)
                            moonBunnies[i].SetPositionAndRotation(OnGround(Vector3.Lerp(from[i], to[i], walk)) + Vector3.up * y, moonFace);
                        Vector3 pp = playerPos;
                        pp.y += together ? Hop(t - hopTogether) : 0f;
                        float turn = Mathf.SmoothStep(0f, 1f, (t - tMeet) / TurnTime);
                        playerBunny.SetPositionAndRotation(pp, Quaternion.Slerp(playerFrom, playerTo, turn));

                        // 제스처: 우리 토끼는 다 돌아서면, 달 토끼는 도착하는 순간 손을 흔듦 (끝나면 컨트롤러가 Idle로 돌림)
                        if (!playerWaved && t >= tMeet + TurnTime) { playerWaved = true; Gesture(playerAnim, "Wave"); }
                        if (!moonWaved && t >= hopTogether) { moonWaved = true; foreach (Animator a in moonAnims) Gesture(a, "Wave"); }
                    }

                    float fadeIn = 1f - t / FadeInTime;
                    float fadeOut = (t - (total - FadeOutTime)) / FadeOutTime;
                    OpeningSequence.SetAlpha(fade, Mathf.Clamp01(Mathf.Max(fadeIn, fadeOut)));
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                }

                OpeningSequence.SetAlpha(fade, 1f);
                await Awaitable.NextFrameAsync(destroyCancellationToken); // 건너뛰기에 쓴 키가 THE END까지 넘기지 않게

                for (float t = 0f; t < endTextDuration; t += Time.deltaTime)
                {
                    endText.alpha = Mathf.Clamp01(t / 0.5f);
                    if (OpeningSequence.SkipPressed()) break;
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                return; // 연출 중 씬이 바뀌어 파괴됨
            }

            SceneManager.LoadScene(mainMenuScene);
        }

        /// <summary>그 자리의 달 바닥 높이로 내린(올린) 위치. 바닥 충돌체가 없거나 못 맞히면 그대로</summary>
        private Vector3 OnGround(Vector3 p)
        {
            if (ground == null) return p;
            var ray = new Ray(new Vector3(p.x, p.y + 50f, p.z), Vector3.down);
            return ground.Raycast(ray, out RaycastHit hit, 100f) ? hit.point : p;
        }

        /// <summary>뛰는 높이: 땅에 닿을 때 0, hopPeriod마다 한 번씩 튕기듯</summary>
        private float Hop(float t) => hopHeight * Mathf.Abs(Mathf.Sin(Mathf.PI * t / hopPeriod));

        /// <summary>Animator가 없는(예전에 만든) 씬이면 아무것도 안 함</summary>
        private static void Gesture(Animator a, string state)
        {
            if (a != null && a.runtimeAnimatorController != null) a.CrossFade(state, 0.15f);
        }

        private static void Place(Transform cam, Transform a, Transform b, float t)
        {
            float s = Mathf.SmoothStep(0f, 1f, t);
            cam.SetPositionAndRotation(Vector3.Lerp(a.position, b.position, s), Quaternion.Slerp(a.rotation, b.rotation, s));
        }

        private static TMP_Text CreateEndText(Transform canvas)
        {
            var go = new GameObject("EndText", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = "THE END";
            text.fontSize = 96f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.alpha = 0f;
            text.raycastTarget = false;
            return text;
        }
    }
}
