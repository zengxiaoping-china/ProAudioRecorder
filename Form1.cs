using NAudio.Wave;
using ProAudioRecorder.AudioUtils;
using ProAudioRecorder.Services;
using System;
using System.IO;
using System.Windows.Forms;

namespace ProAudioRecorder
{
    /// <summary>
    /// 主窗体类，负责处理用户交互和应用程序流程
    /// </summary>
    public partial class Form1 : Form
    {
        // --- 变量声明 ---
        /// <summary>
        /// 音频录制器
        /// </summary>
        private AudioRecorder? audioRecorder;
        /// <summary>
        /// 音频播放器
        /// </summary>
        private AudioPlayer? audioPlayer;
        /// <summary>
        /// 音频设备管理器
        /// </summary>
        private AudioDeviceManager? deviceManager;
        /// <summary>
        /// 文件服务
        /// </summary>
        private FileService? fileService;
        /// <summary>
        /// 当前文件路径
        /// </summary>
        private string currentFilePath = "";

        /// <summary>
        /// UI刷新定时器
        /// </summary>
        private System.Windows.Forms.Timer uiTimer = new System.Windows.Forms.Timer();
        /// <summary>
        /// 目标音量（录音线程写入）
        /// </summary>
        private float targetValue = 0;
        /// <summary>
        /// 当前显示的音量（UI线程读取，用于平滑过渡）
        /// </summary>
        private float currentValue = 0;

        /// <summary>
        /// 录音文件保存的文件夹
        /// </summary>
        private string _recordingsFolder = ResolveRecordingsFolder();

        /// <summary>
        /// 用于更新进度条的计时器
        /// </summary>
        private System.Windows.Forms.Timer progressTimer = new System.Windows.Forms.Timer();
        /// <summary>
        /// 用于更新录音时长的计时器
        /// </summary>
        private System.Windows.Forms.Timer recordingTimer = new System.Windows.Forms.Timer();

        /// <summary>
        /// 标记用户是否正在拖动进度条
        /// </summary>
        private bool isUserDragging = false;
        /// <summary>
        /// 标记是否正在执行删除操作
        /// </summary>
        private bool isDeleting = false;
        /// <summary>
        /// 文件列表右键菜单
        /// </summary>
        private ContextMenuStrip? fileContextMenu;
        
        /// <summary>
        /// 播放模式枚举
        /// </summary>
        private enum PlaybackMode
        {
            /// <summary>
            /// 播完停止
            /// </summary>
            StopWhenFinished,
            /// <summary>
            /// 单曲循环
            /// </summary>
            SingleLoop,
            /// <summary>
            /// 顺序播放
            /// </summary>
            Sequential
        }
        
        /// <summary>
        /// 当前播放模式
        /// </summary>
        private PlaybackMode currentPlaybackMode = PlaybackMode.StopWhenFinished;

        /// <summary>
        /// 防止播放结束事件被重入处理
        /// </summary>
        private bool isHandlingPlaybackEnd = false;
        /// <summary>
        /// 播放会话编号，用于在异步切换时判断用户是否已经停止了播放
        /// </summary>
        private int playbackSession = 0;
        /// <summary>
        /// 串行化文件列表加载，避免并发刷新
        /// </summary>
        private readonly SemaphoreSlim fileLoadLock = new SemaphoreSlim(1, 1);
        /// <summary>
        /// 是否正在启动播放（用于避免录音停止事件重载列表时切换当前文件）
        /// </summary>
        private bool isStartingPlayback = false;

        /// <summary>
        /// 构造函数
        /// </summary>
        public Form1()
        {
            InitializeComponent();

            // 初始化服务
            fileService = new FileService(_recordingsFolder);

            // 初始化组件
            InitializeComponents();

            //设置示波器最大点数
            waveFormControl1.MaxPoints = 1500;
            
            // 清空示波器
            waveFormControl1.Clear();

            // 绑定事件
            BindEvents();

            // 初始化定时器
            InitializeTimers();

            // 初始化右键菜单
            InitializeContextMenu();

            //初始化录音按键
            UpdateRecordingButtonStates();

            // 窗体显示后再异步加载录音文件，避免构造函数中跨线程访问控件
            Shown += async (_, _) => await LoadExistingRecordingsAsync();
        }

        /// <summary>
        /// 解析录音文件保存目录。
        /// 优先保留程序目录下已有的 Recordings（兼容旧版本），
        /// 新安装环境则使用当前用户的 LocalApplicationData，避免 Program Files 写入权限问题。
        /// </summary>
        private static string ResolveRecordingsFolder()
        {
            string portableFolder = Path.Combine(Application.StartupPath, "Recordings");
            if (Directory.Exists(portableFolder) && Directory.GetFiles(portableFolder, "*.wav").Length > 0)
                return portableFolder;

            string localFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ProAudioRecorder",
                "Recordings");

