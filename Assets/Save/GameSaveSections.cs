using System;
using System.Collections.Generic;
using HopUp.FarmEconomy;
using Newtonsoft.Json.Linq;
using Project.Inventory;
using UnityEngine;

// 8.2 세이브: 이성재 담당 데이터(플레이어 위치, 인벤토리, 설치한 발판)의 저장 조각.
// 코인·당근·해금 지대·밭·요리 등은 이준형 님의 FarmEconomySaveSection이 저장합니다.
// SaveManager가 이 조각들을 모아 save.json 한 파일에 저장하고 불러옵니다.

/// <summary>플레이어 위치(발 위치)와 좌우 방향</summary>
public sealed class PlayerSaveSection : ISaveSection
{
    public const string SectionKey = "player";

    private readonly PlayerController player;

    public PlayerSaveSection(PlayerController player)
    {
        this.player = player;
    }

    public string Key => SectionKey;

    public JObject Capture()
    {
        Vector3 p = player.transform.position;   // 발 위치
        return new JObject
        {
            ["x"] = p.x,
            ["y"] = p.y,
            ["z"] = p.z,
            ["yaw"] = player.transform.eulerAngles.y
        };
    }

    public bool Restore(JObject data)
    {
        if (data == null) return false;

        try
        {
            Vector3 p = new Vector3((float)data["x"], (float)data["y"], (float)data["z"]);
            float yaw = (float)data["yaw"];
            player.TeleportTo(p, yaw);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

/// <summary>인벤토리 칸 수와 칸 내용 (아이템은 id로 저장)</summary>
public sealed class InventorySaveSection : ISaveSection
{
    public const string SectionKey = "inventory";

    private readonly InventoryManager inventory;

    public InventorySaveSection(InventoryManager inventory)
    {
        this.inventory = inventory;
    }

    public string Key => SectionKey;

    public JObject Capture()
    {
        var slots = new JArray();
        for (int i = 0; i < inventory.SlotCount; i++)
        {
            var slot = inventory.GetSlot(i);
            bool empty = slot == null || slot.IsEmpty;

            slots.Add(new JObject
            {
                ["id"] = empty ? "" : slot.item.id,
                ["count"] = empty ? 0 : slot.count
            });
        }

        return new JObject
        {
            ["slotCount"] = inventory.SlotCount,
            ["slots"] = slots
        };
    }

    public bool Restore(JObject data)
    {
        if (data == null) return false;

        try
        {
            int slotCount = (int)data["slotCount"];
            JArray arr = data["slots"] as JArray;
            if (arr == null) return false;

            var items = new ItemData[arr.Count];
            var counts = new int[arr.Count];

            for (int i = 0; i < arr.Count; i++)
            {
                string id = (string)arr[i]["id"];
                int count = (int)arr[i]["count"];
                if (string.IsNullOrEmpty(id) || count <= 0) continue;

                ItemData item = inventory.GetItemData(id);
                if (item == null)
                {
                    Debug.LogWarning("[세이브] 알 수 없는 아이템 id라서 빈 칸으로 불러옵니다: " + id);
                    continue;
                }

                items[i] = item;
                counts[i] = count;
            }

            inventory.RestoreSlots(slotCount, items, counts);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

/// <summary>설치된 발판들 (종류·위치·방향)</summary>
public sealed class PlatformsSaveSection : ISaveSection
{
    public const string SectionKey = "platforms";

    private readonly PlatformPlacer placer;

    public PlatformsSaveSection(PlatformPlacer placer)
    {
        this.placer = placer;
    }

    public string Key => SectionKey;

    public JObject Capture()
    {
        var list = new JArray();
        foreach (PlacedPlatform platform in PlacedPlatform.All)
        {
            if (platform == null) continue;

            Vector3 p = platform.transform.position;
            list.Add(new JObject
            {
                ["kind"] = platform.KindId,
                ["x"] = p.x,
                ["y"] = p.y,
                ["z"] = p.z,
                ["yaw"] = platform.transform.eulerAngles.y
            });
        }

        return new JObject { ["items"] = list };
    }

    public bool Restore(JObject data)
    {
        if (data == null) return false;

        JArray arr = data["items"] as JArray;
        if (arr == null) return false;

        try
        {
            // 먼저 지금 씬에 있는 설치 발판을 모두 치움
            var existing = new List<PlacedPlatform>(PlacedPlatform.All);
            foreach (PlacedPlatform platform in existing)
            {
                if (platform != null)
                    UnityEngine.Object.Destroy(platform.gameObject);
            }

            foreach (JToken t in arr)
            {
                string kind = (string)t["kind"];
                Vector3 p = new Vector3((float)t["x"], (float)t["y"], (float)t["z"]);
                float yaw = (float)t["yaw"];

                placer.SpawnPlatform(kind, p, Quaternion.Euler(0f, yaw, 0f));
            }
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
