@echo off
REM HealthHelper 启动脚本 - 自动设置DeepSeek API环境变量

echo ========================================
echo    HealthHelper - 智能健康助手
echo ========================================
echo.
echo 🔧 设置 DeepSeek API 环境变量...
echo.

REM 设置环境变量
set DEEPSEEK_API_KEY=sk-edb4ae50b8044f099e56ce138d88579c
set DEEPSEEK_BASE_URL=https://api.deepseek.com

REM 验证环境变量是否设置
if "%DEEPSEEK_API_KEY%"=="" (
    echo ❌ 错误：DEEPSEEK_API_KEY 未设置
    pause
    exit /b 1
)

if "%DEEPSEEK_BASE_URL%"=="" (
    echo ❌ 错误：DEEPSEEK_BASE_URL 未设置
    pause
    exit /b 1
)

echo ✅ 环境变量设置完成：
echo    DEEPSEEK_API_KEY: %DEEPSEEK_API_KEY%
echo    DEEPSEEK_BASE_URL: %DEEPSEEK_BASE_URL%
echo.

echo 🚀 启动应用程序...
echo    应用程序将在当前窗口运行
echo.

REM 切换到HealthHelper目录
cd HealthHelper

REM 运行应用程序（在当前窗口）
dotnet run

echo.
echo 👋 应用程序已停止
echo    按任意键退出...
echo.
pause >nul