            Directory.CreateDirectory(localFolder);
            return localFolder;
        }

        /// <summary>
        /// 绑定事件处理方法
        /// </summary>
        private void BindEvents()
        {
            // 1. 绑定录音按钮
            btnStartPause.Click += btnStartPause_Click;
            btnStopRec.Click += btnStopRec_Click;
            btnPauseRec.Click += btnPauseRec_Click;

            // 2. 绑定播放按钮
            btnPlay.Click += btnPlay_Click;
            btnPausePlay.Click += btnPausePlay_Click;
            btnStopPlay.Click += btnStopPlay_Click;

            // 3. 绑定文件事件
            lstFiles.SelectedIndexChanged += lstFiles_SelectedIndexChanged;
            lstFiles.MouseDoubleClick += lstFiles_MouseDoubleClick;
            lstFiles.MouseDown += lstFiles_MouseDown;
            lstFiles.KeyDown += lstFiles_KeyDown;
            btnDelete.Click += btnDelete_Click;

            // 4. 绑定进度条事件
            trackProgress.MouseDown += trackProgress_MouseDown;
            trackProgress.MouseUp += trackProgress_MouseUp;
            trackProgress.Scroll += trackProgress_Scroll;

            // 5. 绑定音量事件
            trackVolume.Scroll += trackVolume_Scroll;
            chkMute.CheckedChanged += chkMute_CheckedChanged;
        }

        /// <summary>
        /// 初始化定时器
        /// </summary>
        private void InitializeTimers()
        {
            // 初始化UI定时器
            uiTimer.Interval = 50; // 50毫秒刷新一次，优化性能
            uiTimer.Tick += UiTimer_Tick;
            uiTimer.Start();

            // 初始化进度计时器
            progressTimer.Interval = 100; // 每 100毫秒 更新一次（0.1秒）
            progressTimer.Tick += ProgressTimer_Tick;

            // 初始化录音计时器
            recordingTimer.Interval = 100; // 每 100毫秒 更新一次（0.1秒）
            recordingTimer.Tick += RecordingTimer_Tick;
        }

        /// <summary>
        /// 初始化右键菜单
        /// </summary>
        private void InitializeContextMenu()
        {
            // 创建右键菜单
            fileContextMenu = new ContextMenuStrip();
            
            // 添加播放录音菜单项
            ToolStripMenuItem playMenuItem = new ToolStripMenuItem("播放录音");
            playMenuItem.Click += PlayMenuItem_Click;
            fileContextMenu.Items.Add(playMenuItem);
            
            // 添加删除录音菜单项
            ToolStripMenuItem deleteMenuItem = new ToolStripMenuItem("删除录音");
            deleteMenuItem.Click += DeleteMenuItem_Click;
            fileContextMenu.Items.Add(deleteMenuItem);
            
            // 添加分隔线
            fileContextMenu.Items.Add(new ToolStripSeparator());
            
            // 添加从文件夹打开菜单项
            ToolStripMenuItem openFolderMenuItem = new ToolStripMenuItem("从文件夹打开");
            openFolderMenuItem.Click += OpenFolderMenuItem_Click;
            fileContextMenu.Items.Add(openFolderMenuItem);
            
            // 为文件列表设置右键菜单
            lstFiles.ContextMenuStrip = fileContextMenu;
        }

        /// <summary>
        /// 初始化组件
        /// </summary>
        private void InitializeComponents()
        {
            // 初始化设备管理器
            deviceManager = new AudioDeviceManager();
            InitializeDevices();

            // 初始化音频播放器
            audioPlayer = new AudioPlayer();
            audioPlayer.PositionChanged += AudioPlayer_PositionChanged;
            audioPlayer.DurationChanged += AudioPlayer_DurationChanged;
            audioPlayer.PlaybackStarted += AudioPlayer_PlaybackStarted;
            audioPlayer.PlaybackPaused += AudioPlayer_PlaybackPaused;
            audioPlayer.PlaybackResumed += AudioPlayer_PlaybackResumed;
            audioPlayer.PlaybackStopped += AudioPlayer_PlaybackStopped;
            audioPlayer.VolumeChanged += AudioPlayer_VolumeChanged;
            audioPlayer.WaveformDataChanged += AudioPlayer_WaveformDataChanged;

            // 初始化音频 recorder
            audioRecorder = new AudioRecorder();
            audioRecorder.VolumeChanged += AudioRecorder_VolumeChanged;
            audioRecorder.WaveformDataChanged += AudioRecorder_WaveformDataChanged;
            audioRecorder.RecordingStarted += AudioRecorder_RecordingStarted;
            audioRecorder.RecordingStopped += AudioRecorder_RecordingStopped;
            audioRecorder.RecordingPaused += AudioRecorder_RecordingPaused;
            audioRecorder.RecordingResumed += AudioRecorder_RecordingResumed;
            
            // 初始化播放模式选择
            InitializePlaybackMode();
        }
        
