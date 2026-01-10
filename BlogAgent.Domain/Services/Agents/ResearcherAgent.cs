using BlogAgent.Domain.Services.Agents.Base;
using BlogAgent.Domain.Common.Extensions;
using BlogAgent.Domain.Domain.Dto;
using BlogAgent.Domain.Domain.Enum;
using BlogAgent.Domain.Domain.Model;
using BlogAgent.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;
using System.Text.Json;
using System.ComponentModel;

namespace BlogAgent.Domain.Services.Agents
{
    /// <summary>
    /// 资料收集专家Agent - 使用 Agent Framework
    /// </summary>
    [ServiceDescription(typeof(ResearcherAgent), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped)]
    public class ResearcherAgent : BaseAgentService
    {
        public override string AgentName => "资料收集专家";

        public override AgentType AgentType => AgentType.Researcher;

        protected override string Instructions => @"你是一位专业的技术资料收集专家。

**任务:**
1. 仔细阅读用户提供的主题和参考资料
2. 提取关键信息点(技术概念、代码示例、最佳实践、应用场景等)
3. 整理成结构化的JSON格式输出

**输出要求:**
- topic_analysis: 对主题的理解和定位,包括技术背景、适用场景、目标读者
- key_points: 核心要点列表,每个要点包含重要程度(1-3,3最高)和内容
- technical_details: 技术细节列表,每个包含标题和详细说明
- code_examples: 代码示例列表(如果有),包含语言、代码和描述
- references: 参考来源列表

**质量要求:**
- 信息准确,不添加未提供的内容
- 结构清晰,层次分明
- 提炼核心概念,避免冗余
- 如果资料不足,明确指出缺失的部分";

        // 使用结构化输出
        protected override ChatResponseFormat? ResponseFormat => 
            ChatResponseFormat.ForJsonSchema<ResearchOutput>(schemaName: "ResearchOutput");

        // 添加工具函数
        protected override IEnumerable<Microsoft.Extensions.AI.AITool>? Tools => new[]
        {
            Microsoft.Extensions.AI.AIFunctionFactory.Create(CountWordsInText),
            Microsoft.Extensions.AI.AIFunctionFactory.Create(ExtractCodeBlocks)
        };

        public ResearcherAgent(
            ILogger<ResearcherAgent> logger,
            AgentExecutionRepository executionRepository,
            McpConfigService mcpConfigService)
            : base(logger, executionRepository, mcpConfigService)
        {
        }

        /// <summary>
        /// 执行资料收集任务
        /// </summary>
        /// <param name="topic">博客主题</param>
        /// <param name="referenceContent">参考资料</param>
        /// <param name="taskId">任务ID</param>
        /// <returns>资料收集结果</returns>
        public async Task<ResearchResultDto> ResearchAsync(string topic, string referenceContent, int taskId)
        {
            var input = $@"**主题:** {topic}

**参考资料:**
{referenceContent}

请按照指示整理资料,输出结构化的JSON格式数据。";

            var output = await ExecuteAsync(input, taskId);

            // 解析结构化输出 - 使用 ParseJsonResponse 提供更好的容错性
            var researchOutput = ParseJsonResponse<ResearchOutput>(output);

            if (researchOutput == null)
            {
                _logger.LogError($"[ResearcherAgent] 无法解析研究结果, 原始输出: {output?.Substring(0, Math.Min(200, output?.Length ?? 0))}...");
                throw new InvalidOperationException("无法解析研究结果, AI 返回的内容不是有效的 JSON 格式");
            }

            // 转换为 Markdown 格式(保持向后兼容)
            var markdown = ConvertToMarkdown(researchOutput);

            return new ResearchResultDto
            {
                Summary = markdown,
                KeyPoints = researchOutput.KeyPoints.Select(kp => kp.Content).ToList(),
                Timestamp = DateTime.Now
            };
        }

        /// <summary>
        /// 将结构化输出转换为 Markdown 格式
        /// </summary>
        private string ConvertToMarkdown(ResearchOutput output)
        {
            var markdown = new System.Text.StringBuilder();

            markdown.AppendLine("## 主题分析");
            markdown.AppendLine(output.TopicAnalysis);
            markdown.AppendLine();

            markdown.AppendLine("## 核心要点");
            foreach (var point in output.KeyPoints.OrderByDescending(p => p.Importance))
            {
                var stars = new string('⭐', point.Importance);
                markdown.AppendLine($"{stars} {point.Content}");
            }
            markdown.AppendLine();

            markdown.AppendLine("## 技术细节");
            foreach (var detail in output.TechnicalDetails)
            {
                markdown.AppendLine($"### {detail.Title}");
                markdown.AppendLine(detail.Description);
                markdown.AppendLine();
            }

            if (output.CodeExamples.Any())
            {
                markdown.AppendLine("## 代码示例");
                foreach (var example in output.CodeExamples)
                {
                    markdown.AppendLine($"```{example.Language}");
                    markdown.AppendLine(example.Code);
                    markdown.AppendLine("```");
                    markdown.AppendLine(example.Description);
                    markdown.AppendLine();
                }
            }

            markdown.AppendLine("## 参考来源");
            foreach (var reference in output.References)
            {
                markdown.AppendLine($"- {reference}");
            }

            return markdown.ToString();
        }

        // ============ Agent Tools ============

        /// <summary>
        /// 工具函数: 统计文本字数
        /// </summary>
        [Description("统计给定文本的字数,包括中文字符和英文单词")]
        private static int CountWordsInText([Description("要统计的文本内容")] string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            int chineseChars = text.Count(c => c >= 0x4E00 && c <= 0x9FA5);
            int englishWords = text.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Count(word => word.Any(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')));

            return chineseChars + englishWords;
        }

        /// <summary>
        /// 工具函数: 提取代码块
        /// </summary>
        [Description("从Markdown文本中提取所有代码块")]
        private static string ExtractCodeBlocks([Description("Markdown格式的文本")] string markdown)
        {
            var codeBlocks = new List<string>();
            var lines = markdown.Split('\n');
            bool inCodeBlock = false;
            var currentBlock = new System.Text.StringBuilder();

            foreach (var line in lines)
            {
                if (line.Trim().StartsWith("```"))
                {
                    if (inCodeBlock)
                    {
                        codeBlocks.Add(currentBlock.ToString());
                        currentBlock.Clear();
                        inCodeBlock = false;
                    }
                    else
                    {
                        inCodeBlock = true;
                    }
                }
                else if (inCodeBlock)
                {
                    currentBlock.AppendLine(line);
                }
            }

            return string.Join("\n---\n", codeBlocks);
        }
    }
}


