# Notes: Agent Framework 高级特性研究

## 源资料

### 1. Middleware（中间件）
**路径**: `agent-framework/samples/GettingStarted/Agents/Agent_Step14_Middleware/Program.cs`

**能力**:
- **Chat Client Middleware**: 在消息发送到 LLM 前后处理
- **Agent Run Middleware**: 在 Agent 执行前后处理
- **Function Invocation Middleware**: 在函数调用前后拦截

**应用场景**:
- PII 过滤（隐私信息脱敏）
- Guardrails（内容安全检查）
- 函数调用日志
- 函数结果覆盖
- 人工审批（Human-in-the-Loop）

**实现方式**:
```csharp
var agent = chatClient
    .AsBuilder()
    .Use(PIIMiddleware, null)              // 消息过滤
    .Use(GuardrailMiddleware, null)        // 安全检查
    .Use(FunctionCallMiddleware)           // 函数调用拦截
    .Build();
```

---

### 2. 条件工作流（Conditional Workflows）
**路径**: `agent-framework/samples/GettingStarted/Workflows/ConditionalEdges/01_EdgeCondition/Program.cs`

**能力**:
- **Edge Conditions**: 根据前一步的输出决定下一步走向
- **Shared State**: 在 Executor 之间共享状态
- **分支逻辑**: 不同条件走不同路径

**应用场景**:
- 垃圾邮件检测 → 正常邮件走回复流程，垃圾邮件走标记流程
- 评分判断 → 不达标重写，达标发布

**实现方式**:
```csharp
var workflow = new WorkflowBuilder(spamDetectionExecutor)
    .AddEdge(spamDetectionExecutor, emailAssistantExecutor,
        condition: result => !result.IsSpam)  // 条件边
    .AddEdge(spamDetectionExecutor, handleSpamExecutor,
        condition: result => result.IsSpam)
    .Build();
```

---

### 3. RAG（检索增强生成）
**路径**: `agent-framework/samples/GettingStarted/AgentWithRAG/`

**能力**:
- **向量存储**: 存储文档嵌入
- **相似度检索**: 根据查询检索相关文档
- **自定义数据源**: 实现自己的 RAG 数据源

**应用场景**:
- 从历史博客中检索相关内容
- 提供更准确的参考资料

**实现方式**:
```csharp
// 使用 IVectorStore 接口
var vectorStore = new TextSearchStore();
// 为 Agent 配置 RAG
agent.Options.VectorStore = vectorStore;
```

---

### 4. Human-in-the-Loop（人工介入）
**路径**: `agent-framework/samples/GettingStarted/Workflows/HumanInTheLoop/`

**能力**:
- **Checkpoint**: 工作流暂停点
- **人工确认**: 需要用户输入后继续
- **恢复执行**: 从暂停点继续

**应用场景**:
- 敏感操作需人工确认
- 审查不达标时人工决定是否重写

**实现方式**:
```csharp
// 使用 UserInputRequests
await context.YieldOutputAsync(new UserInputRequest(...));
```

---

### 5. Memory（记忆）
**路径**: `agent-framework/samples/GettingStarted/AgentWithMemory/`

**能力**:
- **对话历史**: 保持会话上下文
- **持久化存储**: 第三方存储（如 Redis）

**应用场景**:
- Agent 间上下文共享
- 多轮对话记忆

---

### 6. Shared State（共享状态）
**路径**: `agent-framework/samples/GettingStarted/Workflows/SharedStates/`

**能力**:
- **跨 Executor 状态共享**: 不同执行器之间共享数据
- **状态作用域**: 支持命名空间

**应用场景**:
- 在 Researcher 和 Writer 之间传递结构化数据
- 避免重复序列化/反序列化

---

## 综合发现

### 博客智能体可应用的增强

| 特性 | 优先级 | 应用场景 |
|------|--------|----------|
| **条件工作流** | 高 | 根据评分自动重写 |
| **Shared State** | 高 | Agent 间传递结构化数据 |
| **Middleware (Guardrails)** | 中 | 内容安全过滤 |
| **RAG** | 中 | 历史博客检索 |
| **Human-in-the-Loop** | 中 | 关键步骤人工确认 |
| **Memory** | 低 | 当前任务级记忆已足够 |

