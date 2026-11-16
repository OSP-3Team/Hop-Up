using System;

namespace HopUp.FarmEconomy
{
    /// <summary>
    /// 농사·경제 시스템이 쓰는 인벤토리 규칙. 실제 인벤토리(이동운님)가 이 인터페이스를 구현해 EconomyHub에 연결한다.
    /// 구현이 없으면 InMemoryInventory(임시 인벤토리)를 쓴다.
    /// </summary>
    public interface IInventory
    {
        int SlotCount { get; }
        int MaxSlots { get; }

        int CountOf(string itemId);
        /// <summary>실제로 넣은 개수를 반환(자리가 모자라면 넣을 수 있는 만큼만).</summary>
        int TryAdd(string itemId, int count);
        /// <summary>실제로 뺀 개수를 반환(모자라면 있는 만큼만).</summary>
        int TryRemove(string itemId, int count);
        /// <summary>이 아이템을 지금 더 넣을 수 있는 개수.</summary>
        int FreeCapacityFor(string itemId);
        /// <summary>칸을 늘린다. 최대 칸 수를 넘으면 아무것도 하지 않고 false.</summary>
        bool TryExpandSlots(int count);
    }

    /// <summary>인벤토리가 가득 찼을 때 아이템을 플레이어 앞 바닥에 떨어뜨린다(이성재님 월드 드롭 구현).</summary>
    public interface IItemDropper
    {
        void DropInFrontOfPlayer(string itemId, int count);
    }
}
