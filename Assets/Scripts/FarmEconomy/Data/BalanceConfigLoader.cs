using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HopUp.FarmEconomy
{
    /// <summary>밸런스 JSON을 읽어 BalanceConfig로 만든다. 읽기와 검사를 통과해야 성공으로 친다.</summary>
    public static class BalanceConfigLoader
    {
        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            NullValueHandling = NullValueHandling.Ignore
        };

        /// <summary>성공하면 true와 config, 실패하면 false와 errors(config는 null).</summary>
        public static bool TryParse(string json, out BalanceConfig config, out List<string> errors)
        {
            config = null;
            errors = new List<string>();
            if (string.IsNullOrWhiteSpace(json))
            {
                errors.Add("JSON이 비어 있습니다.");
                return false;
            }

            BalanceConfig parsed;
            try
            {
                parsed = JsonConvert.DeserializeObject<BalanceConfig>(json, Settings);
            }
            catch (Exception ex)
            {
                errors.Add("JSON을 읽지 못했습니다: " + ex.Message);
                return false;
            }
            if (parsed == null)
            {
                errors.Add("JSON 내용이 없습니다.");
                return false;
            }

            errors = parsed.Validate();
            if (errors.Count > 0) return false;
            config = parsed;
            return true;
        }

        /// <summary>내장 기본값(DefaultBalanceJson)으로 만든 설정.</summary>
        public static BalanceConfig CreateDefault()
        {
            BalanceConfig cfg;
            List<string> errors;
            if (!TryParse(DefaultBalanceJson.Text, out cfg, out errors))
                throw new InvalidOperationException("내장 기본 밸런스가 올바르지 않습니다:\n- " + string.Join("\n- ", errors));
            return cfg;
        }
    }
}
