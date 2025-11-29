# HealthHelper - 智能健康助手

## 项目简介

HealthHelper 是一个基于 Avalonia UI 和 .NET 的智能健康助手桌面应用，采用 MVVM 架构设计，使用 DeepSeek AI 技术帮助用户记录和分析每日健康数据，提供专业级别的个性化健康建议。

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

### 📚 健康提示库
- 内置丰富的健康提示内容
- 支持收藏和分类筛选
- 个性化推荐系统

### 📋 历史记录管理
- 查看历史健康数据
- 详细记录查看和分析
- 数据趋势可视化

### 🧭 智能导航
- 直观的页面导航系统
- 支持前进后退操作
- 无缝的用户体验

## 技术架构

### 核心技术栈
- **UI框架**：Avalonia UI 11.3.4 (跨平台桌面应用)
- **运行环境**：.NET 8.0
- **编程语言**：C# 12.0
- **架构模式**：MVVM (Model-View-ViewModel)
- **数据库**：SQLite 8.0.10
- **依赖注入**：Microsoft.Extensions.DependencyInjection 8.0.0
- **MVVM工具包**：CommunityToolkit.Mvvm 8.2.1
- **响应式编程**：ReactiveUI 22.2.1

### 项目结构
```
HealthHelper/
├── App.axaml & App.axaml.cs    # 应用程序配置和启动
├── Program.cs                  # 程序入口点
├── ViewLocator.cs              # 视图定位器
├── Models/                     # 数据模型层
│   ├── HealthRecords.cs        # 健康数据记录模型
│   └── AdviceModels.cs         # 建议和提示模型
├── Services/                   # 服务层
│   ├── Clients/                # 外部服务客户端
│   │   └── LargeLanguageModelClient.cs    # DeepSeek AI 客户端
│   ├── Contracts/              # 服务接口定义
│   │   ├── IHealthInsightsService.cs      # 健康洞察服务接口
│   │   ├── IRecommendationClient.cs       # 推荐客户端接口
│   │   └── ISystemClock.cs                # 系统时钟接口
│   └── Implementations/        # 服务实现
│       ├── HealthInsightsService.cs       # 健康洞察服务实现
│       └── SystemClock.cs                 # 系统时钟实现
├── ViewModels/                 # 视图模型层
│   ├── ViewModelBase.cs        # 基础视图模型
│   ├── MainWindowViewModel.cs  # 主窗口视图模型
│   ├── WelcomeViewModel.cs     # 欢迎页面视图模型
│   ├── InputViewModel.cs       # 数据录入视图模型
│   ├── AdviceViewModel.cs      # 建议展示视图模型
│   ├── HistoryViewModel.cs     # 历史记录视图模型
│   ├── HistoryDetailViewModel.cs # 历史详情视图模型
│   ├── HealthTipsViewModel.cs  # 健康提示视图模型
│   └── TipsViewModel.cs        # 提示列表视图模型
├── Views/                      # 视图层
│   ├── MainWindow.axaml        # 主窗口
│   ├── WelcomeView.axaml       # 欢迎页面
│   ├── InputView.axaml         # 数据录入界面
│   ├── AdviceView.axaml        # 建议展示界面
│   ├── HistoryView.axaml       # 历史记录界面
│   ├── HistoryDetailView.axaml # 历史详情界面
│   ├── HealthTipsView.axaml    # 健康提示界面
│   └── TipsView.axaml          # 提示列表界面
├── Navigation/                 # 导航系统
│   ├── INavigationService.cs   # 导航服务接口
│   └── NavigationService.cs    # 导航服务实现
├── Configuration/              # 配置管理
│   └── HealthInsightsOptions.cs # 健康洞察配置选项
└── Assets/                     # 资源文件
    └── avalonia-logo.ico       # 应用程序图标
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

### 4. 应用使用流程

#### 欢迎页面
启动应用后进入欢迎页面，提供以下选项：
- **开始录入**：进入健康数据录入界面
- **查看历史**：浏览过往的健康记录
- **健康提示**：查看健康知识库和收藏的提示

#### 数据录入流程
1. **录入健康数据**：
   - **睡眠记录**：填写入睡时间（如 23:15）、起床时间（如 07:00）和睡眠质量评分（1-10分）
   - **饮水记录**：输入实际饮水量（ml），系统自动判断是否达到2000ml目标
   - **运动记录**：记录运动时长（分钟）和久坐时长（分钟）

2. **生成AI建议**：
   - 确保所有数据都已录入
   - 点击"生成健康建议"按钮
   - 系统会调用DeepSeek AI分析最近7天数据并提供个性化建议
   - 如果AI服务不可用，自动使用本地智能分析

#### 历史记录查看
- 在历史页面查看所有保存的健康记录
- 点击具体日期查看详细的健康数据和AI建议
- 支持数据趋势分析

#### 健康提示库
- 浏览内置的健康提示内容
- 支持按类别筛选（全部/收藏）
- 可以收藏有用的健康提示

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
- **多维度健康数据分析**：睡眠、饮水、运动等多方面数据综合分析
- **AI驱动的个性化建议**：集成DeepSeek AI提供专业级健康建议
- **本地智能分析**：网络不可用时自动切换到本地分析算法
- **历史趋势分析**：基于7天数据进行趋势识别和异常检测

### 📱 用户体验
- **直观的导航设计**：基于MVVM的流畅页面切换
- **实时数据验证**：输入即时反馈和错误提示
- **响应式界面**：支持不同分辨率的适配
- **状态管理**：智能的加载状态和错误处理

### 🔒 数据安全
- **本地优先存储**：所有数据存储在本地SQLite数据库
- **隐私保护**：数据不上传到云端，完全本地化
- **数据持久化**：支持数据备份和恢复
- **自动清理**：7天后自动清理过期数据

### 🏗️ 技术特性
- **跨平台支持**：基于Avalonia UI，支持Windows、macOS、Linux
- **模块化架构**：清晰的代码分层，便于维护和扩展
- **依赖注入**：松耦合的服务架构
- **异步操作**：全程异步处理，保证UI响应性
- **类型安全**：使用C#强类型特性，提供编译时安全

## 开发指南

### 项目架构说明
- **MVVM模式**：严格遵循Model-View-ViewModel架构，视图和业务逻辑分离
- **依赖注入**：使用Microsoft.Extensions.DependencyInjection进行服务注册和管理
- **导航系统**：基于接口的导航服务，支持页面间导航和返回栈管理
- **数据持久化**：SQLite数据库本地存储，支持异步操作

### 添加新的健康指标
1. **数据模型扩展**：
   - 在 `Models/HealthRecords.cs` 中定义新的记录类型
   - 更新相关的记录结构和属性

2. **数据库操作**：
   - 在 `Services/Implementations/HealthInsightsService.cs` 中添加数据库字段映射
   - 更新数据保存和查询逻辑

3. **UI界面更新**：
   - 修改 `Views/InputView.axaml` 添加新的输入控件
   - 在 `ViewModels/InputViewModel.cs` 中添加数据绑定属性和处理逻辑
   - 更新数据验证和状态管理

4. **AI分析集成**：
   - 在 `Services/Clients/LargeLanguageModelClient.cs` 中更新提示词模板
   - 修改 `Models/AdviceModels.cs` 以支持新的分析维度

### 添加新的页面
1. **创建视图模型**：
   - 继承 `ViewModelBase` 创建新的ViewModel类
   - 实现必要的属性和命令

2. **创建视图**：
   - 在 `Views/` 目录下创建对应的 `.axaml` 和 `.axaml.cs` 文件
   - 设置正确的数据上下文绑定

3. **注册导航**：
   - 在 `Program.cs` 中注册新的ViewModel到依赖注入容器
   - 更新导航逻辑以支持新页面

### 自定义AI提示词
如需调整AI分析的侧重点，可以修改 `Services/Clients/LargeLanguageModelClient.cs` 中的 `BuildHealthPrompt` 方法来自定义提示词模板。

### 数据库迁移
项目使用SQLite进行数据存储，如需修改数据库结构：
1. 更新 `Models/` 中的数据模型
2. 在 `HealthInsightsService.cs` 中添加迁移逻辑
3. 确保向后兼容性

### 样式和主题
- 使用 Avalonia 的 Fluent 主题
- 样式定义在各个视图文件的 XAML 中
- 支持深色/浅色主题切换（可扩展）

## 许可证

本项目采用 MIT 许可证。
