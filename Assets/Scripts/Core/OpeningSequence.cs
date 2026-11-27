using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.Core
{
    /// <summary>
    /// 새로하기로 게임 씬에 들어왔을 때 토끼가 달에서 농장으로 떨어지는 오프닝 연출.
    /// 대역 토끼(플레이어 모델을 구운 복사본)가 달(3D 구)을 보고 선 채 뒤로 미끄러져 농장으로 떨어진다. 토끼는 내내 달을 본다.
    /// 카메라는 토끼 등 뒤에서 달을 보다가, 어느 정도 떨어지면 토끼 옆을 돌아 위로 올라가 농장으로 떨어지는 모습을 내려다본다.
    /// 땅에 닿는 순간 검은 화면 → 1인칭으로 바꾼 뒤 밝아짐. 진짜 플레이어는 움직이지 않음(낙하 판정에 안 걸리게).
    /// Space/Esc로 건너뛸 수 있고, 오프닝 동안 오른쪽 아래에 안내 문구를 띄운다. 다른 UI(HUD 등)는 오프닝 동안 숨긴다.
    /// 이어하기면 아무것도 하지 않는다. Player나 Moon이 비면 오프닝을 생략.
    ///
    /// 씬 배선은 Tools > Hop-Up > Setup Opening(OpeningSetupMenu)이 해 준다. 오프닝 동안 끌 컴포넌트:
    /// PlayerController, PlatformPlacer, Hotbar, InventoryWindowUI, ShopWindowUI, FirstPersonArm
    /// </summary>
    public class OpeningSequence : MonoBehaviour
    {
        [SerializeField, Tooltip("오프닝 동안 끌 컴포넌트 (PlayerController, PlatformPlacer, Hotbar, InventoryWindowUI, ShopWindowUI, FirstPersonArm)")]
        private Behaviour[] disableDuringOpening;

        [SerializeField, Tooltip("Player 루트 (발 위치 기준). 이 모델을 복사해 떨어뜨리고, 착지 지점도 여기")]
        private Transform player;

        [SerializeField, Tooltip("하늘의 달(구). 토끼가 이 가장자리에서 떨어지기 시작함")]
        private Transform moon;

        [SerializeField, Tooltip("달 가장자리에 서 있는 시간(초)")]
        private float standDuration = 1f;

        [SerializeField, Tooltip("달 표면을 따라 미끄러져 내려가는 시간(초). 짧을수록 빨리 미끄러짐")]
        private float slideDuration = 1.2f;

        [SerializeField, Tooltip("떨어지는 토끼를 카메라 쪽에서 비추는 보조광 세기. 아래에서 올려다보면 해를 등져 어두워서 넣음")]
        private float fillLightIntensity = 6f;

        [SerializeField, Tooltip("떨어지는 데 걸리는 시간(초)")]
        private float fallDuration = 7f;

        [SerializeField, Tooltip("카메라와 토끼 사이 거리(m)")]
        private float cameraDistance = 10f;

        [SerializeField, Range(0f, 1f), Tooltip("낙하 진행도(0~1) 중 언제부터 카메라가 토끼 위로 돌아가 농장 쪽을 보여 줄지")]
        private float farmViewAt = 0.5f;

        [SerializeField, Tooltip("카메라가 토끼 뒤에서 위로 돌아가는 데 걸리는 시간(초)")]
        private float swingDuration = 1.2f;

        [SerializeField, Tooltip("착지 직전 검게 가리는 시간(초). 1인칭이 된 뒤 같은 시간 동안 다시 밝아짐")]
        private float fadeDuration = 0.3f;

        // EndingSequence가 같은 Player를 씀
        internal Transform Player => player;

        private async void Start()
        {
            if (!GameStartRequest.ShouldPlayOpening) return;

            Camera cam = Camera.main;
            if (cam == null || player == null || moon == null)
            {
                Debug.LogWarning("[Opening] MainCamera·Player·Moon 중 비어 있는 것이 있어 오프닝을 생략합니다.", this);
                return;
            }

            // 카메라를 부모에서 떼지 않음: PlayerController가 꺼져 있는 동안 부모가 움직이지 않으므로 월드 자세를 되돌리면 원래 자세가 됨
            Transform camTr = cam.transform;
            Vector3 endPos = camTr.position;
            Quaternion endRot = camTr.rotation;

            // Start에서 끄므로 FirstPersonArm의 LateUpdate가 한 번도 돌지 않아 팔이 원래 자세로 남음 (대역 토끼도 이 자세로 구워짐)
            SetControls(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // 진짜 토끼는 숨기고 대역을 떨어뜨림 (농장을 내려다볼 때 두 마리로 보이지 않게)
            // 원래 꺼져 있던 렌더러는 빼야 끝나고 잘못 켜지 않음
            Renderer[] playerRenderers = Array.FindAll(player.GetComponentsInChildren<Renderer>(), r => r.enabled);
            Transform standIn = CreateStandIn(playerRenderers, out Vector3 landCenter);
            foreach (Renderer r in playerRenderers) r.enabled = false;

            // HUD 등 다른 UI는 오프닝 동안 숨김. Canvas만 끄므로 각 UI 스크립트는 그대로 돎
            var hiddenCanvases = new List<Canvas>();
            foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.enabled)
                {
                    c.enabled = false;
                    hiddenCanvases.Add(c);
                }
            // 카메라 쪽 2m에서 대역 토끼를 비추는 점광원 (거리가 일정해서 착지 직전에도 너무 밝아지지 않음)
            var fillLight = new GameObject("OpeningFillLight").AddComponent<Light>();
            fillLight.type = LightType.Point;
            fillLight.range = 8f;
            fillLight.intensity = fillLightIntensity;
            fillLight.shadows = LightShadows.None;

            void Restore()
            {
                if (fillLight != null) Destroy(fillLight.gameObject);
                foreach (Canvas c in hiddenCanvases)
                    if (c != null) c.enabled = true;
                hiddenCanvases.Clear();
                foreach (Renderer r in playerRenderers)
                    if (r != null) r.enabled = true;
                if (standIn != null) Destroy(standIn.gameObject);
            }

            Image fade = CreateFadeOverlay(out GameObject skipHint);
            try
            {
                // 달 표면(스폰 쪽, 위도 25°)에 서 있다가 → 둥근 표면을 따라 점점 빨리 미끄러져 내려가고 →
                // 표면이 수직이 되는 적도에서 그 속도 그대로 떨어져 나가 스폰에 떨어짐 (중간에 멈칫하거나 튀어 나가지 않게 속도를 이어 붙임)
                // shortcut: 스폰이 달 반지름 안쪽(달 바로 아래)이면 떨어지는 길이 달을 뚫음. 달 위치는 OpeningSetupMenu가 반지름+2m로 맞춤
                Renderer moonRenderer = moon.GetComponent<Renderer>();
                float surface = (moonRenderer != null ? moonRenderer.bounds.extents.y : 0f) + 0.3f; // 발이 표면에 묻히지 않게 살짝 띄움
                Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
                Vector3 toSpawn = Vector3.ProjectOnPlane(landCenter - moon.position, Vector3.up);
                toSpawn = toSpawn.sqrMagnitude > 0.01f ? toSpawn.normalized : -forward;
                float feetToCenter = landCenter.y - player.position.y;
                Vector3 OnMoon(float lat) =>
                    moon.position + (toSpawn * Mathf.Cos(lat) + Vector3.up * Mathf.Sin(lat)) * surface + Vector3.up * feetToCenter;
                float startLat = 25f * Mathf.Deg2Rad;
                Vector3 standCenter = OnMoon(startLat);
                Vector3 dropCenter = OnMoon(0f);
                // 미끄러짐은 위도를 u²로 줄이므로 끝 속도 = 2·호의 길이/시간. 낙하는 이 속도로 시작해 fallDuration 뒤 땅에 닿도록 가속도를 맞춤
                float v0 = 2f * startLat * surface / Mathf.Max(slideDuration, 0.0001f);
                float height = dropCenter.y - landCenter.y;
                float gravity = 2f * (height - v0 * fallDuration) / (fallDuration * fallDuration);
                Quaternion modelYawInv = Quaternion.Inverse(Quaternion.LookRotation(forward)); // 구운 모델의 원래 방향을 상쇄
                float swingStart = farmViewAt * fallDuration;
                float minCamY = player.position.y + 0.3f;
                float fadeFrom = 1f - fadeDuration / Mathf.Max(fallDuration, 0.0001f);
                float total = standDuration + slideDuration + fallDuration;

                bool skipped = false;
                for (float t = 0f; t < total; t += Time.deltaTime)
                {
                    if (SkipPressed())
                    {
                        skipped = true;
                        break;
                    }
                    float slide = Mathf.Clamp01((t - standDuration) / Mathf.Max(slideDuration, 0.0001f));
                    float fallT = Mathf.Clamp(t - standDuration - slideDuration, 0f, fallDuration);
                    float p = fallT / fallDuration;
                    float drop = v0 * fallT + 0.5f * gravity * fallT * fallT;
                    Vector3 center = t < standDuration + slideDuration
                        ? OnMoon(startLat * (1f - slide * slide))                       // 표면을 따라 미끄러짐
                        : Vector3.Lerp(dropCenter, landCenter, drop / height);          // 이어서 떨어짐
                    standIn.position = center;

                    // 토끼는 내내 달을 봄: 달 위에선 안쪽(수평), 떨어지면서는 달 쪽으로 점점 뒤로 젖혀짐 (적도에서 두 방향이 이어짐)
                    Vector3 look = t < standDuration + slideDuration ? -toSpawn : (moon.position - center).normalized;
                    Quaternion faceMoon = Quaternion.LookRotation(look, Vector3.up);
                    standIn.rotation = faceMoon * modelYawInv;
                    Vector3 rabbitUp = faceMoon * Vector3.up;

                    // 카메라 1: 토끼 등 뒤·머리 위쪽에서 토끼 너머 달을 봄 (토끼가 화면 아래, 달이 가운데)
                    Vector3 camPos = center - look * cameraDistance + rabbitUp * (cameraDistance * 0.13f); // 거리에 비례해 올려서 구도 유지
                    Quaternion camRot = Quaternion.LookRotation(moon.position - camPos, rabbitUp);
                    // 카메라 2: farmViewAt부터 토끼 옆을 돌아 위로 올라가, 토끼 너머 농장을 내려다봄 (토끼가 등부터 떨어지는 모습)
                    float swing = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((fallT - swingStart) / Mathf.Max(swingDuration, 0.0001f)));
                    if (swing > 0f)
                    {
                        Vector3 off = camPos - center;
                        float fromAngle = Mathf.Atan2(Vector3.Dot(off, Vector3.up), Vector3.Dot(off, toSpawn)); // 토끼 아래(약 -80°)
                        float angle = Mathf.Lerp(fromAngle, 60f * Mathf.Deg2Rad, swing);                       // → 옆(0°) → 위(60°)
                        camPos = center + (toSpawn * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle))
                            * Mathf.Lerp(off.magnitude, cameraDistance, swing);
                        Quaternion toMoon = Quaternion.LookRotation(moon.position - camPos, rabbitUp);
                        Quaternion toFarm = Quaternion.LookRotation(center + Vector3.down * 3f - camPos, Vector3.up);
                        camRot = Quaternion.Slerp(toMoon, toFarm, swing);
                    }
                    camPos.y = Mathf.Max(camPos.y, minCamY);
                    camTr.SetPositionAndRotation(camPos, camRot);
                    fillLight.transform.position = center + (camPos - center).normalized * 2f;
                    SetAlpha(fade, Mathf.InverseLerp(fadeFrom, 1f, p));
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                }
                SetAlpha(fade, skipped ? 0f : 1f); // 스킵하면 검은 화면 없이 바로 1인칭

                Restore();
                camTr.SetPositionAndRotation(endPos, endRot);
                // 한 프레임 뒤에 켬: 스킵에 쓴 Space가 같은 프레임에 PlayerController에서 점프로 읽히지 않게
                await Awaitable.NextFrameAsync(destroyCancellationToken);
                SetControls(true);
                skipHint.SetActive(false);

                for (float t = 0f; fade.color.a > 0f && t < fadeDuration; t += Time.deltaTime)
                {
                    SetAlpha(fade, 1f - t / fadeDuration);
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // 연출 중 씬이 바뀌어 파괴됨
            }
            finally
            {
                Restore(); // 이미 했으면 아무것도 안 함
                if (fade != null) Destroy(fade.canvas.gameObject);
            }
        }

        /// <summary>
        /// 플레이어의 스킨 메시를 지금 자세 그대로 구워 만든 대역 토끼. 루트는 몸통 가운데(돌 때 축)에 둔다.
        /// landCenter = 진짜 토끼 몸통 가운데 (대역이 떨어져 닿을 곳).
        /// </summary>
        private static Transform CreateStandIn(Renderer[] renderers, out Vector3 landCenter)
        {
            var root = new GameObject("OpeningStandIn").transform;
            Bounds bounds = default;
            bool any = false;
            foreach (Renderer r in renderers)
            {
                if (!any) bounds = r.bounds; else bounds.Encapsulate(r.bounds);
                any = true;
            }
            landCenter = bounds.center;
            root.position = landCenter;

            foreach (Renderer r in renderers)
            {
                if (r is not SkinnedMeshRenderer smr) continue; // 손에 든 아이템 등은 대역에 넣지 않음
                var mesh = new Mesh();
                smr.BakeMesh(mesh); // 월드 크기로 구워짐, 위치·회전만 아래에서 맞춤 (true로 구우면 렌더러 크기(Quaternius 모델은 100)로 나뉘어 작아짐)
                var part = new GameObject(smr.name, typeof(MeshFilter), typeof(MeshRenderer));
                part.transform.SetParent(root, false);
                part.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
                part.GetComponent<MeshFilter>().sharedMesh = mesh;
                part.GetComponent<MeshRenderer>().sharedMaterials = smr.sharedMaterials;
            }
            return root;
        }

        /// <summary>검은 페이드 화면과 오른쪽 아래 건너뛰기 안내. 오프닝이 끝나면 통째로 지움.</summary>
        internal static Image CreateFadeOverlay(out GameObject skipHint)
        {
            var go = new GameObject("OpeningFade", typeof(Canvas), typeof(CanvasScaler), typeof(Image));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            var image = go.GetComponent<Image>(); // 루트 Image가 Canvas 전체를 채움
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = false;

            // TMP 기본 폰트가 NotoSansKR이라 한글이 그대로 나옴
            skipHint = new GameObject("SkipHint", typeof(RectTransform));
            skipHint.transform.SetParent(go.transform, false);
            var rt = (RectTransform)skipHint.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-48f, 40f);
            rt.sizeDelta = new Vector2(600f, 60f);
            var text = skipHint.AddComponent<TextMeshProUGUI>();
            text.text = "Space / Esc  건너뛰기";
            text.fontSize = 30f;
            text.alignment = TextAlignmentOptions.BottomRight;
            text.color = new Color(1f, 1f, 1f, 0.8f);
            text.raycastTarget = false;
            return image;
        }

        internal static void SetAlpha(Image image, float a)
        {
            Color c = image.color;
            c.a = a;
            image.color = c;
        }

        internal static bool SkipPressed()
        {
            Keyboard kb = Keyboard.current;
            return kb != null && (kb.spaceKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame);
        }

        internal void SetControls(bool on)
        {
            if (disableDuringOpening == null) return;
            foreach (Behaviour b in disableDuringOpening)
                if (b != null) b.enabled = on;
        }
    }
}
