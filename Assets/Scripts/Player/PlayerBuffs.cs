using UnityEngine;

/// <summary>
/// 플레이어가 가진 "효과"를 보관하는 곳. (지금은 낙하 방지 효과 하나뿐)
///
/// - 요리 시스템(음식을 먹으면 효과 획득)은 AddFallProtection(횟수)를 호출하면 됩니다.
/// - FallController가 낙하 페널티 직전에 TryConsumeFallProtection()으로 1회 사용합니다.
///
/// Player 오브젝트에 붙입니다. (FallController와 같은 오브젝트)
/// Inspector의 Fall Protection Charges는 테스트용 시작 횟수입니다. (평소엔 0)
/// </summary>
public class PlayerBuffs : MonoBehaviour
{
    [Header("낙하 방지 효과")]
    [SerializeField, Tooltip("남은 낙하 방지 횟수. 테스트할 때 1 이상으로 바꿔 보세요 (평소엔 0)")]
    private int fallProtectionCharges = 0;

    /// <summary>남은 낙하 방지 횟수</summary>
    public int FallProtectionCharges => fallProtectionCharges;

    /// <summary>낙하 방지 횟수를 늘린다. (요리 시스템에서 호출)</summary>
    public void AddFallProtection(int amount = 1)
    {
        if (amount <= 0) return;
        fallProtectionCharges += amount;
    }

    /// <summary>낙하 방지 1회 사용. 횟수가 있으면 1 줄이고 true, 없으면 false.</summary>
    public bool TryConsumeFallProtection()
    {
        if (fallProtectionCharges <= 0) return false;

        fallProtectionCharges--;
        return true;
    }
}
