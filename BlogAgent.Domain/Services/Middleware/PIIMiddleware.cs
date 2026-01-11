using System.Text.RegularExpressions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace BlogAgent.Domain.Services.Middleware
{
    /// <summary>
    /// PII 过滤中间件 - 过滤个人隐私信息
    /// 包括：电话号码、邮箱地址、身份证号、银行卡号等
    /// </summary>
    public static class PIIMiddleware
    {
        /// <summary>
        /// PII 过滤中间件 - 在 Agent 运行前后过滤消息中的 PII 信息
        /// </summary>
        public static async Task<AgentRunResponse> FilterPIIAsync(
            IEnumerable<ChatMessage> messages,
            AgentThread? thread,
            AgentRunOptions? options,
            AIAgent innerAgent,
            CancellationToken cancellationToken,
            ILogger? logger = null)
        {
            // 过滤输入消息中的 PII 信息
            var filteredMessages = FilterMessages(messages);
            logger?.LogInformation("[PIIMiddleware] 已过滤输入消息中的 PII 信息");

            // 执行 Agent
            var response = await innerAgent.RunAsync(filteredMessages, thread, options, cancellationToken).ConfigureAwait(false);

            // 过滤输出消息中的 PII 信息
            response.Messages = FilterMessages(response.Messages);
            logger?.LogInformation("[PIIMiddleware] 已过滤输出消息中的 PII 信息");

            return response;
        }

        /// <summary>
        /// 过滤消息列表中的 PII 信息
        /// </summary>
        private static IList<ChatMessage> FilterMessages(IEnumerable<ChatMessage> messages)
        {
            return messages.Select(m => new ChatMessage(m.Role, FilterPII(m.Text))).ToList();
        }

        /// <summary>
        /// 过滤文本中的 PII 信息
        /// </summary>
        public static string FilterPII(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return content;

            // PII 检测的正则表达式模式
            var piiPatterns = new List<(Regex Pattern, string Replacement)>
            {
                // 手机号码（中国大陆）
                (new Regex(@"\b1[3-9]\d{9}\b", RegexOptions.Compiled), "[手机号]"),
                (new Regex(@"\b\d{3}-\d{4}-\d{4}\b", RegexOptions.Compiled), "[手机号]"),
                (new Regex(@"\b\d{3}-\d{3}-\d{4}\b", RegexOptions.Compiled), "[电话号码]"),

                // 电子邮件地址
                (new Regex(@"\b[\w\.-]+@[\w\.-]+\.\w+\b", RegexOptions.Compiled), "[邮箱]"),

                // 身份证号码（中国大陆 18 位）
                (new Regex(@"\b[1-9]\d{5}(19|20)\d{2}(0[1-9]|1[0-2])(0[1-9]|[12]\d|3[01])\d{3}[\dXx]\b", RegexOptions.Compiled), "[身份证号]"),

                // 银行卡号（16-19 位数字）
                (new Regex(@"\b\d{16,19}\b", RegexOptions.Compiled), "[银行卡号]"),

                // IP 地址
                (new Regex(@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b", RegexOptions.Compiled), "[IP地址]"),

                // QQ 号码（5-12 位数字）
                (new Regex(@"\bQQ[:\s]*[1-9]\d{4,11}\b", RegexOptions.Compiled), "[QQ号]"),
                (new Regex(@"\b[1-9]\d{4,11}\b", RegexOptions.Compiled), "[数字ID]"),

                // 微信号（字母开头，6-20位字母数字下划线）
                (new Regex(@"\b微信[:\s]*[a-zA-Z][a-zA-Z0-9_]{5,19}\b", RegexOptions.Compiled), "[微信号]"),

                // 地址信息（简单检测）
                (new Regex(@"[北京市天津市上海市重庆市][^，,。\n]{2,8}[区县][^，,。\n]{2,10}[路街道巷号]", RegexOptions.Compiled), "[地址]"),
            };

            foreach (var (pattern, replacement) in piiPatterns)
            {
                content = pattern.Replace(content, replacement);
            }

            return content;
        }

        /// <summary>
        /// 检测文本中是否包含 PII 信息
        /// </summary>
        public static bool ContainsPII(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return false;

            var piiPatterns = new[]
            {
                new Regex(@"\b1[3-9]\d{9}\b", RegexOptions.Compiled), // 手机号
                new Regex(@"\b[\w\.-]+@[\w\.-]+\.\w+\b", RegexOptions.Compiled), // 邮箱
                new Regex(@"\b[1-9]\d{5}(19|20)\d{2}(0[1-9]|1[0-2])(0[1-9]|[12]\d|3[01])\d{3}[\dXx]\b", RegexOptions.Compiled), // 身份证
                new Regex(@"\b\d{16,19}\b", RegexOptions.Compiled), // 银行卡
            };

            return piiPatterns.Any(pattern => pattern.IsMatch(content));
        }

        /// <summary>
        /// 提取文本中的 PII 信息（用于审计日志）
        /// </summary>
        public static Dictionary<string, List<string>> ExtractPIIInfo(string content)
        {
            var result = new Dictionary<string, List<string>>();

            if (string.IsNullOrWhiteSpace(content))
                return result;

            // 手机号码
            var phonePattern = new Regex(@"\b1[3-9]\d{9}\b");
            var phones = phonePattern.Matches(content).Select(m => m.Value).ToList();
            if (phones.Any())
                result["手机号"] = phones;

            // 电子邮件
            var emailPattern = new Regex(@"\b[\w\.-]+@[\w\.-]+\.\w+\b");
            var emails = emailPattern.Matches(content).Select(m => m.Value).ToList();
            if (emails.Any())
                result["邮箱"] = emails;

            // 身份证号
            var idCardPattern = new Regex(@"\b[1-9]\d{5}(19|20)\d{2}(0[1-9]|1[0-2])(0[1-9]|[12]\d|3[01])\d{3}[\dXx]\b");
            var idCards = idCardPattern.Matches(content).Select(m => m.Value).ToList();
            if (idCards.Any())
                result["身份证号"] = idCards;

            return result;
        }
    }
}
