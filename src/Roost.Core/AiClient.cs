using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Roost.Core
{
    public enum AiFailureKind
    {
        InvalidKey,
        NotFound,
        Rejected,
        Quota,
        RateLimited,
        Server,
        Network,
        Timeout,
        Cancelled,
        BadResponse
    }

    public sealed class AiException : Exception
    {
        public AiFailureKind Kind { get; private set; }

        public AiException(AiFailureKind kind, string message) : base(message)
        {
            Kind = kind;
        }

        public bool Retryable
        {
            get
            {
                return Kind == AiFailureKind.Network || Kind == AiFailureKind.Timeout ||
                       Kind == AiFailureKind.Server || Kind == AiFailureKind.RateLimited;
            }
        }
    }

    public sealed class AiEndpoint
    {
        public string BaseUrl { get; set; }
        public string Model { get; set; }
        public string ApiKey { get; set; }

        public string ChatCompletionsUrl
        {
            get
            {
                string value = (BaseUrl ?? string.Empty).Trim().TrimEnd('/');
                const string suffix = "/chat/completions";
                if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    value = value.Substring(0, value.Length - suffix.Length);
                return value + suffix;
            }
        }
    }

    public sealed class AiClient
    {
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
        private const int MaxVendorMessageLength = 120;
        private static readonly HttpClient Http;

        static AiClient()
        {
            // csc 直接编译的程序没有目标框架标记，默认只启用 SSL3/TLS1.0；部分厂商要求 TLS 1.2+。
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | (SecurityProtocolType)12288;
            Http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        }

        private readonly TimeSpan timeout;

        public AiClient() : this(DefaultTimeout) { }

        public AiClient(TimeSpan timeout)
        {
            this.timeout = timeout;
        }

        public async Task<string> CompleteAsync(AiEndpoint endpoint, string systemPrompt, string userMessage, int maxTokens, CancellationToken cancellation)
        {
            if (endpoint == null) throw new ArgumentNullException("endpoint");
            Uri uri;
            if (!Uri.TryCreate(endpoint.ChatCompletionsUrl, UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                throw new AiException(AiFailureKind.NotFound, "服务地址格式不正确。");

            Dictionary<string, object> body = new Dictionary<string, object>();
            body["model"] = endpoint.Model;
            body["messages"] = new object[]
            {
                new Dictionary<string, object> { { "role", "system" }, { "content", systemPrompt } },
                new Dictionary<string, object> { { "role", "user" }, { "content", userMessage } }
            };
            body["temperature"] = 0;
            body["max_tokens"] = maxTokens;
            body["stream"] = false;
            JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };

            using (CancellationTokenSource timer = new CancellationTokenSource(timeout))
            using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, timer.Token))
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, uri))
            {
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + (endpoint.ApiKey ?? string.Empty).Trim());
                request.Content = new StringContent(serializer.Serialize(body), Encoding.UTF8, "application/json");
                string text;
                HttpStatusCode status;
                try
                {
                    using (HttpResponseMessage response = await Http.SendAsync(request, linked.Token).ConfigureAwait(false))
                    {
                        status = response.StatusCode;
                        text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    if (cancellation.IsCancellationRequested) throw new AiException(AiFailureKind.Cancelled, "已取消。");
                    throw new AiException(AiFailureKind.Timeout, string.Format("模型服务 {0} 秒内没有回应，请稍后重试。", (int)timeout.TotalSeconds));
                }
                catch (HttpRequestException)
                {
                    throw new AiException(AiFailureKind.Network, "连不上模型服务，请检查网络后重试。");
                }

                if ((int)status < 200 || (int)status >= 300) throw FromStatus(status, text, serializer);
                return ReadContent(text, serializer);
            }
        }

        public Task<string> CheckAsync(AiEndpoint endpoint, CancellationToken cancellation)
        {
            return CompleteAsync(endpoint, "你是连通性检测。", "请只回复 ok", 16, cancellation);
        }

        private static AiException FromStatus(HttpStatusCode status, string body, JavaScriptSerializer serializer)
        {
            int code = (int)status;
            if (code == 401 || code == 403)
                return new AiException(AiFailureKind.InvalidKey, "API Key 无效或没有权限，请到设置里检查。");
            string vendor = VendorMessage(body, serializer);
            string suffix = vendor == null ? string.Empty : "（服务返回：" + vendor + "）";
            if (code == 404) return new AiException(AiFailureKind.NotFound, "服务地址或模型名不正确。" + suffix);
            if (code == 402) return new AiException(AiFailureKind.Quota, "模型账户余额不足。" + suffix);
            if (code == 429) return new AiException(AiFailureKind.RateLimited, "请求太频繁或额度已用完，请稍后重试。" + suffix);
            if (code >= 500) return new AiException(AiFailureKind.Server, string.Format("模型服务出错（{0}），请稍后重试。", code));
            return new AiException(AiFailureKind.Rejected, string.Format("模型服务拒绝了请求（{0}），可能是模型名不正确。", code) + suffix);
        }

        private static string VendorMessage(string body, JavaScriptSerializer serializer)
        {
            try
            {
                Dictionary<string, object> root = serializer.DeserializeObject(body) as Dictionary<string, object>;
                if (root == null) return null;
                object error;
                string message = null;
                if (root.TryGetValue("error", out error))
                {
                    Dictionary<string, object> detail = error as Dictionary<string, object>;
                    object value;
                    if (detail != null && detail.TryGetValue("message", out value)) message = value as string;
                    else message = error as string;
                }
                if (message == null)
                {
                    object value;
                    if (root.TryGetValue("message", out value)) message = value as string;
                }
                if (string.IsNullOrEmpty(message)) return null;
                message = message.Trim();
                return message.Length <= MaxVendorMessageLength ? message : message.Substring(0, MaxVendorMessageLength) + "…";
            }
            catch (ArgumentException) { return null; }
            catch (InvalidOperationException) { return null; }
        }

        private static string ReadContent(string body, JavaScriptSerializer serializer)
        {
            try
            {
                Dictionary<string, object> root = serializer.DeserializeObject(body) as Dictionary<string, object>;
                object[] choices = root == null || !root.ContainsKey("choices") ? null : root["choices"] as object[];
                Dictionary<string, object> first = choices == null || choices.Length == 0 ? null : choices[0] as Dictionary<string, object>;
                Dictionary<string, object> message = first == null || !first.ContainsKey("message") ? null : first["message"] as Dictionary<string, object>;
                string content = message == null || !message.ContainsKey("content") ? null : message["content"] as string;
                if (content != null) return content;
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            throw new AiException(AiFailureKind.BadResponse, "模型服务返回的内容无法识别。");
        }
    }
}
