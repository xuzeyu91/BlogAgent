using BlogAgent.Domain.Services.Agents.Base;
using BlogAgent.Domain.Common.Extensions;
using BlogAgent.Domain.Domain.Dto;
using BlogAgent.Domain.Domain.Enum;
using BlogAgent.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace BlogAgent.Domain.Services.Agents
{
    /// <summary>
    /// 质量审查专家Agent
    /// </summary>
    [ServiceDescription(typeof(ReviewerAgent), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped)]
    public class ReviewerAgent : BaseAgentService
    {
        public override string AgentName => "质量审查专家";

        public override AgentType AgentType => AgentType.Reviewer;

        protected override string Instructions => @"你是一位严格的技术内容审查专家,负责对博客文章进行全面质量评估。

**审查标准:**

1. **准确性(40分)**: 
   - 技术概念定义是否准确
   - 代码示例是否正确可运行
   - 引用数据是否真实可靠
   - 是否包含过时或错误信息

2. **逻辑性(30分)**:
   - 文章结构是否清晰、层次分明
   - 论证是否充分、有理有据
   - 段落衔接是否自然流畅
   - 是否存在跳跃式思维或逻辑漏洞

3. **原创性(20分)**:
   - 是否有独特见解和深度分析
   - 是否是简单资料堆砌
   - 案例是否具有实战价值
   - 是否避免空洞套话

4. **规范性(10分)**:
   - Markdown格式是否规范
   - 代码块是否正确标注语言
   - 中英文标点是否符合规范
   - 专业术语使用是否统一

**输出格式(严格按照JSON格式,不要添加任何其他文字):**

```json
{
  ""overallScore"": 85,
  ""accuracy"": {
    ""score"": 38,
    ""issues"": [""问题描述1"", ""问题描述2""]
  },
  ""logic"": {
    ""score"": 25,
    ""issues"": [""问题描述1""]
  },
  ""originality"": {
    ""score"": 18,
    ""issues"": []
  },
  ""formatting"": {
    ""score"": 9,
    ""issues"": [""问题描述1""]
  },
  ""recommendation"": ""通过"",
  ""summary"": ""总体评价和具体修改建议""
}
```

**评分规则:**
- 总分 ≥ 80分: recommendation为""通过""
- 70 ≤ 总分 < 80: recommendation为""需修改""
- 总分 < 70: recommendation为""不通过""

**注意事项:**
- 评分要客观公正,不要过于严苛或宽松
- issues数组中的每个问题要具体明确,指出位置
- summary要给出可操作的修改建议
- 必须严格按照JSON格式输出,不要有多余文字";

        protected override float Temperature => 0.3f; // 降低温度以提高输出稳定性

        public ReviewerAgent(
            ILogger<ReviewerAgent> logger,
            AgentExecutionRepository executionRepository,
            McpConfigService mcpConfigService)
            : base(logger, executionRepository, mcpConfigService)
        {
        }

        /// <summary>
        /// 执行质量审查任务
        /// </summary>
        /// <param name="title">博客标题</param>
        /// <param name="content">博客内容</param>
        /// <param name="taskId">任务ID</param>
        /// <returns>审查结果</returns>
        public async Task<ReviewResultDto> ReviewAsync(string title, string content, int taskId)
        {
            var input = $@"请审查以下博客文章:

**标题:** {title}

**内容:**
{content}

请严格按照JSON格式输出审查结果,不要添加任何解释性文字。";

            var output = await ExecuteAsync(input, taskId);

            // 解析JSON响应
            var result = ParseJsonResponse<ReviewResultDto>(output);

            if (result == null)
            {
                _logger.LogWarning($"[{AgentName}] JSON解析失败,返回默认审查结果");
                
                // 返回默认的低分结果
                return new ReviewResultDto
                {
                    OverallScore = 50,
                    Accuracy = new DimensionScore { Score = 20, Issues = new List<string> { "AI响应格式错误,无法准确评估" } },
                    Logic = new DimensionScore { Score = 15, Issues = new List<string>() },
                    Originality = new DimensionScore { Score = 10, Issues = new List<string>() },
                    Formatting = new DimensionScore { Score = 5, Issues = new List<string>() },
                    Recommendation = "需修改",
                    Summary = "审查结果解析失败,建议人工检查文章质量"
                };
            }

            return result;
        }
    }
}

