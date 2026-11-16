using System;

namespace HopUp.FarmEconomy
{
    /// <summary>
    /// 지대 단계. 정거장 높이에 도달하면 한 단계씩 올라가고 내려가지 않는다.
    /// 높이가 75/150/225이면 단계는 0~3(75m 도달=1단계, 150m=2단계, 225m=3단계).
    /// </summary>
    public sealed class ZoneProgress
    {
        readonly float[] heights;

        public ZoneProgress(float[] stationHeights)
        {
            heights = stationHeights ?? new float[0];
        }

        public int Stage { get; private set; }
        public int MaxStage { get { return heights.Length; } }

        /// <summary>새 단계가 열릴 때마다(단계 하나당 한 번) 그 단계 번호를 알린다.</summary>
        public event Action<int> StageUnlocked;

        public int StageForHeight(float height)
        {
            int stage = 0;
            for (int i = 0; i < heights.Length; i++)
                if (height >= heights[i]) stage = i + 1;
            return stage;
        }

        /// <summary>플레이어가 도달한 높이를 알린다. 단계가 올랐으면 true.</summary>
        public bool NotifyHeight(float height)
        {
            int target = StageForHeight(height);
            if (target <= Stage) return false;
            while (Stage < target)
            {
                Stage++;
                var handler = StageUnlocked;
                if (handler != null) handler(Stage);
            }
            return true;
        }

        /// <summary>불러오기·새로하기용. 이벤트는 내지 않는다.</summary>
        public void Restore(int stage)
        {
            Stage = Math.Max(0, Math.Min(stage, heights.Length));
        }
    }
}
