using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Project.Core;

namespace Project.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [SerializeField, Tooltip("이어하기 버튼의 글자 (비활성일 때 흐리게 만들 용도)")]
        private TMP_Text continueLabel;

        [SerializeField, Tooltip("새로하기·이어하기를 눌렀을 때 불러올 씬 이름. Build Profiles의 Scene List에 있어야 함")]
        private string gameSceneName = "Sandbox_Inventory";

        [SerializeField, Tooltip("저장 파일 이름. 8.2(세이브)와 맞춰야 함")]
        private string saveFileName = "save.json";

        [SerializeField, Tooltip("테스트용: 저장 파일이 있는 것처럼 취급")]
        private bool debugForceSaveExists;

        // 설정 버튼을 눌렀을 때 발생 (6.8 설정 화면이 받아서 연다)
        public event Action OnSettingsRequested;

        private void Awake()
        {
            // 게임에서 메인 메뉴로 돌아올 때를 대비: 커서 풀기, 시간 되돌리기
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;

            newGameButton.onClick.AddListener(OnNewGameClicked);
            continueButton.onClick.AddListener(OnContinueClicked);
            settingsButton.onClick.AddListener(OnSettingsClicked);
            quitButton.onClick.AddListener(OnQuitClicked);
        }

        private void OnEnable()
        {
            RefreshContinueButton();
        }

        private bool HasSaveFile()
        {
            if (debugForceSaveExists) return true;

            string path = Path.Combine(Application.persistentDataPath, saveFileName);
            return File.Exists(path);
        }

        private void RefreshContinueButton()
        {
            bool canContinue = HasSaveFile();
            continueButton.interactable = canContinue;

            if (continueLabel != null)
                continueLabel.alpha = canContinue ? 1f : 0.4f;
        }

        private void OnNewGameClicked()
        {
            StartGame(StartMode.NewGame);
        }

        private void OnContinueClicked()
        {
            if (!HasSaveFile()) return; // 비활성이어야 하지만 혹시를 대비
            StartGame(StartMode.Continue);
        }

        private void StartGame(StartMode mode)
        {
            GameStartRequest.Mode = mode;
            Debug.Log($"[MainMenu] {mode} 시작 → {gameSceneName}");
            SceneManager.LoadScene(gameSceneName); // 오프닝은 게임 씬의 OpeningSequence가 GameStartRequest를 보고 재생
        }

        private void OnSettingsClicked()
        {
            OnSettingsRequested?.Invoke();
            Debug.Log("[MainMenu] 설정 버튼 클릭 (6.8 설정 화면 연결 예정)");
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // 테스트용: 점 세 개(⋮) 메뉴에서 실행하면 저장 파일 경로가 Console에 나옴
        [ContextMenu("Log Save Path")]
        private void LogSavePath()
        {
            Debug.Log(Path.Combine(Application.persistentDataPath, saveFileName));
        }
    }
}