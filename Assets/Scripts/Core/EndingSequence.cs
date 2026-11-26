using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.Core
{
    /// <summary>
    /// 엔딩 높이(FallController.EndingReached, 300m)에 착지하면 조작·HUD를 끄고 검은 화면이 된 뒤 엔딩 씬으로 넘어간다.
    /// 넘어가기 직전에 플레이어를 농장 시작 위치로 옮겨 저장한다(이어하기는 농장에서 시작, 코인·밭·발판은 그대로).
    /// 엔딩 연출 자체는 Ending 씬의 EndingCutscene이 한다.
    /// OpeningSequence와 같은 오브젝트에 붙이고, Player·끌 컴포넌트는 OpeningSequence 설정을 같이 쓴다.
    /// </summary>
    [RequireComponent(typeof(OpeningSequence))]
    public class EndingSequence : MonoBehaviour
    {
        [SerializeField, Tooltip("검은 화면으로 바뀌는 시간(초)")]
        private float fadeDuration = 1f;

        [SerializeField, Tooltip("넘어갈 엔딩 씬 이름. Build Profiles의 Scene List에 있어야 함")]
        private string endingScene = "Ending";

        private OpeningSequence opening;
        private PlayerController playerCtrl;
        private FallController fall;
        private Vector3 spawnFoot;
        private float spawnYaw;

        private void Awake()
        {
            opening = GetComponent<OpeningSequence>();
            Transform player = opening.Player;
            if (player == null) return;

            // 씬에 놓인 자리 = 농장 시작 위치. Awake라서 이어하기 불러오기(SaveManager.Start)가 옮기기 전에 읽음
            spawnFoot = player.position;
            spawnYaw = player.eulerAngles.y;
            playerCtrl = player.GetComponent<PlayerController>();
            fall = player.GetComponent<FallController>();
            if (fall != null) fall.EndingReached += Play;
        }

        private void OnDestroy()
        {
            if (fall != null) fall.EndingReached -= Play;
        }

        private async void Play()
        {
            opening.SetControls(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            foreach (Canvas c in FindObjectsByType<Canvas>())
                if (c.isRootCanvas) c.enabled = false;

            Image fade = OpeningSequence.CreateFadeOverlay(out GameObject skipHint);
            skipHint.SetActive(false);
            try
            {
                for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
                {
                    OpeningSequence.SetAlpha(fade, t / fadeDuration);
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                }
            }
            catch (System.OperationCanceledException)
            {
                return; // 그새 씬이 바뀌어 파괴됨
            }
            OpeningSequence.SetAlpha(fade, 1f);
            SaveAndExit();
        }

        private void SaveAndExit()
        {
            // 이어하기는 농장에서 시작. 같은 프레임에 낙하 기록을 지우고 저장해야 300m → 농장 이동이 낙하로 잡히지 않음
            if (playerCtrl != null) playerCtrl.TeleportTo(spawnFoot, spawnYaw);
            if (fall != null) fall.ResetTracking();
            if (SaveManager.Instance == null || !SaveManager.Instance.SaveNow())
                Debug.LogWarning("[Ending] 저장하지 못했습니다. 마지막 자동 저장이 남습니다.", this);
            // 엔딩 씬이 Scene List에 없으면(Setup Ending 안 돌림 등) 검은 화면에 갇히지 않게 메인 메뉴로
            if (Application.CanStreamedLevelBeLoaded(endingScene)) SceneManager.LoadScene(endingScene);
            else
            {
                Debug.LogError("[Ending] '" + endingScene + "' 씬을 열 수 없어 메인 메뉴로 갑니다. Tools > Hop-Up > Setup Ending을 실행하세요.", this);
                SceneManager.LoadScene("MainMenu");
            }
        }
    }
}
