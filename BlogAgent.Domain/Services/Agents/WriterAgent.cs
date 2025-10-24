using BlogAgent.Domain.Services.Agents.Base;
using BlogAgent.Domain.Common.Extensions;
using BlogAgent.Domain.Domain.Dto;
using BlogAgent.Domain.Domain.Enum;
using BlogAgent.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace BlogAgent.Domain.Services.Agents
{
    /// <summary>
    /// 博客撰写专家Agent
    /// </summary>
    [ServiceDescription(typeof(WriterAgent), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped)]
    public class WriterAgent : BaseAgentService
    {
        public override string AgentName => "博客撰写专家";

        public override AgentType AgentType => AgentType.Writer;

        protected override string Instructions => @"你是一位资深技术博客作家,擅长将技术内容转化为通俗易懂的文章。

**任务:**
1. 基于提供的资料摘要撰写技术博客
2. 确保逻辑清晰、层次分明
3. 适当添加代码示例和实战案例
4. 语言专业但通俗易懂

**文章结构(严格按照以下格式):**

# 标题
> 引言(一段吸引读者的导语,阐明文章价值)

## 一、背景介绍
[技术背景、为什么需要这个技术、解决什么问题]

## 二、核心概念
[关键概念详细解释,必要时配图说明]

## 三、实战应用
[代码示例和使用场景]

### 3.1 场景一
```csharp
// 代码示例
```

### 3.2 场景二
```csharp
// 代码示例
```

## 四、最佳实践
[经验总结、注意事项、常见陷阱]

## 五、总结
[全文总结、技术展望、延伸阅读建议]

**质量标准:**
- 字数不少于1500字(根据用户要求调整)
- 代码示例使用```代码块,标注语言类型
- 避免空洞的套话和无意义的形容词
- 逻辑流畅,前后呼应
- 专业术语首次出现时给予解释
- 适当使用列表、表格等提升可读性";

        protected override int MaxTokens => 6000; // 增加Token限制以支持长文

        public WriterAgent(
            ILogger<WriterAgent> logger,
            AgentExecutionRepository executionRepository,
            McpConfigService mcpConfigService)
            : base(logger, executionRepository, mcpConfigService)
        {
        }

        /// <summary>
        /// 执行博客撰写任务
        /// </summary>
        /// <param name="topic">博客主题</param>
        /// <param name="researchSummary">资料摘要</param>
        /// <param name="requirements">写作要求</param>
        /// <param name="taskId">任务ID</param>
        /// <returns>博客初稿</returns>
        public async Task<DraftContentDto> WriteAsync(
            string topic, 
            string researchSummary, 
            WritingRequirements? requirements,
            int taskId)
        {
            var requirementsText = requirements != null
                ? $@"
**写作要求:**
- 目标字数: {requirements.TargetWordCount ?? 1500}字
- 写作风格: {requirements.Style ?? "专业易懂"}
- 目标读者: {requirements.TargetAudience ?? "中级开发者"}"
                : "";

            var input = $@"**主题:** {topic}

**资料摘要:**
{researchSummary}
{requirementsText}

请基于以上资料撰写一篇高质量的技术博客,严格按照规定的结构和格式输出。";

            var output = await ExecuteAsync(input, taskId);

            return new DraftContentDto
            {
                Title = ExtractTitle(output),
                Content = output,
                WordCount = CountWords(output),
                GeneratedAt = DateTime.Now
            };
        }
    }
}

