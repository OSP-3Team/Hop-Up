using UnityEngine;

namespace Project.Core
{
    public enum StartMode
    {
        NewGame,   // 새로하기: 오프닝을 보여 주고 처음부터 시작
        Continue   // 이어하기: 오프닝을 생략하고 저장된 진행 상황을 불러옴
    }

    // 메인 메뉴에서 고른 시작 방식을 게임 씬에 전달하는 용도
    public static class GameStartRequest
    {
        // 메인 메뉴를 거치지 않고 게임 씬을 바로 실행하면 새 게임으로 취급
        public static StartMode Mode = StartMode.NewGame;

        public static bool ShouldPlayOpening => Mode == StartMode.NewGame;
        public static bool ShouldLoadSave => Mode == StartMode.Continue;

        // 프로젝트가 Play 때 도메인 리로드를 끄고 있어(Enter Play Mode Options) static 값이 이전 Play에서 남음.
        // 이전에 누른 이어하기가 남아 게임 씬을 바로 Play해도 세이브를 불러오지 않게, Play마다 새 게임으로 되돌림
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Mode = StartMode.NewGame;
        }
    }
}
