using System.Collections.Generic;

namespace Roost.Core
{
    public sealed class AiPreset
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public string BaseUrl { get; private set; }
        public string Model { get; private set; }
        public string Description { get; private set; }
        public string KeyUrl { get; private set; }

        public AiPreset(string id, string name, string baseUrl, string model, string description, string keyUrl)
        {
            Id = id;
            Name = name;
            BaseUrl = baseUrl;
            Model = model;
            Description = description;
            KeyUrl = keyUrl;
        }
    }

    public static class AiPresets
    {
        public const string CustomId = "custom";

        // 推荐模型须通过 AI 评测集（PRD 8.6）后才算定稿；当前为 2026-09-23 按各家官方文档填写的候选。
        public static readonly AiPreset[] All =
        {
            new AiPreset("deepseek", "DeepSeek", "https://api.deepseek.com/v1", "deepseek-flash",
                "深度求索官方接口，中文理解好，价格低。", "https://platform.deepseek.com/api_keys"),
            new AiPreset("qwen", "通义千问", "https://dashscope.aliyuncs.com/compatible-mode/v1", "qwen-plus",
                "阿里云百炼（北京地域），需使用北京地域创建的 key。", "https://bailian.console.aliyun.com/"),
            new AiPreset("kimi", "Kimi", "https://api.moonshot.cn/v1", "kimi-k2.6",
                "月之暗面 Kimi 开放平台的通用模型。", "https://platform.kimi.com/console/api-keys"),
            new AiPreset("zhipu", "智谱", "https://open.bigmodel.cn/api/paas/v4", "glm-5.3-flash",
                "智谱开放平台的 GLM 轻量模型。", "https://open.bigmodel.cn/usercenter/apikeys"),
            new AiPreset("doubao", "豆包", "https://ark.cn-beijing.volces.com/api/v3", "doubao-seed-2-1-pro-260628",
                "火山方舟（北京），模型名也可以填自己创建的接入点 ID（ep- 开头）。", "https://console.volcengine.com/ark/region:ark+cn-beijing/apiKey")
        };

        public static AiPreset Find(string id)
        {
            foreach (AiPreset preset in All) if (preset.Id == id) return preset;
            return null;
        }
    }
}
