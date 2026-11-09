# ProAudioRecorder 专业录音程序

> 基于 .NET 10 + Windows Forms + NAudio 的桌面音频录制与播放工具，支持实时波形、音量监控、多种播放模式和录音文件管理。

---

## 目录

- [项目简介](#项目简介)
- [功能特性](#功能特性)
- [技术栈](#技术栈)
- [项目结构](#项目结构)
- [架构说明](#架构说明)
- [界面说明](#界面说明)
- [运行环境](#运行环境)
- [构建与运行](#构建与运行)
- [使用说明](#使用说明)
- [录音文件说明](#录音文件说明)
- [关键实现细节](#关键实现细节)
- [已知限制](#已知限制)
- [更新记录](#更新记录)
- [贡献与许可证](#贡献与许可证)

---

## 项目简介

ProAudioRecorder 是一个 Windows 桌面音频工具，主界面集成了录音、播放、波形显示、音量表和录音文件列表。程序启动后会自动扫描程序目录下的 `Recordings` 文件夹并加载历史录音，用户可以在同一个界面中完成录音、回放、删除和打开文件所在目录等操作。

应用启动入口为 `Program.cs`，主窗体为 `Form1`，音频能力由 `AudioUtils` 目录中的录制器、播放器、设备管理器和文件管理器提供。

---

## 功能特性

### 录音

- 自动枚举系统录音设备，可在下拉框中选择输入设备
- 实时绘制录音波形（绿色示波器风格）
- 实时音量电平表，带平滑升降动画
- 支持开始、暂停、继续、停止录音
- 实时显示录音时长（`hh:mm:ss`）
- 录音文件自动按时间命名并保存为 WAV
- 录音与播放互斥：开始录音会自动停止播放，开始播放会自动停止录音

### 播放

- 播放 `Recordings` 目录中的录音文件
- 播放进度条拖拽跳转、播放时间显示
- 播放音量调节与静音
- 播放过程中的实时波形与音量显示
- 暂停 / 继续 / 停止播放
- 三种播放模式：
  - **播完停止**：当前文件播放结束后停止
  - **单曲循环**：当前文件循环播放
  - **顺序播放**：自动播放列表中的下一个文件

### 文件管理

- 启动时自动加载已有录音，列表显示为 `文件名 [mm:ss]`
- 列表按文件名降序排列，最新录音排在最前
- 双击列表项直接播放
- 支持按钮、`Delete` 键、右键菜单三种删除方式，删除前二次确认
- 右键菜单支持「播放录音」「删除录音」「从文件夹打开」
- 删除后自动重新加载列表并选中相邻文件

---

## 技术栈

| 类别 | 说明 |
| --- | --- |
| 语言 | C#（启用可空引用类型、隐式 using） |
| 运行时 | .NET 10（`net10.0-windows`） |
| UI 框架 | Windows Forms |
| 音频库 | NAudio 2.3.0 |
| 相关包 | NAudio.Wasapi 2.3.0、NAudio.Vorbis 1.5.0 |
| 解决方案 | `ProAudioRecorder.slnx`（新版 XML 解决方案格式） |

> 说明：项目虽然引入了 `NAudio.Wasapi` 与 `NAudio.Vorbis`，但当前核心代码使用 `WaveInEvent` / `WaveOutEvent`（MME/WinMM 封装）完成录音与播放，文件列表也只筛选 `.wav` 文件。

---

## 项目结构

```text
ProAudioRecorder/
├── AudioUtils/                     # 音频核心逻辑
│   ├── AudioDeviceManager.cs       # 枚举录音/播放设备
│   ├── AudioRecorder.cs            # 录音、暂停、继续、停止
│   ├── AudioPlayer.cs              # 音频加载、播放、暂停、跳转、静音
│   ├── PlaybackSampleProvider.cs   # 播放采样拦截，生成音量与波形事件
│   └── FileManager.cs              # 录音文件枚举、时长读取、删除、缓存
├── Services/
│   └── FileService.cs              # 文件管理门面，提供路径与文件名处理
├── Form1.cs                        # 主窗体业务逻辑与事件处理
├── Form1.Designer.cs               # 主窗体控件布局
├── Form1.resx                      # 主窗体资源
├── WaveFormControl.cs              # 自定义实时波形控件
├── CustomProgressBar.cs            # 可自定义颜色的进度条控件
├── Program.cs                      # 程序入口
├── ProAudioRecorder.csproj         # 项目与依赖配置
├── ProAudioRecorder.slnx           # 解决方案文件
└── README.md                       # 项目说明文档
```

运行时还会生成：

```text
<程序目录>/Recordings/              # 录音输出目录，首次使用时自动创建
<程序目录>/bin/、obj/               # 编译输出与中间文件
```

---

## 架构说明

### 整体分层

```text
┌──────────────────────────────────────────────┐
│                   Form1（UI）                 │
│  设备选择 / 录音按钮 / 播放按钮 / 文件列表     │
│  进度条 / 音量条 / 波形控件 / 状态标签         │
└───────────────┬──────────────────────────────┘
                │ 事件订阅 + BeginInvoke 回 UI 线程
┌───────────────▼──────────────────────────────┐
│              AudioUtils（音频层）             │
│  AudioRecorder   AudioPlayer   FileManager   │
│  AudioDeviceManager   PlaybackSampleProvider │
└───────────────┬──────────────────────────────┘
                │
┌───────────────▼──────────────────────────────┐
│                 NAudio / WinMM                │
│  WaveInEvent / WaveFileWriter / AudioFileReader│
│  WaveOutEvent / ISampleProvider               │
└──────────────────────────────────────────────┘
```

### 核心类职责

| 类 | 职责 |
| --- | --- |
| `AudioDeviceManager` | 通过 `WaveIn.DeviceCount`、`WaveOut.DeviceCount` 枚举录音与播放设备，返回 `AudioDeviceInfo` |
| `AudioRecorder` | 使用 `WaveInEvent` + `WaveFileWriter` 录音；计算音量峰值、降采样生成波形；维护录音时长与暂停累计时长 |
| `AudioPlayer` | 使用 `AudioFileReader` 加载文件、`WaveOutEvent` 输出声音；提供播放、暂停、恢复、停止、跳转、静音、音量控制 |
| `PlaybackSampleProvider` | 实现 `ISampleProvider`，拦截播放采样，计算峰值音量并周期性产生波形事件 |
| `FileManager` | 扫描 `*.wav`、读取时长并缓存、删除文件 |
| `FileService` | 封装 `FileManager`，并提供「显示文本 ↔ 文件名 ↔ 完整路径」转换 |
| `WaveFormControl` | 基于 `Queue<float>` 缓存波形点，双缓冲绘制网格与波形 |
| `CustomProgressBar` | 重写 `OnPaint`，支持自定义进度颜色和边框颜色 |

### 事件驱动流程

**录音流程**

```text
用户点击开始录音
  → AudioRecorder.StartRecording(deviceId, recordingsFolder)
  → WaveInEvent.DataAvailable 回调
      ├─ 写入 WAV 文件
      ├─ 计算峰值音量 → VolumeChanged
      └─ 降采样波形数据 → WaveformDataChanged
  → Form1 通过 BeginInvoke 回到 UI 线程
      ├─ 更新音量电平表（50ms 平滑动画）
      └─ 向 WaveFormControl 追加波形点
```

**播放流程**

```text
用户点击播放
  → AudioPlayer.LoadFile(path)（AudioFileReader）
  → AudioPlayer.Play()（WaveOutEvent + PlaybackSampleProvider）
  → PlaybackSampleProvider.Read 回调
      ├─ 计算峰值音量 → VolumeChanged
      └─ 每约 1102 个采样产生一次波形点 → WaveformDataChanged
  → Form1 通过 BeginInvoke 回到 UI 线程
      ├─ 更新音量表与波形
      └─ 100ms 定时器轮询播放位置并刷新进度条
```

### 线程与定时器

- 录音、播放的数据回调都运行在音频线程，`Form1` 使用 `BeginInvoke` 将 UI 更新切回主线程。
- `uiTimer`：每 **50 ms** 刷新音量电平表，使用线性插值实现平滑过渡（上升系数 1.0，下降系数 0.8）。
- `progressTimer`：每 **100 ms** 调用 `AudioPlayer.UpdatePosition()` 刷新播放进度。
- `recordingTimer`：每 **100 ms** 刷新录音时长标签。
- 用户拖动进度条时暂停自动刷新，松开鼠标后再执行 `Seek`，避免拖动与播放位置互相抢占。

---

## 界面说明

主窗体标题为「专业录音程序」，默认客户区大小约 `671 × 405`。

| 控件 | 名称 | 作用 |
| --- | --- | --- |
| 设备下拉框 | `cmbDevices` | 选择录音输入设备 |
| 开始/暂停录音 | `btnStartPause` | 开始录音；录音中变为「暂停录音」，暂停后变为「继续录音」 |
| 停止录音 | `btnStopRec` | 结束录音并保存文件 |
| 暂停录音 | `btnPauseRec` | 暂停当前录音 |
| 波形显示 | `waveFormControl1` | 实时显示录音/播放波形 |
| 音量电平表 | `progLevel` | 自定义颜色的实时音量指示 |
| 播放进度条 | `trackProgress` | 显示并拖动播放位置 |
| 播放时间 | `lblTime` | `当前时间 / 总时长` |
| 音量滑块 | `trackVolume` | 播放音量 0–100 |
| 静音复选框 | `chkMute` | 播放静音开关 |
| 播放模式下拉框 | `cmbPlaybackMode` | 运行时动态创建，位于播放按钮右侧 |
| 播放/暂停/停止 | `btnPlay` / `btnPausePlay` / `btnStopPlay` | 播放控制 |
| 录音文件列表 | `lstFiles` | 展示录音文件及右键菜单 |
| 删除按钮 | `btnDelete` | 删除选中的录音 |
| 状态/时长标签 | `lblStatus` / `lblDuration` | 显示当前状态与录音时长 |

> 播放模式下拉框不在 `Form1.Designer.cs` 中，而是在 `InitializePlaybackMode()` 里以代码方式创建并添加到窗体。

---

## 运行环境

- **操作系统**：Windows 10 / Windows 11（需要可用的音频输入设备才能录音）
- **运行时**：.NET 10 Desktop Runtime（开发时需要 .NET 10 SDK）
- **开发工具**：
  - 支持 .NET 10 与 `.slnx` 解决方案的 Visual Studio，或
  - 直接使用 `dotnet` CLI
- **NuGet 包**：首次构建时自动还原 `NAudio`、`NAudio.Wasapi`、`NAudio.Vorbis`

---

## 构建与运行

### 使用命令行

```powershell
# 进入项目目录
cd ProAudioRecorder

# 还原依赖
dotnet restore

# 编译
dotnet build

# 运行
dotnet run
```

### 使用 Visual Studio

1. 打开 `ProAudioRecorder.slnx`
2. 等待 NuGet 包还原完成
3. 选择 `Debug` 配置与 `Any CPU` 平台
4. 按 `F5` 启动调试，或 `Ctrl+F5` 直接运行

### 发布（可选）

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

---

## 使用说明

### 录音

1. 在顶部下拉框中选择录音设备。
2. 点击「开始录音」，状态栏显示「正在录音...」，波形与音量表开始刷新。
3. 点击「暂停录音」可暂停；点击「继续录音」恢复。
4. 点击「停止录音」结束并保存，文件列表自动刷新。
5. 录音时长显示在「开始录音」按钮右侧。

### 播放

1. 在右侧文件列表中选择一个录音（或直接双击）。
2. 点击「播放音频」开始播放。
3. 拖动进度条可跳转；拖动结束后会精确定位到目标位置。
4. 使用「暂停播放 / 继续播放」「停止播放」控制播放状态。
5. 通过音量滑块调节音量；勾选「静音」可临时静音，调节音量会自动取消静音。
6. 在播放模式下拉框中选择「播完停止」「单曲循环」或「顺序播放」。

### 文件管理

- **双击**列表项：立即播放该录音。
- **Delete 键**：删除当前选中录音（有确认提示）。
- **删除选中音频**按钮：删除当前选中录音。
- **右键菜单**：
  - 播放录音
  - 删除录音
  - 从文件夹打开（在资源管理器中定位到该文件）

---

## 录音文件说明

- **保存位置**：`Application.StartupPath\Recordings`（通常是 `bin\Debug\net10.0-windows\Recordings` 或发布目录下的 `Recordings`）。
- **文件命名**：`yyyy-MM-dd-HHmmss.wav`，例如 `2026-11-09-143025.wav`。
- **音频格式**：WAV / PCM，采样率 **44100 Hz**，位深 **16 bit**，声道 **单声道**。
- **列表排序**：按文件路径降序，最新文件在最上方。
- **显示格式**：`文件名 [mm:ss]`，例如 `2026-11-09-143025.wav [01:23]`。
- **时长缓存**：`FileManager` 会缓存已读取的文件时长，删除文件时同步移除缓存。

---

## 关键实现细节

1. **录音数据双用途**：`WaveInEvent.DataAvailable` 中同时完成文件写入、音量峰值计算和波形降采样，避免重复遍历带来额外开销。
2. **波形降采样**：录音时每个数据块最多抽取约 100 个采样点；播放时每约 1102 个采样产生一个波形点，从而在保证视觉效果的同时控制 UI 刷新压力。
3. **波形控件双缓冲**：`WaveFormControl` 开启 `DoubleBuffer`、`UserPaint`、`AllPaintingInWmPaint`，减少闪烁。
4. **UI 平滑音量**：录音线程只写入目标值，UI 定时器做插值，避免电平表跳变。
5. **波形点上限**：录音时默认 1500 点；播放开始时 150 点、停止后 3000 点，用于适配不同显示节奏。
6. **播放模式**：由 `PlaybackMode` 枚举控制，播放结束事件中根据当前模式决定停止、重播或播放下一首。
7. **文件路径处理**：列表项包含显示用时长，`FileService.ExtractFileNameFromDisplayText` 负责从 `文件名 [时长]` 中还原真实文件名。

---

## 已知限制

- **继续录音使用默认设备**：`AudioRecorder.ResumeRecording()` 重新创建 `WaveInEvent` 时固定使用 `DeviceNumber = 0`，若开始时选择的不是 0 号设备，继续录音后可能切换到默认设备。
- **顺序播放/循环播放有停顿**：切换文件时在 UI 线程调用 `Thread.Sleep(100)`，可能造成短暂卡顿。
- **仅管理 WAV 文件**：文件列表只扫描 `*.wav`；虽然 `AudioFileReader` 本身可读取更多格式，但界面未开放。
- **文件时长同步读取**：加载列表时逐个文件读取时长，录音文件很多时首次加载可能有短暂延迟（已有缓存缓解）。
- **无自动化测试**：项目目前以手工验证为主，未包含单元测试或集成测试。
- **未打包安装程序**：需要自行编译运行，或使用 `dotnet publish` 发布。

---

## 更新记录

- **当前版本**
  - 引入播放模式（播完停止 / 单曲循环 / 顺序播放）
  - 增加右键菜单、Delete 键删除、打开文件所在目录
  - 增加音量平滑、暂停时长累计、波形降采样与双缓冲
- **历史提交**
  - `更新README.md文件，添加项目详细说明`
  - `完善代码注释和波形控件功能`
  - `Backup current version before optimization`
  - `添加项目文件`
  - `添加 .gitattributes、.gitignore 和 README.md`

---

## 贡献与许可证

欢迎通过 Issue 和 Pull Request 提交问题与改进建议。

本项目采用 **MIT 许可证**。
