# Abyssal Submarine

Unity 游戏项目，使用 **Unity 2022.3.62f3**。

## 打开项目

1. 克隆仓库后，在 Unity Hub 中安装 Unity 2022.3.62f3。
2. 在 Unity Hub 中添加本仓库根目录并打开。
3. 等待 Unity 还原 Packages 依赖并完成资源导入。

## 版本管理约定

- 提交 `Assets/`、`Packages/`、`ProjectSettings/` 和仓库配置文件。
- 资源及其 `.meta` 文件必须一起提交，文件夹对应的 `.meta` 也需要保留。
- 在 Unity 编辑器内移动或重命名资源，以保留 GUID 和资源引用。
- 保持 Version Control 为 `Visible Meta Files`、Asset Serialization 为 `Force Text`。
- `Library/`、`Temp/`、`Logs/`、`UserSettings/`、构建输出和自动生成的 IDE 工程文件不提交。
- `Packages/packages-lock.json` 应提交，以记录依赖解析结果。

默认分支为 `main`。当前资源直接由 Git 管理；本仓库没有启用 Git LFS。
原 Plastic SCM 本地配置保留在磁盘上，由 `.gitignore` 排除。
