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
        private string _recordingsFolder = Path.Combine(Application.StartupPath, "Recordings");

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
        /// 标记是否是用户主动停止播放
        /// </summary>
        private bool isUserStopping = false;

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

            // 启动时加载已有的录音文件
            LoadExistingRecordings();

            // 绑定事件
            BindEvents();

            // 初始化定时器
            InitializeTimers();

            // 初始化右键菜单
            InitializeContextMenu();

            //初始化录音按键
            UpdateRecordingButtonStates();
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
            }
        }

        /// <summary>
        /// 加载已有录音文件
        /// </summary>
        private void LoadExistingRecordings()
        {
            // 1.先清空列表，防止重复添加
            lstFiles.Items.Clear();

            // 2. 获取所有录音文件
            var files = fileService?.FileManager.GetRecordingFiles();
            if (files != null)
            {
                foreach (var fileInfo in files)
                {
                    // 将文件名和时长拼接后添加到列表 效果：2026-04-09-120000.wav [00:05]
                    lstFiles.Items.Add($"{fileInfo.FileName} [{fileInfo.Duration.ToString(@"mm\:ss")}]");
                }

                // 根据列表是否有内容，决定是否启用播放按钮
                if (lstFiles.Items.Count > 0)
                {
                    btnPlay.Enabled = true;
                    btnPausePlay.Enabled = false;
                    btnStopPlay.Enabled = false;

                    // 自动选中第一个（最新的）
                    lstFiles.SelectedIndex = 0;

                    // 强制手动更新 currentFilePath
                    string firstItemText = lstFiles.Items[0]?.ToString() ?? "";
                    string fileName = fileService?.ExtractFileNameFromDisplayText(firstItemText) ?? "";

                    // 更新全局路径变量
                    currentFilePath = fileService?.GetFullPath(fileName) ?? "";
                }
                else
                {
                    currentFilePath = ""; // 没文件就清空
                    btnPlay.Enabled = false;
                    btnPausePlay.Enabled = false;
                    btnStopPlay.Enabled = false;
                }
            }
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

            // 强制安全检查（防止越界崩溃）
            int val = (int)(currentValue * 15); // 这里的 15 取决于你的 ProgressBar.Maximum
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

            if (!fileService?.FileExists(currentFilePath) ?? true)
            {
                MessageBox.Show("请先选中需要播放的音频文件！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                // 检查是否正在录音，如果是则停止录音
                if (audioRecorder != null && (audioRecorder.IsRecording || audioRecorder.IsPaused))
                {
                    audioRecorder.StopRecording();
                    recordingTimer.Stop();
                }

                // 1. 加载文件
                audioPlayer.LoadFile(currentFilePath);

                // 2.设置进度条
                trackProgress.Maximum = (int)audioPlayer.TotalDuration.TotalSeconds;
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
        }

        /// <summary>
        /// 停止播放
        /// </summary>
        private void StopPlayback()
        {
            progressTimer?.Stop();

            trackProgress.Value = 0;
            lblTime.Text = "00:00 / 00:00";

            trackVolume.Value = 100;
            chkMute.Checked = false;
            if (trackVolume.Enabled == false) trackVolume.Enabled = true;

            targetValue = 0;
            currentValue = 0;
            progLevel.Value = 0;

            if (audioPlayer != null)
            {
                // 标记为用户主动停止
                isUserStopping = true;
                audioPlayer.Stop();
            }

            lblStatus.Text = "已停止";

            btnPlay.Enabled = true;
            btnPausePlay.Enabled = false;
            btnStopPlay.Enabled = false;
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
            }
            else
            {
                audioPlayer.Pause();
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
            // 1. 如果没有选中任何项，直接返回
            if (lstFiles.SelectedIndex == -1 || lstFiles.SelectedItem == null) return;

            // 2. 获取选中的文本
            string selectedText = lstFiles.SelectedItem.ToString() ?? "";

            // 3. 提取纯文件名
            string fileName = fileService?.ExtractFileNameFromDisplayText(selectedText) ?? "";

            // 4. 拼接完整路径
            currentFilePath = fileService?.GetFullPath(fileName) ?? "";
        }

        // --- 文件删除相关方法 ---
        /// <summary>
        /// 文件列表键盘按下事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void lstFiles_KeyDown(object? sender, KeyEventArgs e)
        {
            // 如果按下了 Delete 键，且列表里有选中项
            if (e.KeyCode == Keys.Delete && lstFiles.SelectedItem != null)
            {
                DeleteSelectedFile();
            }
        }

        /// <summary>
        /// 删除按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void btnDelete_Click(object? sender, EventArgs e)
        {
            DeleteSelectedFile();
        }

        /// <summary>
        /// 删除选中的文件
        /// </summary>
        private void DeleteSelectedFile()
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
                if (lstFiles.SelectedItem == null)
                {
                    MessageBox.Show("请先在列表中选择一个要删除的文件！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 2. 获取列表显示的完整文本
                string fullText = lstFiles.SelectedItem.ToString() ?? "";

                // 3. 提取真正的文件名
                string fileName = fileService?.ExtractFileNameFromDisplayText(fullText) ?? "";

                // 4. 记录当前选中文件的索引
                int deletedIndex = lstFiles.SelectedIndex;
                
                // 5. 二次确认
                if (MessageBox.Show($"确定要删除文件 \"{fileName}\" 吗？", "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    try
                    {
                        fileService?.DeleteFile(fileName);
                        
                        // 延迟重新加载文件列表，确保所有事件处理完毕
                        System.Threading.Thread.Sleep(100);
                        LoadExistingRecordings();
                        
                        // 删除后选中下一个文件
                        if (lstFiles.Items.Count > 0)
                        {
                            // 如果删除的文件下面还有文件，选中该索引位置的文件
                            if (deletedIndex < lstFiles.Items.Count)
                            {
                                lstFiles.SelectedIndex = deletedIndex;
                            }
                            else
                            {
                                // 如果删除的文件下面没有文件了，选中最新文件（索引0）
                                lstFiles.SelectedIndex = 0;
                            }
                            
                            // 更新 currentFilePath
                            string selectedText = lstFiles.SelectedItem?.ToString() ?? "";
                            string selectedFileName = fileService?.ExtractFileNameFromDisplayText(selectedText) ?? "";
                            currentFilePath = fileService?.GetFullPath(selectedFileName) ?? "";
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("删除失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
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
                // TrackBar 的值是 0-100，AudioFileReader.Volume 需要 0.0-1.0
                float volume = trackVolume.Value / 100.0f;
                audioPlayer.Volume = volume;

                // 如果用户手动调节了音量，自动取消静音
                if (chkMute.Checked)
                {
                    chkMute.Checked = false;
                }
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
            // 使用 BeginInvoke 异步更新变量，避免阻塞主线程
            if (InvokeRequired)
            {
                BeginInvoke(new Action<float>((vol) => {
                    targetValue = vol;
                }), volume);
            }
            else
            {
                targetValue = volume;
            }
        }

        /// <summary>
        /// 录音波形数据改变事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="waveformData">波形数据</param>
        private void AudioRecorder_WaveformDataChanged(object? sender, float waveformData)
        {
            if (waveFormControl1 != null && audioRecorder != null && audioRecorder.IsRecording)
            {
                // 使用 BeginInvoke 异步更新 UI，避免阻塞主线程
                if (InvokeRequired)
                {
                    BeginInvoke(new Action<float>((data) => {
                        if (waveFormControl1 != null && audioRecorder != null && audioRecorder.IsRecording)
                        {
                            waveFormControl1.AddValue(data);
                        }
                    }), waveformData);
                }
                else
                {
                    waveFormControl1.AddValue(waveformData);
                }
            }
        }

        /// <summary>
        /// 播放音量改变事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="volume">音量值</param>
        private void AudioPlayer_VolumeChanged(object? sender, float volume)
        {
            if (audioPlayer != null && audioPlayer.IsPlaying)
            {
                // 使用 BeginInvoke 异步更新变量，避免阻塞主线程
                if (InvokeRequired)
                {
                    BeginInvoke(new Action<float>((vol) => {
                        if (audioPlayer != null && audioPlayer.IsPlaying)
                        {
                            targetValue = vol;
                        }
                    }), volume);
                }
                else
                {
                    targetValue = volume;
                }
            }
        }

        /// <summary>
        /// 播放波形数据改变事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="waveformData">波形数据</param>
        private void AudioPlayer_WaveformDataChanged(object? sender, float waveformData)
        {
            if (waveFormControl1 != null && audioPlayer != null && audioPlayer.IsPlaying)
            {
                // 使用 BeginInvoke 异步更新 UI，避免阻塞主线程
                if (InvokeRequired)
                {
                    BeginInvoke(new Action<float>((data) => {
                        if (waveFormControl1 != null && audioPlayer != null && audioPlayer.IsPlaying)
                        {
                            waveFormControl1.AddValue(data);
                        }
                    }), waveformData);
                }
                else
                {
                    waveFormControl1.AddValue(waveformData);
                }
            }
        }

        /// <summary>
        /// 录音开始事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void AudioRecorder_RecordingStarted(object? sender, EventArgs e)
        {
            // UI 更新
            btnStartPause.Text = "暂停录音";
            btnStopRec.Enabled = true;
            btnPauseRec.Enabled = true;
            cmbDevices.Enabled = false;
            lblStatus.Text = "正在录音...";
            waveFormControl1.Clear();
        }

        /// <summary>
        /// 录音停止事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void AudioRecorder_RecordingStopped(object? sender, EventArgs e)
        {
            // UI 更新
            btnStartPause.Text = "开始录音";
            btnStartPause.Enabled = true;
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

            // 重新加载文件列表
            LoadExistingRecordings();

            // 重置标签
            lblDuration.Text = "00:00:00";
        }

        /// <summary>
        /// 录音暂停事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void AudioRecorder_RecordingPaused(object? sender, EventArgs e)
        {
            // UI 更新
            btnStartPause.Text = "继续录音";
            btnStopRec.Enabled = false;
            btnPauseRec.Enabled = false;
            cmbDevices.Enabled = false;
            lblStatus.Text = "已暂停";
            
            // 强制归零
            targetValue = 0;
            currentValue = 0;
            progLevel.Value = 0;
        }

        /// <summary>
        /// 录音恢复事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void AudioRecorder_RecordingResumed(object? sender, EventArgs e)
        {
            // UI 更新
            btnStartPause.Text = "暂停录音";
            btnStopRec.Enabled = true;
            btnPauseRec.Enabled = true;
            cmbDevices.Enabled = false;
            lblStatus.Text = "正在录音...";
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
            trackProgress.Maximum = (int)duration.TotalSeconds;
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
        private void AudioPlayer_PlaybackStopped(object? sender, EventArgs e)
        {
            waveFormControl1.MaxPoints = 3000;
            waveFormControl1.Clear();
            
            // 检查是否是用户主动停止
            if (isUserStopping)
            {
                // 重置标志
                isUserStopping = false;
                // 不执行播放模式逻辑
                return;
            }
            
            // 根据播放模式执行不同操作
            switch (currentPlaybackMode)
            {
                case PlaybackMode.StopWhenFinished:
                    // 播完停止
                    StopPlayback();
                    break;
                case PlaybackMode.SingleLoop:
                    // 单曲循环
                    // 延迟一下，避免播放停止后立即重新开始导致的问题
                    System.Threading.Thread.Sleep(100);
                    StartPlayback();
                    break;
                case PlaybackMode.Sequential:
                    // 顺序播放
                    PlayNextFile();
                    break;
            }
        }
        
        /// <summary>
        /// 播放下一个文件
        /// </summary>
        private void PlayNextFile()
        {
            if (lstFiles.Items.Count > 0)
            {
                int currentIndex = lstFiles.SelectedIndex;
                int nextIndex = currentIndex + 1;
                
                // 如果不是最后一个文件，播放下一个
                if (nextIndex < lstFiles.Items.Count)
                {
                    lstFiles.SelectedIndex = nextIndex;
                    
                    // 更新 currentFilePath
                    string selectedText = lstFiles.SelectedItem?.ToString() ?? "";
                    string selectedFileName = fileService?.ExtractFileNameFromDisplayText(selectedText) ?? "";
                    currentFilePath = fileService?.GetFullPath(selectedFileName) ?? "";
                    
                    // 延迟一下，确保文件路径更新完成
                    System.Threading.Thread.Sleep(100);
                    StartPlayback();
                }
                else
                {
                    // 已经是最后一个文件，停止播放
                    StopPlayback();
                }
            }
            else
            {
                // 文件列表为空，停止播放
                StopPlayback();
            }
        }

        /// <summary>
        /// 更新录音按钮状态
        /// </summary>
        private void UpdateRecordingButtonStates()
        {
            btnStartPause.Enabled = true;
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
        private void DeleteMenuItem_Click(object? sender, EventArgs e)
        {
            DeleteSelectedFile();
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
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,{currentFilePath}");
                }
                else
                {
                    // 如果没有选中文件，只打开文件夹
                    System.Diagnostics.Process.Start("explorer.exe", _recordingsFolder);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开文件夹失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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