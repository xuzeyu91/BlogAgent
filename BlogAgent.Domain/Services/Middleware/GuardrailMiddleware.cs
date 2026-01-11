using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace BlogAgent.Domain.Services.Middleware
{
    /// <summary>
    /// Guardrail 中间件 - 内容安全检查
    /// 包括：敏感词过滤、违禁内容检测、不当言论过滤等
    /// </summary>
    public static class GuardrailMiddleware
    {
        /// <summary>
        /// 默认的违禁关键词列表
        /// </summary>
        private static readonly string[] DefaultForbiddenKeywords =
        [
            // 暴力相关
            "杀人", "杀戮", "谋杀", "屠杀", "暴力", "殴打", "伤害", "袭击",

            // 违法相关
            "毒品", "吸毒", "贩毒", "制毒", "走私", "诈骗", "偷窃", "抢劫", "盗窃",

            // 色情相关
            "色情", "淫秽", "裸体", "性交", "做爱", "情色", "成人内容",

            // 诈骗相关
            "赌博", "博彩", "六合彩", "赌球", "网络赌博",

            // 极端主义
            "恐怖主义", "极端主义", "邪教", "邪教组织",

            // 自残相关
            "自杀", "自残", "割腕", "跳楼", "上吊",

            // 歧视言论
            "种族歧视", "性别歧视", "仇恨言论",
        ];

        /// <summary>
        /// Guardrail 中间件 - 在 Agent 运行前后进行内容安全检查
        /// </summary>
        public static async Task<AgentRunResponse> GuardrailAsync(
            IEnumerable<ChatMessage> messages,
            AgentThread? thread,
            AgentRunOptions? options,
            AIAgent innerAgent,
            CancellationToken cancellationToken,
            ILogger? logger = null,
            IEnumerable<string>? forbiddenKeywords = null)
        {
            var keywords = forbiddenKeywords ?? DefaultForbiddenKeywords;

            // 检查并过滤输入消息
            var (filteredMessages, inputBlocked) = FilterMessages(messages, keywords);
            if (inputBlocked.Blocked)
            {
                logger?.LogWarning("[GuardrailMiddleware] 输入消息被拦截: {Reason}", inputBlocked.Reason);

                // 返回拦截消息
                return new AgentRunResponse
                {
                    Messages = new List<ChatMessage>
                    {
                        new ChatMessage(ChatRole.Assistant, GetBlockedMessage(inputBlocked.Reason))
                    }
                };
            }

            logger?.LogInformation("[GuardrailMiddleware] 输入消息安全检查通过");

            // 执行 Agent
            var response = await innerAgent.RunAsync(filteredMessages, thread, options, cancellationToken).ConfigureAwait(false);

            // 检查并过滤输出消息
            var (filteredOutput, outputBlocked) = FilterMessages(response.Messages, keywords);
            if (outputBlocked.Blocked)
            {
                logger?.LogWarning("[GuardrailMiddleware] 输出消息被拦截: {Reason}", outputBlocked.Reason);

                response.Messages = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.Assistant, GetBlockedMessage(outputBlocked.Reason))
                };
            }
            else
            {
                response.Messages = filteredOutput;
                logger?.LogInformation("[GuardrailMiddleware] 输出消息安全检查通过");
            }

            return response;
        }

        /// <summary>
        /// 过滤消息列表中的违禁内容
        /// </summary>
        private static (IList<ChatMessage> FilteredMessages, BlockResult BlockResult) FilterMessages(
            IEnumerable<ChatMessage> messages,
            IEnumerable<string> forbiddenKeywords)
        {
            var filteredMessages = new List<ChatMessage>();
            var allViolations = new List<string>();

            foreach (var message in messages)
            {
                var (filteredContent, violations) = FilterContent(message.Text, forbiddenKeywords);

                if (violations.Count > 0)
                {
                    allViolations.AddRange(violations);
                }

                filteredMessages.Add(new ChatMessage(message.Role, filteredContent));
            }

            if (allViolations.Count > 0)
            {
                // 如果有违禁内容，检查是否需要完全拦截
                if (ShouldBlock(allViolations))
                {
                    return (filteredMessages, new BlockResult
                    {
                        Blocked = true,
                        Reason = $"检测到严重违禁内容: {string.Join(", ", allViolations.Distinct())}"
                    });
                }
            }

            return (filteredMessages, new BlockResult { Blocked = false });
        }

        /// <summary>
        /// 过滤文本中的违禁内容
        /// </summary>
        public static (string FilteredContent, List<string> Violations) FilterContent(
            string content,
            IEnumerable<string>? forbiddenKeywords = null)
        {
            var keywords = forbiddenKeywords ?? DefaultForbiddenKeywords;
            var violations = new List<string>();
            var filteredContent = content;

            foreach (var keyword in keywords)
            {
                if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add(keyword);

                    // 用星号替换违禁词
                    var replacement = new string('*', keyword.Length);
                    filteredContent = Regex.Replace(filteredContent, Regex.Escape(keyword), replacement, RegexOptions.IgnoreCase);
                }
            }

            return (filteredContent, violations);
        }

        /// <summary>
        /// 判断是否应该完全拦截内容（基于严重程度）
        /// </summary>
        private static bool ShouldBlock(List<string> violations)
        {
            // 严重违禁词列表（完全拦截）
            var severeKeywords = new[]
            {
                "毒品", "吸毒", "色情", "淫秽", "恐怖主义", "邪教", "自杀", "自残"
            };

            return violations.Any(v => severeKeywords.Any(k => v.Contains(k, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// 获取拦截提示消息
        /// </summary>
        private static string GetBlockedMessage(string reason)
        {
            return $"[系统提示] 抱歉，您的内容包含违规信息，已被拦截。\n\n原因：{reason}\n\n请修改后重试。";
        }

        /// <summary>
        /// 检查内容是否安全（不包含违禁内容）
        /// </summary>
        public static (bool IsSafe, List<string> Violations) CheckContentSafety(
            string content,
            IEnumerable<string>? forbiddenKeywords = null)
        {
            var (filteredContent, violations) = FilterContent(content, forbiddenKeywords);
            return (violations.Count == 0, violations);
        }

        /// <summary>
        /// 获取违禁内容统计报告
        /// </summary>
        public static ContentSafetyReport GetSafetyReport(
            string content,
            IEnumerable<string>? forbiddenKeywords = null)
        {
            var keywords = forbiddenKeywords ?? DefaultForbiddenKeywords;
            var violations = new Dictionary<string, int>();

            foreach (var keyword in keywords)
            {
                var count = Regex.Matches(content, Regex.Escape(keyword), RegexOptions.IgnoreCase).Count;
                if (count > 0)
                {
                    violations[keyword] = count;
                }
            }

            var isSafe = violations.Count == 0;

            return new ContentSafetyReport
            {
                IsSafe = isSafe,
                ViolationCount = violations.Values.Sum(),
                Violations = violations,
                Severity = AssessSeverity(violations)
            };
        }

        /// <summary>
        /// 评估违禁内容的严重程度
        /// </summary>
        private static string AssessSeverity(Dictionary<string, int> violations)
        {
            if (violations.Count == 0)
                return "无";

            var severeKeywords = new[] { "毒品", "色情", "恐怖主义", "邪教", "自杀" };
            var hasSevere = violations.Keys.Any(k => severeKeywords.Any(s => k.Contains(s, StringComparison.OrdinalIgnoreCase)));

            if (hasSevere)
                return "严重";

            if (violations.Values.Sum() > 5)
                return "高";

            if (violations.Values.Sum() > 2)
                return "中";

            return "低";
        }

        /// <summary>
        /// 拦截结果
        /// </summary>
        private record BlockResult
        {
            public bool Blocked { get; init; }
            public string Reason { get; init; } = string.Empty;
        }

        /// <summary>
        /// 内容安全报告
        /// </summary>
        public class ContentSafetyReport
        {
            public bool IsSafe { get; init; }
            public int ViolationCount { get; init; }
            public Dictionary<string, int> Violations { get; init; } = new();
            public string Severity { get; init; } = "无";

            public override string ToString()
            {
                if (IsSafe)
                    return "内容安全，未检测到违禁内容";

                var violationList = Violations.Select(kv => $"  - {kv.Key}: {kv.Value}次");
                return $"内容不安全 (严重程度: {Severity})\n检测到 {ViolationCount} 处违禁内容:\n{string.Join("\n", violationList)}";
            }
        }
    }
}
