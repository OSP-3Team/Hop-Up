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
    }
}