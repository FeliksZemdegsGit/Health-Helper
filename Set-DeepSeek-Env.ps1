# HealthHelper DeepSeek API 环境变量设置脚本
# 此脚本会设置必要的环境变量并启动应用程序

Write-Host "🔧 设置 HealthHelper DeepSeek API 环境变量..." -ForegroundColor Green

# 设置DeepSeek API环境变量
$env:DEEPSEEK_API_KEY = "sk-edb4ae50b8044f099e56ce138d88579c"
$env:DEEPSEEK_BASE_URL = "https://api.deepseek.com"

Write-Host "✅ 环境变量设置完成：" -ForegroundColor Green
Write-Host "   DEEPSEEK_API_KEY: $($env:DEEPSEEK_API_KEY)" -ForegroundColor Yellow
Write-Host "   DEEPSEEK_BASE_URL: $($env:DEEPSEEK_BASE_URL)" -ForegroundColor Yellow

# 验证环境变量是否正确设置
if ([string]::IsNullOrWhiteSpace($env:DEEPSEEK_API_KEY)) {
    Write-Host "❌ 错误：DEEPSEEK_API_KEY 未设置" -ForegroundColor Red
    exit 1
}

if ([string]::IsNullOrWhiteSpace($env:DEEPSEEK_BASE_URL)) {
    Write-Host "❌ 错误：DEEPSEEK_BASE_URL 未设置" -ForegroundColor Red
    exit 1
}

Write-Host "`n🚀 启动 HealthHelper 应用程序..." -ForegroundColor Green
Write-Host "   应用程序将在当前窗口运行" -ForegroundColor Cyan
Write-Host "   按 Ctrl+C 停止应用程序" -ForegroundColor Cyan

# 切换到HealthHelper目录并运行应用程序
Set-Location ".\HealthHelper"

try {
    dotnet run
}
catch {
    Write-Host "`n❌ 应用程序启动失败: $($_.Exception.Message)" -ForegroundColor Red
}
finally {
    Write-Host "`n👋 应用程序已停止。如需重新运行，请再次执行此脚本。" -ForegroundColor Blue
}
