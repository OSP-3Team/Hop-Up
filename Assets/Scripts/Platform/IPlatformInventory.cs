/// <summary>
/// 발판 설치 기능(PlatformPlacer)과 인벤토리가 서로 대화하기 위한 "약속".
/// 인벤토리 쪽 스크립트가 이 인터페이스를 구현하면, 씬이 달라도 PlatformPlacer가 찾아서 쓸 수 있습니다.
///
/// 구현하는 쪽(인벤토리 매니저)에서 해야 할 일:
///   1) class InventoryManager : MonoBehaviour, IPlatformInventory
///   2) OnEnable()에서 PlatformInventoryLocator.Register(this)
///      OnDisable()에서 PlatformInventoryLocator.Unregister(this)
///   3) 아래 두 가지 멤버를 채우기
/// </summary>
public interface IPlatformInventory
{
    /// <summary>지금 손에 든 칸(선택된 칸)의 아이템이 발판인가?</summary>
    bool IsHoldingPlatform { get; }

    /// <summary>손에 든 발판 아이템의 id (예: scaffold_1). 발판이 아니면 빈 문자열.  [종류별 발판]</summary>
    string HeldPlatformId { get; }

    /// <summary>
    /// 손에 든 발판 1개를 소모한다. 성공하면 true, 실패(발판이 아님·수량 0)하면 false.
    /// 수량이 0이 되면 칸을 비우고 빈손으로 만드는 처리는 인벤토리 쪽에서 한다.
    /// </summary>
    bool TryConsumeHeldPlatform();
}

/// <summary>
/// 지금 씬에 있는 인벤토리를 찾아주는 연결 창구.
/// (다른 씬에 있는 오브젝트는 Inspector로 끌어서 연결할 수 없기 때문에 필요합니다.)
/// </summary>
public static class PlatformInventoryLocator
{
    public static IPlatformInventory Current { get; private set; }

    public static void Register(IPlatformInventory inventory)
    {
        Current = inventory;
    }

    public static void Unregister(IPlatformInventory inventory)
    {
        if (Current == inventory) Current = null;
    }
}
