# HealthHelper - 智能健康助手

## 项目简介

HealthHelper 是一个基于 Avalonia UI 和 .NET 的智能健康助手应用，使用 DeepSeek AI 技术帮助用户记录和分析每日健康数据，提供专业级别的个性化健康建议。

## 主要功能

### 📊 健康数据记录
- **睡眠记录**：入睡时间、起床时间、睡眠质量评分（1-10分）
- **饮水记录**：每日饮水量目标和实际饮水量，自动判断是否达标
- **运动记录**：运动时长和久坐时长

### 🤖 AI 健康建议
- 基于最近7天健康数据分析
- DeepSeek AI 模型提供个性化建议
- 本地智能分析算法（离线模式）
- 专业健康顾问级别的建议生成

### 💾 数据存储
- SQLite 数据库本地存储
- 自动清理7天前的数据
- 数据持久化和安全性保障

## 技术架构

### 核心技术栈
- **UI框架**：Avalonia UI (跨平台桌面应用)
- **语言**：C# 12.0
- **模式**：MVVM (Model-View-ViewModel)
- **数据库**：SQLite
- **依赖注入**：Microsoft.Extensions.DependencyInjection

### 项目结构
```
HealthHelper/
├── Models/                 # 数据模型
│   ├── HealthRecords.cs    # 健康数据记录类型
│   └── AdviceModels.cs     # 建议模型
├── Services/               # 服务层
│   ├── Clients/            # 外部服务客户端
│   │   └── LargeLanguageModelClient.cs  # DeepSeek AI 客户端
│   ├── Contracts/          # 服务接口定义
│   └── Implementations/    # 服务实现
│       ├── HealthInsightsService.cs
│       └── SystemClock.cs
├── ViewModels/             # 视图模型
│   ├── InputViewModel.cs   # 输入界面逻辑
│   └── AdviceViewModel.cs  # 建议界面逻辑
├── Views/                  # 用户界面
│   ├── InputView.axaml     # 数据输入界面
│   └── AdviceView.axaml    # 建议展示界面
├── Navigation/             # 页面导航
├── Configuration/          # 配置选项
└── App.axaml.cs            # 应用启动配置
```

## 使用指南

### 1. 环境配置
- 安装 .NET 8.0 SDK
- 安装 Avalonia UI 工具（可选）

### 2. 构建项目
```bash
# 克隆项目
git clone <repository-url>
cd Health-Helper

# 构建项目
dotnet build
```

### 3. 运行项目（推荐方式）

#### 方式1：使用启动脚本（推荐）
```bash
# Windows用户：双击运行
Run-HealthHelper.bat

# 或使用PowerShell脚本
.\Set-DeepSeek-Env.ps1
```

#### 方式2：手动设置环境变量
```bash
# 先设置环境变量
set DEEPSEEK_API_KEY=sk-edb4ae50b8044f099e56ce138d88579c
set DEEPSEEK_BASE_URL=https://api.deepseek.com

# 然后运行项目
dotnet run --project HealthHelper
```

### 3. AI 模型配置

HealthHelper 使用 DeepSeek AI 模型提供智能健康建议。

#### DeepSeek API 配置

**🎉 好消息！现在无需手动配置，开箱即用！**

应用程序已经内置了完整的DeepSeek API配置：

```
✅ API密钥: sk-edb4ae50b8044f099e56ce138d88579c (已内置)
✅ API地址: https://api.deepseek.com (已内置)
```

**直接运行即可使用AI功能！**

#### 如果需要自定义配置（可选）

如果您想使用自己的API密钥，可以设置环境变量来覆盖内置配置：

```bash
# Windows PowerShell
$env:DEEPSEEK_API_KEY="your-custom-api-key"
$env:DEEPSEEK_BASE_URL="https://api.deepseek.com"

# Windows CMD
set DEEPSEEK_API_KEY=your-custom-api-key
set DEEPSEEK_BASE_URL=https://api.deepseek.com

# Linux/macOS
export DEEPSEEK_API_KEY="your-custom-api-key"
export DEEPSEEK_BASE_URL="https://api.deepseek.com"
```

#### 仍然提供启动脚本

```bash
# 双击运行（推荐）
Run-HealthHelper.bat

# 或使用PowerShell脚本
.\Set-DeepSeek-Env.ps1
```

**关于 DeepSeek AI**：
- **高质量中文回复**：专为中文用户优化，提供专业、准确的健康建议
- **成本效益**：相对较低的API费用，适合长期使用
- **实时分析**：基于您的7天健康数据，生成个性化建议
- **离线保障**：如果AI服务不可用，自动使用本地智能分析

**API 特性**：
- 模型：`deepseek-chat`
- 回复语言：中文
- 分析维度：睡眠、饮水、运动、久坐时间
- 生成内容：健康评估 + 个性化建议

#### 注意事项
- **安全性**：API 密钥不会存储在代码中，仅在运行时读取
- **离线模式**：如果未配置 AI API，系统会自动使用本地智能分析
- **费用**：使用 OpenAI API 会产生费用，请注意账户余额

### 4. 使用步骤
1. **录入健康数据**：
   - 填写睡眠时间和质量评分
   - 输入饮水量（系统会自动判断是否达标）
   - 记录运动时长和久坐时长

2. **生成建议**：
   - 确保所有数据都已录入
   - 点击"生成健康建议"按钮
   - 系统会分析最近7天的数据并提供个性化建议

### 5. 故障排除

#### AI功能不工作
**问题**：AI建议显示本地分析结果而不是AI生成的内容
**原因**：网络连接问题或API服务暂时不可用
**解决方法**：
1. 检查网络连接
2. 等待几分钟后重试
3. 如果问题持续，可以查看控制台错误信息

**注意**：应用程序会自动在AI服务不可用时切换到本地智能分析，确保功能始终可用。

#### 环境变量不生效
**问题**：每次重启命令行都要重新设置
**解决方法**：
1. 使用提供的启动脚本
2. 或在系统环境变量中永久设置（Windows设置 → 系统 → 关于 → 高级系统设置 → 环境变量）

#### 网络连接问题
**问题**：API调用失败
**解决方法**：
1. 检查网络连接
2. 确认API密钥有效
3. 查看控制台错误信息

## 功能特性

### 🎯 智能分析
- 多维度健康数据分析
- 趋势识别和异常检测
- 个性化建议生成

### 📱 用户体验
- 直观友好的界面设计
- 实时数据验证和反馈
- 无缝的页面导航

### 🔒 数据安全
- 本地数据存储，不上传到云端
- SQLite 数据库加密支持
- 数据自动清理和隐私保护

## 开发指南

### 添加新的健康指标
1. 在 `HealthRecords.cs` 中定义新的记录类型
2. 在 `HealthInsightsService.cs` 中添加数据库字段
3. 更新 `InputViewModel.cs` 中的数据处理逻辑
4. 修改界面以支持新的数据输入

### 自定义AI提示词
如需调整AI分析的侧重点，可以修改 `LargeLanguageModelClient.cs` 中的 `BuildHealthPrompt` 方法来自定义提示词模板。

## 许可证

本项目采用 MIT 许可证。