---

## 技术细节

### 条件边实现示例（评分判断）
```csharp
// 创建评分检查条件
Func<ReviewResult, bool> shouldRewrite = review => review.OverallScore < 80;

// 添加条件边
var workflow = new WorkflowBuilder(reviewerExecutor)
    .AddEdge(reviewerExecutor, rewriteExecutor, condition: shouldRewrite)
    .AddEdge(reviewerExecutor, publishExecutor, condition: review => review.OverallScore >= 80)
    .Build();
```

### Shared State 示例
```csharp
// 存储
await context.QueueStateUpdateAsync("research_result", result, "BlogScope");

// 读取
var research = await context.ReadStateAsync<ResearchResult>("research_result", "BlogScope");
```

### Middleware 示例（Guardrails）
```csharp
async Task<AgentRunResponse> ContentSafetyMiddleware(
    IEnumerable<ChatMessage> messages,
    AgentThread? thread,
    AgentRunOptions? options,
    AIAgent innerAgent,
    CancellationToken cancellationToken)
{
    var filteredMessages = FilterSensitiveContent(messages);
    var response = await innerAgent.RunAsync(filteredMessages, thread, options, cancellationToken);
    response.Messages = FilterSensitiveContent(response.Messages);
    return response;
}
```

---

## 实现记录

### Human-in-the-Loop 实现要点

1. **ApprovalRequiredAIFunction**
   - 用于包装需要人工审批的函数工具
   - 来自 `Microsoft.Extensions.AI` 命名空间
   - 当前是评估 API (MEAI001)，需要 `#pragma warning disable MEAI001`

2. **FunctionApprovalRequestContent**
   - 包含函数调用信息（函数名、参数）
   - 需要用户创建响应（批准/拒绝）
   - 通过 `AgentRunResponse.UserInputRequests` 获取

3. **实现模式**
```csharp
// 1. 创建需要审批的函数
var approvalFunction = new ApprovalRequiredAIFunction(originalFunction);

// 2. 执行 Agent
var response = await agent.RunAsync(input, thread);

// 3. 检查是否有待处理请求
while (response.UserInputRequests.Any())
{
    var approvals = response.UserInputRequests.OfType<FunctionApprovalRequestContent>();
    var responses = approvals.Select(a =>
        new ChatMessage(ChatRole.User, [a.CreateResponse(approved)]));
    response = await agent.RunAsync(responses, thread);
}
```

### 已知限制

1. **AnonymousDelegatingAIAgent** 是 internal 类，无法直接使用
2. **FunctionApprovalRequestContent** 是评估 API
3. **ChatOptions.Tools** 需要类型转换

### 创建的文件

**Middleware**:
- `PIIMiddleware.cs` - PII 过滤（手机号、邮箱、身份证等）
- `GuardrailMiddleware.cs` - 内容安全检查（敏感词过滤）
- `LoggingMiddleware.cs` - 日志和性能监控
- `AgentMiddlewareHelpers.cs` - 中间件辅助方法

**Human-in-the-Loop**:
- `HumanInLoopHelper.cs` - 人工介入辅助类
- `HumanInLoopWorkflowExtensions.cs` - 工作流扩展（含 WorkflowHumanInterventionManager）

### Shared State 实现记录

**已实现的功能**:
- `ResearcherExecutor` 存储任务信息、研究结果、初始化重写次数
- `WriterExecutor` 读取任务信息，存储草稿内容
- `ReviewerExecutor` 存储审查结果
- `RewriteExecutor` 读取/更新重写次数、读取研究内容和任务信息

**Shared State 常量** (`BlogStateConstants.cs`):
- `BlogStateScope` - 博客状态作用域
- `TaskInfoKey` - 任务信息
- `ResearchResultKey` - 研究结果
- `DraftContentKey` - 草稿内容
- `ReviewResultKey` - 审查结果
- `RewriteCountKey` - 重写次数
- `MaxRewriteCount` - 最大重写次数 (3)

**API 使用**:
```csharp
// 写入
await context.QueueStateUpdateAsync(key, value, scopeName, cancellationToken);

// 读取
var value = await context.ReadStateAsync<T>(key, scopeName, cancellationToken);
```

