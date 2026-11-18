using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HopUp.FarmEconomy.Tests
{
    /// <summary>PlayMode 테스트(Test Runner > PlayMode). 씬 없이 EconomyHub·밭 4칸·봇을 코드로 만들어 상태 머신을 돌린다.</summary>
    public class FarmBotPlayModeTests
    {
        EconomyHub hub;
        readonly System.Collections.Generic.List<GameObject> spawned = new System.Collections.Generic.List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 4f;
            var hubGo = new GameObject("EconomyHub");
            spawned.Add(hubGo);
            hub = hubGo.AddComponent<EconomyHub>();
            for (int i = 0; i < 4; i++)
                Spawn<FarmPlot>("Plot" + i, new Vector3(i * 2f, 0f, 0f), "{\"plotIndex\":" + i + "}");
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            // 즉시 파괴: 지연 파괴면 다음 테스트의 EconomyHub가 남은 Instance를 보고 스스로 사라짐
            foreach (GameObject go in spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            spawned.Clear();
        }

        // 비활성 상태로 만들어 직렬화 필드(plotIndex/botIndex)를 먼저 채운 뒤 켠다(OnEnable 전에 번호가 정해져야 함)
        T Spawn<T>(string name, Vector3 pos, string json) where T : MonoBehaviour
        {
            var go = new GameObject(name);
            spawned.Add(go);
            go.SetActive(false);
            go.transform.position = pos;
            T c = go.AddComponent<T>();
            JsonUtility.FromJsonOverwrite(json, c);
            go.SetActive(true);
            return c;
        }

        FarmBot SpawnBot(int index, Vector3 pos)
        {
            hub.Shop.RestoreBots(Math.Max(hub.Shop.BotsOwned, index + 1));
            return Spawn<FarmBot>("Bot" + index, pos, "{\"botIndex\":" + index + "}");
        }

        static IEnumerator WaitUntil(Func<bool> done, float seconds = 30f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(done(), "시간 안에 조건을 만족하지 못함");
        }

        PlotModel Plot(int i) { return hub.Field.GetPlot(i); }

        [UnityTest]
        public IEnumerator Bot_PlantsAllPlots_ThenHarvestsWithoutSeeds()
        {
            FarmBot bot = SpawnBot(0, new Vector3(-2f, 0f, 3f));
            yield return null;
            hub.Inventory.TryAdd("seed_normal", 4);
            Assert.AreEqual(SeedInsertResult.Ok, bot.InsertSeeds("seed_normal", 4));

            yield return WaitUntil(() => Enumerable.Range(0, 4).All(i => Plot(i).Phase == PlotPhase.Growing));
            Assert.AreEqual(0, bot.Model.SeedCount);

            hub.Field.Tick(100000f);  // 전부 다 자라게
            yield return WaitUntil(() => bot.Model.Carrots.Total == 4 && bot.Current == FarmBot.State.Idle);
            Assert.IsTrue(Enumerable.Range(0, 4).All(i => Plot(i).Phase == PlotPhase.Empty), "씨앗이 없으니 다시 심지 않음");
        }

        [UnityTest]
        public IEnumerator TwoBots_OneRipePlot_OnlyOneGoes()
        {
            hub.Field.TryPlant(2, "normal", true);
            hub.Field.Tick(100000f);
            FarmBot a = SpawnBot(0, new Vector3(-2f, 0f, 3f));
            FarmBot b = SpawnBot(1, new Vector3(8f, 0f, 3f));
            yield return WaitUntil(() => a.Current != FarmBot.State.Idle || b.Current != FarmBot.State.Idle);
            yield return null;
            Assert.IsFalse(a.Current != FarmBot.State.Idle && b.Current != FarmBot.State.Idle, "두 봇이 같은 칸으로 감");

            yield return WaitUntil(() => a.Model.Carrots.Total + b.Model.Carrots.Total == 1);
            Assert.AreEqual(PlotPhase.Empty, Plot(2).Phase);
        }

        [UnityTest]
        public IEnumerator PlayerHarvestsTargetFirst_BotGivesUpUnchanged()
        {
            hub.Field.TryPlant(3, "normal", true);
            hub.Field.Tick(100000f);
            FarmBot bot = SpawnBot(0, new Vector3(-6f, 0f, 3f));  // 멀리서 출발해 이동 중에 가로챈다
            yield return WaitUntil(() => bot.Current == FarmBot.State.Moving);

            Assert.AreEqual(HarvestResult.Ok, hub.Field.HarvestToStock(3));
            Assert.AreEqual(FarmBot.State.Idle, bot.Current, "목표 칸이 바뀌면 바로 대기로");
            Assert.AreEqual(0, bot.Model.Carrots.Total);
            Assert.AreEqual(1, hub.Carrots.Count("normal"));
        }

        [UnityTest]
        public IEnumerator Bot_IgnoresGoldenAndLockedPlots()
        {
            hub.Zone.NotifyHeight(150f);  // 황금 칸이 열리는 단계. 일반 칸은 여전히 0~3만 열림
            FarmBot bot = SpawnBot(0, new Vector3(-2f, 0f, 3f));
            int golden = hub.Field.NormalPlotTotal;
            Spawn<FarmPlot>("Locked", new Vector3(-4f, 0f, 3f), "{\"plotIndex\":4}");         // 잠긴 일반 칸(봇 바로 옆)
            Spawn<FarmPlot>("Golden", new Vector3(-2f, 0f, 5f), "{\"plotIndex\":" + golden + "}");  // 황금 전용 칸
            yield return null;
            hub.Inventory.TryAdd("seed_normal", 1);
            bot.InsertSeeds("seed_normal", 1);

            yield return WaitUntil(() => bot.Model.SeedCount == 0 && bot.Current == FarmBot.State.Idle);
            Assert.AreEqual(PlotPhase.Empty, Plot(4).Phase);
            Assert.AreEqual(PlotPhase.Empty, Plot(golden).Phase);
            Assert.AreEqual(1, Enumerable.Range(0, 4).Count(i => Plot(i).Phase == PlotPhase.Growing));
        }
    }
}