        /// <summary>
        /// 初始化播放模式选择
        /// </summary>
        private void InitializePlaybackMode()
        {
            // 播放音频按钮位置: (12, 362), 大小: (75, 23)
            // 播放模式控件应该放在播放音频按钮的右边，水平对齐
            
            // 创建播放模式选择ComboBox
            ComboBox cmbPlaybackMode = new ComboBox();
            cmbPlaybackMode.Name = "cmbPlaybackMode";
            // X坐标: 播放按钮X(12) + 播放按钮宽度(75) + 间距(8) = 95
            // Y坐标: 播放按钮Y(362)，与播放按钮水平对齐
            cmbPlaybackMode.Location = new Point(95, 362);
            // 大小与播放音频按钮一样: (75, 23)
            cmbPlaybackMode.Size = new Size(75, 23);
            cmbPlaybackMode.DropDownStyle = ComboBoxStyle.DropDownList;
            
            // 添加播放模式选项
            cmbPlaybackMode.Items.Add("播完停止");
            cmbPlaybackMode.Items.Add("单曲循环");
            cmbPlaybackMode.Items.Add("顺序播放");
            
            // 默认选中播完停止
            cmbPlaybackMode.SelectedIndex = 0;
            
            // 绑定事件
            cmbPlaybackMode.SelectedIndexChanged += cmbPlaybackMode_SelectedIndexChanged;
            
            // 添加到表单
            this.Controls.Add(cmbPlaybackMode);
        }

        // --- 初始化设备 ---
        /// <summary>
        /// 初始化录音设备列表
        /// </summary>
        private void InitializeDevices()
        {
            // 清空列表
            cmbDevices.Items.Clear();

            // 添加所有可用的录音设备
            var devices = deviceManager?.GetRecordingDevices();
            if (devices != null)
            {
                foreach (var device in devices)
                {
                    cmbDevices.Items.Add(device);
                }

                // 默认选中第一个
                if (cmbDevices.Items.Count > 0)
                {
                    cmbDevices.SelectedIndex = 0;
                }
                else
                {
                    lblStatus.Text = "未检测到录音设备";
                    btnStartPause.Enabled = false;
                }
            }
        }

