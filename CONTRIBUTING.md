# Contributing Guidelines

感谢你考虑为 GamePulseMonitor 贡献代码！🎉

## 开发环境要求

- Windows 10/11
- Visual Studio 2022（或更高版本）
- .NET 8 SDK
- PowerShell 7+

## 如何贡献

### 1. 报告 Bug

请使用 [Bug Report 模板](.github/ISSUE_TEMPLATE/bug_report.md) 提交 Issue，包含：
- 操作系统版本
- .NET 版本
- 复现步骤
- 预期行为 vs 实际行为
- 日志文件

### 2. 提出新功能

使用 [Feature Request 模板](.github/ISSUE_TEMPLATE/feature_request.md) 提交 Issue。

### 3. 提交代码

1. Fork 本仓库
2. 创建你的特性分支：`git checkout -b feat/your-feature`
3. 提交你的更改：`git commit -m "feat: add some feature"`
4. 推送到分支：`git push origin feat/your-feature`
5. 提交 Pull Request

### 提交规范

使用 [Conventional Commits](https://www.conventionalcommits.org/) 格式：

- `feat:` 新功能
- `fix:` Bug 修复
- `docs:` 文档
- `style:` 代码风格
- `refactor:` 重构
- `perf:` 性能优化
- `test:` 测试
- `chore:` 构建/工具

### 代码风格

- 遵循 .editorconfig（如果存在）
- 使用 C# 12 语法
- 保持 WPF XAML 整洁
- 所有 public API 添加 XML 文档注释

## 行为准则

请阅读并遵守 [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)。
