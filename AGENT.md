# 项目 Agent 说明

## 项目定位

本项目是 `DeafAlsoPlayFps`，用于帮助听障或弱听玩家在 FPS 游戏中通过视觉方式感知声音方向。

当前产品重点不是完整还原 3D 音源，而是提供实战可用的“声音转向校准器”：

- 左上角红绿条是核心功能，用于快速判断应向左或向右转。
- 八方向圆环仅作为辅助提示，前后和上下方向不应作为高置信定位依据。
- 功能必须保持非侵入式：不读游戏内存、不注入进程、不修改游戏数据、不影响耳机正常输出。

## 背景知识

现有方案通过 WASAPI loopback 捕获系统最终输出音频。该音频通常已经经过游戏引擎、Windows 声音链路、声卡、HRTF 或耳机虚拟化混音处理，最终只剩左右双声道。

因此可稳定利用的信息是：

- 左右声道 RMS 能量差。
- 左右差值的 dB 表达。
- 声音强弱、出现和消失的时间变化。

不稳定或弱推断的信息是：

- 前后方向。
- 上下方向。
- 左前、右前、左后、右后的准确象限。

技术判断：

- 左右声道差是最可靠信号。
- `20 * log10(right / left)` 比线性差值更符合人耳响度差感知。
- 短促脚步声需要视觉保持，否则用户来不及识别。
- 前后方向在双声道混音里缺少稳定独立特征，只能作为弱提示。

## 当前默认参数

实战默认值来自用户游戏测试反馈：

```text
Volume Gain: 2.0
Channel Separation: 2.0
Response Sensitivity: 2.0
Side Threshold: 10dB
Direction Ring: enabled
```

这些默认值应保持，除非有新的实测反馈证明误触发明显增加。

## 用户偏好

- 使用简体中文沟通。
- 回答要直接、清晰、偏终端风格。
- 可以使用少量 emoji 强化标题和分隔。
- 用户重视实测体验，尤其是 FPS 游戏中的快速识别。
- 用户明确偏好左上角红绿条，认为其对转向最有帮助。
- 最小改动优先，避免无关重构。
- 不要在本机编译项目。
- 编译和发布通过 GitHub Actions workflow 完成。
- 构建产物应发布到 GitHub Release，分支构建发布为 Pre-release，方便用户下载测试。

## 开发合作模式

### 分支

当前主要开发分支：

```text
codex/improve-direction-visuals
```

除非用户另有要求，继续在该分支推进方向条和音频可视化相关优化。

### GitHub CLI

本机可使用 `gh` 与 GitHub 交互。常用命令：

```bash
gh auth status
gh run list --workflow "Release Build" --branch codex/improve-direction-visuals --limit 5
gh run watch <run-id> --exit-status
gh release list --limit 10
gh release view <tag> --json url,assets,isPrerelease,tagName,name,publishedAt
```

如果 `git push origin ...` 因 HTTPS 凭据失败，可使用 SSH 远端地址推送：

```bash
git push git@github.com:hi-unc1e/DeafCanAlsoPlayFPS.git codex/improve-direction-visuals
```

### Workflow

Release workflow 文件：

```text
.github/workflows/release-build.yml
```

行为：

- push 到 `codex/improve-direction-visuals`：远程构建并创建 Pre-release。
- push `1.*` tag：远程构建并创建正式 Release。
- `workflow_dispatch`：可手动触发构建。

重要约束：

- 不在本机运行 MSBuild 或本机编译。
- 可以运行文本检查，例如 `git diff --check`。
- 构建验证以 GitHub Actions 结果为准。
- 如果 workflow 失败，查看日志后修复并再次 push。

### 发布

测试版下载流程：

1. push 分支触发 workflow。
2. 使用 `gh run watch` 等待构建结束。
3. 使用 `gh release list` 找到最新 Pre-release。
4. 使用 `gh release view <tag>` 获取 `DeafAlsoPlayFps.zip` 下载链接。

正式版本流程：

1. 确认分支构建成功。
2. 更新版本号，例如 `DeafAlsoPlayFps.csproj` 中的 `<Version>`。
3. 创建并推送 `1.*` tag。
4. 等待 workflow 创建正式 Release。

## 关键文件

```text
DeafAlsoPlayFps/Services/AudioCaptureService.cs
```

WASAPI loopback 捕获系统输出音频。当前应使用 RMS 能量而不是峰值作为左右通道强度基础。

```text
DeafAlsoPlayFps/ViewModel/AudioVisualizerViewModel.cs
```

处理增益、声道分离、灵敏度，并把处理后的左右音量推给 UI。

```text
DeafAlsoPlayFps/ViewModel/ChannelDifferenceViewModel.cs
```

顶部红绿转向条和八方向辅助提示的核心逻辑。

当前重点逻辑：

- 使用 dB 差值驱动红绿条长度。
- 左右接近平衡时显示对准区。
- 声音触发后保持约 `150ms`。
- 声音消失后平滑衰减约 `250ms-400ms`。

```text
DeafAlsoPlayFps/Views/ChannelDifferenceWindow.xaml
```

顶部提示窗口 UI。红绿条是核心，圆环是辅助。

```text
DeafAlsoPlayFps/Views/LayoutAdjustWindow.xaml
```

布局调整预览窗口。修改顶部提示 UI 时要同步更新预览。

```text
DeafAlsoPlayFps/MainWindow.xaml
DeafAlsoPlayFps/MainWindow.xaml.cs
DeafAlsoPlayFps/ViewModel/MainViewModel.cs
DeafAlsoPlayFps/Config.cs
```

主界面配置、默认值、重置逻辑和设置持久化。

## 代码原则

- 保持改动最小，优先沿用现有 WPF/MVVM 写法。
- 不引入新依赖，除非用户明确同意。
- 不做侵入式游戏数据读取。
- UI 优先服务 FPS 实战识别，避免遮挡准心和中部视野。
- 红绿条比圆环优先级更高。
- 阈值和 magic number 必须能从声学或实测反馈解释。
- 保留旧用户本地配置，不强制覆盖 `settings_data.json`。
- 提交前至少运行：

```bash
git diff --check
git status --short
```

## 已知产品结论

- 用户测试确认功能基本可用。
- HRTF 或空间音效开关不一定能改善本应用捕获到的左右差，因为应用拿到的是最终双声道混音。
- 低音均衡、空间虚拟化、耳机增强可能改变左右差异和瞬态，需要用户实测。
- 非侵入式获取“游戏原始对象音频”通常不可行；更清晰的原始多声道或对象音频通常在游戏引擎内部，外部软件无法稳定合法读取。
- 当前最有竞争力的方向是把红绿条做成低延迟、低遮挡、高辨识度的转向反馈，而不是追求不可靠的完整 3D 雷达。

## 当前发布记录

- `1.0.0`：方向圆环、配置、版权声明。
- `1.0.1`：修复左右方向能量检测，改用 RMS，降低可听阈值。
- `1.0.x`：当前已发布稳定线。
- `1.1.0`：当前开发目标版本，包含红绿转向条 2.0、默认实战参数、dB 驱动、对准区、保持和衰减。
- `beta-9`：`1.1.0` 开发线的 Pre-release 测试包。

## 版权说明

项目已加入版权声明：

- 原版：RightFS
- 优化：unc1e

相关文件：

```text
NOTICE.md
README.md
```