        /// <summary>
        /// 异步加载已有录音文件
        /// </summary>
        /// <param name="preferredIndex">优先选中的索引</param>
        /// <param name="preserveSelectedPath">需要保持选中的文件路径（可选）</param>
        private async Task LoadExistingRecordingsAsync(int preferredIndex = 0, string? preserveSelectedPath = null)
        {
            if (fileService == null)
                return;

            await fileLoadLock.WaitAsync();

            try
            {
                // 在后台线程读取文件时长，避免文件较多时阻塞 UI
                var files = await Task.Run(() => fileService.FileManager.GetRecordingFiles());

                if (IsDisposed || Disposing)
                    return;

                string? previousPath = preserveSelectedPath;

                // 使用对象绑定，避免从显示文本中反解析文件名
                lstFiles.DataSource = null;
                lstFiles.DisplayMember = nameof(AudioFileInfo.DisplayName);
                lstFiles.DataSource = files;

                if (files.Count > 0)
                {
                    int index = preferredIndex;
                    if (!string.IsNullOrEmpty(previousPath))
                    {
                        int preservedIndex = files.FindIndex(f => f.FilePath == previousPath);
                        if (preservedIndex >= 0)
                            index = preservedIndex;
                    }

                    index = Math.Max(0, Math.Min(index, files.Count - 1));
                    lstFiles.SelectedIndex = index;
                    currentFilePath = files[index].FilePath;

                    UpdatePlaybackButtonsForCurrentState();
                }
                else
                {
                    currentFilePath = "";
                    UpdatePlaybackButtonsForCurrentState();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("加载录音文件失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                fileLoadLock.Release();
            }
        }

        /// <summary>
        /// 根据当前播放状态更新播放按钮，避免刷新文件列表时打断正在播放的 UI 状态
        /// </summary>
        private void UpdatePlaybackButtonsForCurrentState()
        {
            bool hasFiles = lstFiles.Items.Count > 0;
            bool isPlaying = audioPlayer?.IsPlaying == true;

            btnPlay.Enabled = hasFiles && !isPlaying;
            btnPausePlay.Enabled = isPlaying;
            btnStopPlay.Enabled = isPlaying;
        }

        /// <summary>
        /// 定时器事件：负责平滑地更新界面
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void UiTimer_Tick(object? sender, EventArgs e)
        {
            // 动态调整平滑系数
            // 如果当前值小于目标值（声音变大），快速跟上 (系数 1.0)
            // 如果当前值大于目标值（声音变小），快速回落 (系数 0.8)
            float speed = (currentValue < targetValue) ? 1.0f : 0.8f;

            // 简单的线性插值
            currentValue = currentValue + (targetValue - currentValue) * speed;

            // 强制安全检查（防止越界崩溃），按进度条最大值映射满量程
            int val = (int)(currentValue * progLevel.Maximum);
            if (val < progLevel.Minimum) val = progLevel.Minimum;
            if (val > progLevel.Maximum) val = progLevel.Maximum;

            progLevel.Value = val;
        }

        // --- 录音按钮逻辑 ---
        /// <summary>
        /// 开始/暂停录音按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void btnStartPause_Click(object? sender, EventArgs e)
        {
            waveFormControl1.Clear();//清空示波器

            // 检查是否是暂停状态
            if (audioRecorder != null && audioRecorder.IsPaused)
            {
                // 继续录音
                audioRecorder.ResumeRecording();
                recordingTimer.Start();
                return;
            }

            // 检查是否正在录音
            if (audioRecorder != null && !audioRecorder.IsRecording)
            {
                if (cmbDevices.Items.Count == 0)
                {
                    MessageBox.Show("未检测到录音设备，请检查麦克风连接后重启程序。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                try
                {
                    // 检查是否正在播放，如果是则停止播放
                    if (audioPlayer != null && audioPlayer.IsPlaying)
                    {
                        StopPlayback();
                    }

                    // 获取选中的设备
                    if (cmbDevices.SelectedItem is AudioDeviceInfo deviceInfo)
                    {
                        // 启动录音
                        audioRecorder.StartRecording(deviceInfo.DeviceId, _recordingsFolder);
                        recordingTimer.Start();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("录音启动失败: " + ex.Message);
                }
            }
            // 如果正在录音（且没暂停） -> 触发暂停
            else if (audioRecorder != null && audioRecorder.IsRecording && !audioRecorder.IsPaused)
            {
                // 暂停录音
                audioRecorder.PauseRecording();
                recordingTimer.Stop();
            }
        }

        /// <summary>
        /// 定时器触发事件（每100毫秒执行一次）更新录音时长
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void RecordingTimer_Tick(object? sender, EventArgs e)
        {
            // 只有在录音中且没有暂停时才更新
            if (audioRecorder != null && audioRecorder.IsRecording && !audioRecorder.IsPaused)
            {
                // 更新UI
                lblDuration.Text = audioRecorder.CurrentDuration.ToString(@"hh\:mm\:ss");
            }
        }

        /// <summary>
        /// 停止录音按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void btnStopRec_Click(object? sender, EventArgs e)
        {
            if (audioRecorder != null && (audioRecorder.IsRecording || audioRecorder.IsPaused))
            {
                // 停止录音
                audioRecorder.StopRecording();
                recordingTimer.Stop();

                // 清空示波器
                waveFormControl1.Clear();
            }
        }

        /// <summary>
        /// 暂停录音按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void btnPauseRec_Click(object? sender, EventArgs e)
        {
            if (audioRecorder != null && audioRecorder.IsRecording && !audioRecorder.IsPaused)
            {
                try
                {
                    // 暂停录音
                    audioRecorder.PauseRecording();
                    recordingTimer.Stop();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("暂停出错: " + ex.Message);
                }
            }
        }

        // --- 播放按钮逻辑 ---

        /// <summary>
        /// 播放按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void btnPlay_Click(object? sender, EventArgs e)
        {
            StartPlayback();
        }

        /// <summary>
        /// 进度更新计时器事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void ProgressTimer_Tick(object? sender, EventArgs e)
        {
            // 1. 确保音频播放器存在且已初始化
            if (audioPlayer == null)
                return;

            // 2. 只有当用户没在拖动时，才更新进度条
            if (!isUserDragging)
            {
                try
                {
                    // 更新播放器位置
                    audioPlayer.UpdatePosition();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);// 记录日志，防止 UI 线程崩溃
                }
            }
        }

        /// <summary>
        /// 用户拖动或点击进度条时触发
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void trackProgress_Scroll(object? sender, EventArgs e)
        {
            // 如果用户正在拖动，才执行跳转
            if (isUserDragging && audioPlayer != null)
            {
                audioPlayer.Seek(TimeSpan.FromSeconds(trackProgress.Value)); // 1. 跳转音频
            }
        }

        /// <summary>
        /// 进度条鼠标按下事件（用户按下鼠标）
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void trackProgress_MouseDown(object? sender, MouseEventArgs e)
        {
            isUserDragging = true;
        }

        /// <summary>
        /// 进度条鼠标松开事件（用户松开鼠标）
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void trackProgress_MouseUp(object? sender, MouseEventArgs e)
        {
            isUserDragging = false;
            // 松开后立即跳转一次，确保精准
            if (audioPlayer != null)
            {
                audioPlayer.Seek(TimeSpan.FromSeconds(trackProgress.Value));
            }
        }

        /// <summary>
        /// 核心音频文件播放方法
        /// </summary>
        private void StartPlayback()
        {
            if (audioPlayer == null)
                return;

            // 额外保险：防止文件路径为空
            if (string.IsNullOrEmpty(currentFilePath))
                return;

            string pathToPlay = currentFilePath;

            if (!fileService?.FileExists(pathToPlay) ?? true)
            {
                MessageBox.Show("请先选中需要播放的音频文件！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                // 新的播放会话，用户后续停止时会使旧的异步切换失效
                playbackSession++;
                isStartingPlayback = true;

                // 检查是否正在录音，如果是则停止录音
                if (audioRecorder != null && (audioRecorder.IsRecording || audioRecorder.IsPaused))
                {
                    audioRecorder.StopRecording();
                    recordingTimer.Stop();
                }

                // 1. 加载文件（内部会释放上一首资源，但不会触发播放模式逻辑）
                audioPlayer.LoadFile(pathToPlay);

                // 2.设置进度条
                trackProgress.Maximum = Math.Max(0, (int)audioPlayer.TotalDuration.TotalSeconds);
                trackProgress.Value = 0;

                // 3. 开始播放
                audioPlayer.Play();

                // 4. 启动计时器
                progressTimer.Start();

                // 5. 更新UI
                btnPlay.Enabled = false;
                btnPausePlay.Enabled = true;
                btnStopPlay.Enabled = true;
                btnPausePlay.Text = "暂停播放";
                lblStatus.Text = "正在播放...";
            }
            catch (Exception ex)
            {
                MessageBox.Show("播放失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                isStartingPlayback = false;
            }
        }

        /// <summary>
        /// 停止播放
        /// </summary>
        private void StopPlayback()
        {
            // 使正在等待的自动切换逻辑失效
            playbackSession++;

            progressTimer?.Stop();

            trackProgress.Value = 0;
            lblTime.Text = "00:00 / 00:00";

            targetValue = 0;
            currentValue = 0;
            progLevel.Value = 0;
            waveFormControl1.Clear();

            if (audioPlayer != null)
            {
                // 用户主动停止，AudioPlayer 会以 UserStopped 原因通知，不会触发播放模式
                audioPlayer.Stop();
            }

            lblStatus.Text = "已停止";

            UpdatePlaybackButtonsForCurrentState();
            btnPausePlay.Text = "暂停播放";
        }

        /// <summary>
        /// 暂停/继续按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void btnPausePlay_Click(object? sender, EventArgs e)
        {
            if (audioPlayer == null)
                return;

            // 判断当前状态：如果是暂停中，则继续；否则暂停
            if (audioPlayer.IsPaused)
            {
                audioPlayer.Resume();
                progressTimer.Start();
            }
            else
            {
                audioPlayer.Pause();
                progressTimer.Stop();
            }
        }

        /// <summary>
        /// 停止按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void btnStopPlay_Click(object? sender, EventArgs e)
        {
            StopPlayback();// 直接调用之前写好的彻底清理方法
        }

        /// <summary>
        /// 文件列表选中项改变事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void lstFiles_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // 直接使用绑定的文件对象，避免从显示文本中反解析文件名
            if (lstFiles.SelectedItem is AudioFileInfo fileInfo)
            {
                currentFilePath = fileInfo.FilePath;
            }
        }

        // --- 文件删除相关方法 ---
        /// <summary>
        /// 文件列表键盘按下事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private async void lstFiles_KeyDown(object? sender, KeyEventArgs e)
        {
            // 如果按下了 Delete 键，且列表里有选中项
            if (e.KeyCode == Keys.Delete && lstFiles.SelectedItem != null)
            {
                await DeleteSelectedFileAsync();
            }
        }

        /// <summary>
        /// 删除按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private async void btnDelete_Click(object? sender, EventArgs e)
        {
            await DeleteSelectedFileAsync();
        }

        /// <summary>
        /// 删除选中的文件
        /// </summary>
        private async Task DeleteSelectedFileAsync()
        {
            // 检查是否正在执行删除操作
            if (isDeleting)
                return;

            // 标记为正在执行删除操作
            isDeleting = true;

            try
            {
                // 禁用所有相关控件，防止用户操作
                btnDelete.Enabled = false;
                lstFiles.Enabled = false;

                // 1. 检查有没有选中任何文件
                if (lstFiles.SelectedItem is not AudioFileInfo fileInfo)
                {
                    MessageBox.Show("请先在列表中选择一个要删除的文件！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                int deletedIndex = lstFiles.SelectedIndex;

                // 2. 二次确认
                if (MessageBox.Show($"确定要删除文件 \"{fileInfo.FileName}\" 吗？", "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;

                try
                {
                    // 3. 如果删除的是当前正在播放的文件，先停止播放，释放文件句柄
                    if (audioPlayer?.IsPlaying == true && string.Equals(currentFilePath, fileInfo.FilePath, StringComparison.OrdinalIgnoreCase))
                    {
                        StopPlayback();
                    }

                    fileService?.DeleteFile(fileInfo.FileName);

                    // 4. 重新加载列表，并尽量选中原位置的下一个文件
                    await LoadExistingRecordingsAsync(deletedIndex);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("删除失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                // 无论操作成功与否，都重新启用所有相关控件并重置标志
                btnDelete.Enabled = true;
                lstFiles.Enabled = true;
                isDeleting = false;
            }
        }

        /// <summary>
        /// 文件列表双击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void lstFiles_MouseDoubleClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                StartPlayback();
            }
        }

        /// <summary>
        /// 音量条滚动事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void trackVolume_Scroll(object? sender, EventArgs e)
        {
            if (audioPlayer != null)
            {
                // 如果处于静音状态，先恢复静音前的音量，再应用新的音量
                if (chkMute.Checked)
                {
                    chkMute.Checked = false;
                }

                // TrackBar 的值是 0-100，AudioFileReader.Volume 需要 0.0-1.0
                audioPlayer.Volume = trackVolume.Value / 100.0f;
            }
        }

        /// <summary>
        /// 静音复选框事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void chkMute_CheckedChanged(object? sender, EventArgs e)
        {
            if (audioPlayer != null)
            {
                audioPlayer.Mute(chkMute.Checked);

                // 可选：把音量条变灰，表示不可用
                trackVolume.Enabled = !chkMute.Checked;
            }
        }

        // --- 事件处理方法 ---

        /// <summary>
        /// 录音音量改变事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="volume">音量值</param>
        private void AudioRecorder_VolumeChanged(object? sender, float volume)
        {
            // 音频回调线程 -> UI 线程
            RunOnUiThread(() => targetValue = volume);
        }

        /// <summary>
        /// 录音波形数据改变事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="waveformData">一批波形数据</param>
        private void AudioRecorder_WaveformDataChanged(object? sender, float[] waveformData)
        {
            RunOnUiThread(() =>
            {
                if (audioRecorder is { IsRecording: true })
                    waveFormControl1.AddValues(waveformData);
            });
        }

        /// <summary>
        /// 播放音量改变事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="volume">音量值</param>
        private void AudioPlayer_VolumeChanged(object? sender, float volume)
        {
            RunOnUiThread(() =>
            {
                if (audioPlayer is { IsPlaying: true })
                    targetValue = volume;
            });
        }

        /// <summary>
        /// 播放波形数据改变事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="waveformData">一批波形数据</param>
        private void AudioPlayer_WaveformDataChanged(object? sender, float[] waveformData)
        {
            RunOnUiThread(() =>
            {
                if (audioPlayer is { IsPlaying: true })
                    waveFormControl1.AddValues(waveformData);
            });
        }

        /// <summary>
        /// 录音开始事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void AudioRecorder_RecordingStarted(object? sender, EventArgs e)
        {
            RunOnUiThread(() =>
            {
                // UI 更新
                btnStartPause.Text = "暂停录音";
                btnStopRec.Enabled = true;
                btnPauseRec.Enabled = true;
                cmbDevices.Enabled = false;
                lblStatus.Text = "正在录音...";
                waveFormControl1.MaxPoints = 1500;
                waveFormControl1.Clear();
            });
        }

        /// <summary>
        /// 录音停止事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void AudioRecorder_RecordingStopped(object? sender, EventArgs e)
        {
            RunOnUiThread(() =>
            {
                // UI 更新
                btnStartPause.Text = "开始录音";
                btnStartPause.Enabled = cmbDevices.Items.Count > 0;
                btnStopRec.Enabled = false;
                btnPauseRec.Enabled = false;
                cmbDevices.Enabled = true;
                lblStatus.Text = "录音完成";

                // 强制归零
                targetValue = 0;
                currentValue = 0;
                progLevel.Value = 0;

                // 清空波形
                waveFormControl1.Clear();

                // 重置标签
                lblDuration.Text = "00:00:00";

                // 如果在播放启动过程中停止录音，保持当前选中文件，避免列表重载切换播放目标
                string? preservePath = (audioPlayer?.IsPlaying == true || isStartingPlayback)
                    ? currentFilePath
                    : null;

                // 重新加载文件列表（有播放/启动播放时保留当前选择）
                _ = LoadExistingRecordingsAsync(0, preservePath);
            });
        }

        /// <summary>
        /// 录音暂停事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void AudioRecorder_RecordingPaused(object? sender, EventArgs e)
        {
            RunOnUiThread(() =>
            {
                // UI 更新
                btnStartPause.Text = "继续录音";
                btnStopRec.Enabled = true;
                btnPauseRec.Enabled = false;
                cmbDevices.Enabled = false;
                lblStatus.Text = "已暂停";

                // 强制归零
                targetValue = 0;
                currentValue = 0;
                progLevel.Value = 0;
            });
        }

        /// <summary>
        /// 录音恢复事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void AudioRecorder_RecordingResumed(object? sender, EventArgs e)
        {
            RunOnUiThread(() =>
            {
                // UI 更新
                btnStartPause.Text = "暂停录音";
                btnStopRec.Enabled = true;
                btnPauseRec.Enabled = true;
                cmbDevices.Enabled = false;
                lblStatus.Text = "正在录音...";
            });
        }

        /// <summary>
        /// 更新时间标签的显示格式：mm:ss / mm:ss
        /// </summary>
        /// <param name="current">当前时间</param>
        /// <param name="total">总时间</param>
        private void UpdateTimeLabel(TimeSpan current, TimeSpan total)
        {
            // 格式化为 "mm:ss"
            string currentStr = current.ToString(@"mm\:ss");
            string totalStr = total.ToString(@"mm\:ss");

            lblTime.Text = $"{currentStr} / {totalStr}";
        }

        /// <summary>
        /// 播放位置改变事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="position">当前位置</param>
        private void AudioPlayer_PositionChanged(object? sender, TimeSpan position)
        {
            // 只有当用户没在拖动时，才更新进度条
            if (!isUserDragging)
            {
                int currentVal = (int)position.TotalSeconds;
                if (currentVal <= trackProgress.Maximum)
                {
                    trackProgress.Value = currentVal;
                }

                // 更新时间标签
                UpdateTimeLabel(position, audioPlayer?.TotalDuration ?? TimeSpan.Zero);
            }
        }

        /// <summary>
        /// 播放时长改变事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="duration">总时长</param>
        private void AudioPlayer_DurationChanged(object? sender, TimeSpan duration)
        {
            // 更新进度条最大值
            trackProgress.Maximum = Math.Max(0, (int)duration.TotalSeconds);
            // 更新时间标签
            UpdateTimeLabel(TimeSpan.Zero, duration);
        }

        /// <summary>
        /// 播放开始事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void AudioPlayer_PlaybackStarted(object? sender, EventArgs e)
        {
            waveFormControl1.MaxPoints = 150;
            waveFormControl1.Clear();
            lblStatus.Text = "正在播放...";
            btnPausePlay.Text = "暂停播放";
        }

        /// <summary>
        /// 播放暂停事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void AudioPlayer_PlaybackPaused(object? sender, EventArgs e)
        {
            lblStatus.Text = "播放已暂停";
            btnPausePlay.Text = "继续播放";
        }

        /// <summary>
        /// 播放恢复事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void AudioPlayer_PlaybackResumed(object? sender, EventArgs e)
        {
            lblStatus.Text = "正在播放...";
            btnPausePlay.Text = "暂停播放";
        }

        /// <summary>
        /// 播放停止事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void AudioPlayer_PlaybackStopped(object? sender, PlaybackStoppedEventArgs e)
        {
            RunOnUiThread(() =>
            {
                // 忽略上一首延迟送达的停止事件
                if (audioPlayer == null || e.PlaybackId != audioPlayer.PlaybackId)
                    return;

                waveFormControl1.MaxPoints = 3000;
                waveFormControl1.Clear();

                if (e.Exception != null)
                {
                    MessageBox.Show("播放出错: " + e.Exception.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    StopPlayback();
                    return;
                }

                // 用户主动停止时只需要更新界面，不再触发播放模式逻辑
                if (e.Reason != PlaybackStopReason.ReachedEnd)
                    return;

                _ = HandlePlaybackEndedAsync();
            });
        }

        /// <summary>
        /// 处理自然播放结束后的播放模式切换
        /// </summary>
        private async Task HandlePlaybackEndedAsync()
        {
            if (isHandlingPlaybackEnd)
                return;

            isHandlingPlaybackEnd = true;
            int session = playbackSession;

            try
            {
                progressTimer.Stop();

                switch (currentPlaybackMode)
                {
                    case PlaybackMode.StopWhenFinished:
                        // 播完停止
                        StopPlayback();
                        break;

                    case PlaybackMode.SingleLoop:
                        // 单曲循环：异步等待，避免阻塞 UI 线程
                        await Task.Delay(100);
                        if (session != playbackSession)
                            return;
                        StartPlayback();
                        break;

                    case PlaybackMode.Sequential:
                        // 顺序播放
                        int currentIndex = lstFiles.SelectedIndex;
                        int nextIndex = currentIndex + 1;

                        if (nextIndex < lstFiles.Items.Count)
                        {
                            lstFiles.SelectedIndex = nextIndex;

                            if (lstFiles.SelectedItem is AudioFileInfo nextFile)
                                currentFilePath = nextFile.FilePath;

                            await Task.Delay(100);
                            if (session != playbackSession)
                                return;

                            StartPlayback();
                        }
                        else
                        {
                            // 已经是最后一个文件，停止播放
                            StopPlayback();
                        }
                        break;
                }
            }
            finally
            {
                isHandlingPlaybackEnd = false;
            }
        }

        /// <summary>
        /// 更新录音按钮状态
        /// </summary>
        private void UpdateRecordingButtonStates()
        {
            btnStartPause.Enabled = cmbDevices.Items.Count > 0;
            btnStopRec.Enabled = false;
            btnPauseRec.Enabled = false;
        }

        // --- 右键菜单事件处理 ---        
        /// <summary>
        /// 播放录音菜单项点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void PlayMenuItem_Click(object? sender, EventArgs e)
        {
            StartPlayback();
        }

        /// <summary>
        /// 删除录音菜单项点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private async void DeleteMenuItem_Click(object? sender, EventArgs e)
        {
            await DeleteSelectedFileAsync();
        }

        /// <summary>
        /// 文件列表鼠标按下事件，用于右键点击时自动选中文件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void lstFiles_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                // 获取右键点击位置的索引
                int index = lstFiles.IndexFromPoint(e.Location);
                if (index != -1)
                {
                    // 选中点击位置的文件
                    lstFiles.SelectedIndex = index;
                }
            }
        }

        /// <summary>
        /// 从文件夹打开菜单项点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void OpenFolderMenuItem_Click(object? sender, EventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(currentFilePath))
                {
                    // 打开文件所在文件夹并选中该文件
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{currentFilePath}\"",
                        UseShellExecute = true
                    });
                }
                else
                {
                    // 如果没有选中文件，只打开文件夹
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{_recordingsFolder}\"",
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开文件夹失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 在 UI 线程执行操作（音频回调线程不能直接操作控件）
        /// </summary>
        private void RunOnUiThread(Action action)
        {
            if (action == null || IsDisposed || Disposing)
                return;

            if (!IsHandleCreated)
                return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(action);
                }
                catch (InvalidOperationException)
                {
                    // 窗体正在关闭或已释放，忽略
                }
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// 关闭窗体时停止录音和播放，确保 WAV 文件写入完整
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            uiTimer.Stop();
            progressTimer.Stop();
            recordingTimer.Stop();

            audioRecorder?.Dispose();
            audioRecorder = null;

            audioPlayer?.Dispose();
            audioPlayer = null;

            fileContextMenu?.Dispose();
            fileContextMenu = null;

            base.OnFormClosing(e);
        }

        // --- 播放模式相关 ---        
        /// <summary>
        /// 播放模式选择事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void cmbPlaybackMode_SelectedIndexChanged(object? sender, EventArgs e)
        {
            ComboBox? comboBox = sender as ComboBox;
            if (comboBox != null)
            {
                switch (comboBox.SelectedIndex)
                {
                    case 0: // 播完停止
                        currentPlaybackMode = PlaybackMode.StopWhenFinished;
                        break;
                    case 1: // 单曲循环
                        currentPlaybackMode = PlaybackMode.SingleLoop;
                        break;
                    case 2: // 顺序播放
                        currentPlaybackMode = PlaybackMode.Sequential;
                        break;
                }
            }
        }

    }
}