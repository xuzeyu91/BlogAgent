# Task Plan: 使用 Agent Framework 能力提升博客智能体智能程度

## 目标
利用 Microsoft Agent Framework 的高级能力（RAG、Memory、Middleware、条件工作流、Human-in-the-Loop）来提升博客智能体的智能程度和用户体验。

## 当前状态分析

### 现有实现
- **三个基础 Agent**: ResearcherAgent（资料收集）、WriterAgent（博客撰写）、ReviewerAgent（质量审查）
- **顺序工作流**: 使用 `AgentWorkflowBuilder.BuildSequential` 简单顺序执行
- **结构化输出**: 已使用 `ChatResponseFormat.ForJsonSchema`
- **基础工具**: 字数统计、代码块提取
- **MCP 支持**: 已集成 MCP 工具

### 智能化不足的问题
1. **无知识库检索**: 每次都从零开始，无法利用历史博客内容
2. **无记忆机制**: Agent 之间无法共享中间上下文
3. **工作流僵化**: 只能顺序执行，无法根据评分自动决定是否重写
4. **无安全防护**: 没有 guardrails 和内容过滤
5. **无人工干预**: 无法在关键步骤让人工介入

## 实施阶段

### Phase 1: 研究与规划
- [x] 分析当前项目架构
- [x] 研究 Agent Framework 高级特性
- [x] 设计增强方案

### Phase 2: 条件工作流实现
- [x] 创建 Shared State 常量（BlogStateConstants）
- [x] 创建消息类型（BlogTaskInput, ResearchResultOutput, DraftContentOutput, ReviewResultOutput）
- [x] 创建 Executor 类（ResearcherExecutor, WriterExecutor, ReviewerExecutor, RewriteExecutor, PublishExecutor, FailureExecutor）
- [x] 创建 BlogAgentWorkflowServiceV2（条件工作流服务）
- [x] 更新 WorkflowProgressDto 支持执行器日志
- [x] 修复所有编译错误
- [x] 集成到 UI（让用户可以选择使用 V2 工作流）
- [x] 测试条件工作流的实际运行

### Phase 3: Middleware 安全防护
- [x] 实现 PII 过滤中间件
- [x] 实现 Guardrails（内容安全检查）
- [x] 实现日志记录中间件
- [x] 实现性能监控中间件
- [x] 创建中间件辅助类
- [x] 修复所有编译错误

### Phase 6: Human-in-the-Loop
- [x] 实现关键步骤人工确认
- [x] 添加函数调用审批机制
- [x] 支持工作流中断和恢复

### Phase 4: Memory 记忆机制
- [x] 实现 Agent 间共享状态（Shared State）
- [x] 添加对话历史记忆
- [x] 创建上下文传递机制

### Phase 4: 条件工作流
- [x] 实现条件边（Edge Conditions）- 根据评分决定下一步
- [x] 添加循环重写机制（评分不达标自动重写）
- [x] 实现分支逻辑（不同类型主题走不同流程）

### Phase 5: Middleware 安全防护
- [x] 实现 PII 过滤中间件
- [x] 实现 Guardrails（内容安全检查）
- [x] 添加敏感词过滤

### Phase 6: Human-in-the-Loop
- [x] 实现关键步骤人工确认
- [x] 添加函数调用审批机制
- [x] 支持工作流中断和恢复

## 关键问题

1. **RAG 存储**: 使用哪种向量存储？（考虑 SQLite 全文搜索、Redis、或外部向量服务）
2. **记忆持久化**: 使用数据库还是内存存储共享状态？
3. **UI 更新**: 如何在 Blazor UI 中实时显示条件分支和人工介入点？

**决策**:
- RAG 存储: 暂缓实施，优先实现条件工作流
- Shared State: 使用 Agent Framework 内置的 Shared State 机制
- UI 更新: 通过 `WorkflowProgressDto` 扩展支持显示分支路径

## 决策记录

- **向量存储方案**: 暂缓 - 优先实现条件工作流和 Shared State
- **工作流架构**: 使用 WorkflowBuilder 替代 AgentWorkflowBuilder，实现条件分支
- **共享状态**: 使用 Agent Framework 内置 Shared State，避免重复序列化
- **实施优先级**: 条件工作流 > Shared State > Middleware > Human-in-the-Loop > RAG

## 错误记录

（暂无）

## 状态
**项目完成** - 已实现条件工作流（V2）、Middleware 安全防护、Human-in-the-Loop 和 Shared State

## 已完成功能

### Phase 2: 条件工作流实现 ✅
- 创建了 `BlogStateConstants` 定义共享状态常量
- 创建了消息类型：`BlogTaskInput`, `ResearchResultOutput`, `DraftContentOutput`, `ReviewResultOutput`
- 创建了 Executor 类：`ResearcherExecutor`, `WriterExecutor`, `ReviewerExecutor`, `RewriteExecutor`, `PublishExecutor`, `FailureExecutor`
- 创建了 `BlogAgentWorkflowServiceV2` 条件工作流服务
- 更新了 `WorkflowProgressDto` 支持执行器日志
- 集成到 UI，用户可以在 V1 和 V2 工作流之间选择

### Phase 4: Shared State 记忆机制 ✅
- `ResearcherExecutor` 存储任务信息、研究结果、初始化重写次数
- `WriterExecutor` 读取任务信息，存储草稿内容
- `ReviewerExecutor` 存储审查结果
- `RewriteExecutor` 读取/更新重写次数、读取研究内容和任务信息

### Phase 3: Middleware 安全防护 ✅
- 创建了 `PIIMiddleware` - 过滤个人隐私信息（手机号、邮箱、身份证等）
- 创建了 `GuardrailMiddleware` - 内容安全检查（敏感词过滤）
- 创建了 `LoggingMiddleware` - 日志记录和性能监控
- 创建了 `AgentMiddlewareHelpers` - 提供便捷的中间件使用方式

### Phase 6: Human-in-the-Loop ✅
- 创建了 `HumanInLoopHelper` - 人工介入辅助类
- 创建了 `HumanInLoopWorkflowExtensions` - 工作流人工介入扩展
- 实现了函数调用审批机制
- 实现了工作流中断和恢复支持
- 集成到 UI，用户可以在 V1 和 V2 工作流之间选择

## 优先级调整

根据研究，确定实施优先级：

1. **高优先级**:
   - 条件工作流（根据评分自动重写）
   - Shared State（Agent 间传递结构化数据）

2. **中优先级**:
   - Middleware（Guardrails 安全检查）
   - Human-in-the-Loop（关键步骤确认）

3. **低优先级**:
   - RAG（需要额外的向量存储服务）
   - Memory（当前任务级记忆已足够）
