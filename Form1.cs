using NAudio.Gui;
using NAudio.Wasapi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace ProAudioRecorder
{
    public partial class Form1 : Form
    {
        // --- 变量声明 ---
        private WaveInEvent? waveIn; // 录音输入
        private WaveFileWriter? writer; // 录音文件写入
        private WaveOutEvent? waveOut; // 播放输出
        private AudioFileReader? audioFile; // 播放文件读取
        private string currentFilePath = ""; // 当前录音文件路径

        private bool isRecording = false;
        private bool isPlaying = false;
        private bool isPaused = false; // --- 标记是否暂停 ---
        private DateTime recordingStartTime; // 记录开始录音的时间点
        private TimeSpan totalPausedDuration = TimeSpan.Zero; // 记录暂停前的总时长
        private float currentMaxVol = 0; // 加上这一行，用来存音量

        private System.Windows.Forms.Timer uiTimer; // UI刷新定时器
        private float targetValue = 0; // 目标音量（录音线程写入）
        private float currentValue = 0; // 当前显示的音量（UI线程读取，用于平滑过渡）

        private string _recordingsFolder = Path.Combine(Application.StartupPath, "Recordings");// 定义录音文件保存的文件夹

        private double totalDuration = 0;//// 用于存储音频总时长（秒）

        private System.Windows.Forms.Timer progressTimer; // 用于更新进度条的计时器

        private bool isUserDragging = false;// 定义一个全局变量，用于标记用户是否正在拖动进度条
   
        private float previousVolume = 1.0f;// 用于记录静音前的音量，方便取消静音


        public Form1()
        {
            InitializeComponent();

            // 初始化设备列表
            InitializeDevices();

            //设置示波器最大点数
            waveFormControl1.MaxPoints = 60;

            // 启动时加载已有的录音文件
            LoadExistingRecordings();

            // 1. 绑定录音按钮
            btnStartPause.Click += btnStartPause_Click;
            btnStopRec.Click += btnStopRec_Click;
            btnPauseRec.Click += btnPauseRec_Click;

            // 2. 绑定播放按钮
            btnPlay.Click += btnPlay_Click;
            btnPausePlay.Click += btnPausePlay_Click;
            btnStopPlay.Click += btnStopPlay_Click;

            //绑定文件选中事件
            this.lstFiles.SelectedIndexChanged += new System.EventHandler(this.lstFiles_SelectedIndexChanged);

            //把 MouseDown 和 MouseUp 事件绑定到了进度条上
            this.trackProgress.MouseDown += new System.Windows.Forms.MouseEventHandler(this.trackProgress_MouseDown);
            this.trackProgress.MouseUp += new System.Windows.Forms.MouseEventHandler(this.trackProgress_MouseUp);

            // 绑定进度条的滚动事件
            this.trackProgress.Scroll += new EventHandler(this.trackProgress_Scroll);

            // 绑定音量条的滚动事件
            this.trackVolume.Scroll += new System.EventHandler(this.trackVolume_Scroll);

            // 绑定静音复选框的状态改变事件
            this.chkMute.CheckedChanged += new System.EventHandler(this.chkMute_CheckedChanged);

            // 绑定定时器的时间触发事件
            timerRecording.Tick += timerRecording_Tick;

            //初始化录音按键
            isRecording = true; // 开始录音按钮可用状态
            btnStopRec.Enabled = false;//暂停录音按钮可用状态
            btnPauseRec.Enabled = false;//停止录音按钮可用状态

            // 初始化定时器
            uiTimer = new System.Windows.Forms.Timer();
            uiTimer.Interval = 1; // 1毫秒刷新一次
            uiTimer.Tick += UiTimer_Tick;
            uiTimer.Start();

            // 初始化计时器
            progressTimer = new System.Windows.Forms.Timer();
            progressTimer.Interval = 100; // 每 100毫秒 更新一次（0.1秒）
            progressTimer.Tick += ProgressTimer_Tick; // 绑定更新事件

        }

        // --- 初始化设备 ---
        private void InitializeDevices()
        {
            // 清空列表
            cmbDevices.Items.Clear();

            // 添加所有可用的录音设备
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var caps = WaveIn.GetCapabilities(i);
                cmbDevices.Items.Add(caps.ProductName);
            }

            // 默认选中第一个
            if (cmbDevices.Items.Count > 0)
            {
                cmbDevices.SelectedIndex = 0;
            }
        }

        // 加载已有文件的函数
        private void LoadExistingRecordings()
        {
            // 1. 如果文件夹不存在，就创建一个
            if (!Directory.Exists(_recordingsFolder))
            {
                Directory.CreateDirectory(_recordingsFolder);
                return; // 刚创建说明是空的，直接返回
            }

            // 2.先清空列表，防止重复添加
            lstFiles.Items.Clear();

            // 3. 获取所有 .wav 文件
            string[] files = Directory.GetFiles(_recordingsFolder, "*.wav");

            // 4. 使用 LINQ 将文件列表倒序排列（最新的在前面）
            var sortedFiles = files.OrderByDescending(f => f).ToArray();

            // 5. 添加到列表
            foreach (string file in sortedFiles)
            {
                string fileName = Path.GetFileName(file);
                string durationText = "";

                try
                {
                    // 使用 NAudio 读取时长
                    using (var audioFile = new NAudio.Wave.AudioFileReader(file))
                    {
                        // audioFile.TotalTime 是一个 TimeSpan 对象
                        // ToString(@"mm\:ss") 可以将其格式化为 "分:秒"
                        durationText = audioFile.TotalTime.ToString(@"mm\:ss");
                    }
                }
                catch
                {
                    // 如果读取失败（比如文件损坏），显示 "00:00" 或空
                    durationText = "00:00";
                }

                // 将文件名和时长拼接后添加到列表 效果：2026-04-09-120000.wav [00:05]
                lstFiles.Items.Add($"{fileName} [{durationText}]");
            }

            // 根据列表是否有内容，决定是否启用播放按钮
            if (lstFiles.Items.Count > 0)
            {
                btnPlay.Enabled = true;//按钮不可用状态
                btnPausePlay.Enabled = false;//按钮不可用状态
                btnStopPlay.Enabled = false;//按钮不可用状态

                // 自动选中第一个（最新的）
                lstFiles.SelectedIndex = 0;

                // 强制手动更新 currentFilePath
                // 因为初始化时 SelectedIndexChanged 事件可能还没完全生效
                // 我们直接读取第一行的文本并解析路径
                string firstItemText = lstFiles.Items[0].ToString();
                string fileName = "";

                if (firstItemText.Contains(" ["))
                {
                    fileName = firstItemText.Substring(0, firstItemText.IndexOf(" ["));
                }

                // 更新全局路径变量
                currentFilePath = Path.Combine(_recordingsFolder, fileName);
            }
            else
            {
                currentFilePath = ""; // 没文件就清空
                btnPlay.Enabled = false;//按钮不可用状态
                btnPausePlay.Enabled = false;//按钮不可用状态
                btnStopPlay.Enabled = false;//按钮不可用状态
            }
        }

        // 定时器事件：负责平滑地更新界面
        private void UiTimer_Tick(object sender, EventArgs e)
        {
            // 动态调整平滑系数
            // 如果当前值小于目标值（声音变大），快速跟上 (系数 0.3)
            // 如果当前值大于目标值（声音变小），缓慢回落 (系数 0.05)，制造“弹回”的效果
            float speed = (currentValue < targetValue) ? 1.0f : 0.5f;

            // 简单的线性插值
            currentValue = currentValue + (targetValue - currentValue) * speed;

            // 强制安全检查（防止越界崩溃）
            int val = (int)(currentValue * 20); // 这里的 20 取决于你的 ProgressBar.Maximum
            if (val < progLevel.Minimum) val = progLevel.Minimum;
            if (val > progLevel.Maximum) val = progLevel.Maximum;

            progLevel.Value = val;
        }

        // --- 录音按钮逻辑 ---
        private void btnStartPause_Click(object sender, EventArgs e)
        {
            waveFormControl1.Clear();//清空示波器

            // 情况 A：如果是从“暂停”状态点击的 -> 继续录音
            if (isPaused)
            {
                waveIn.DataAvailable += WaveIn_DataAvailable;

                // --- 核心修改：重置开始时间 ---
                recordingStartTime = DateTime.Now;

                waveIn?.StartRecording(); // 底层继续录音
                timerRecording.Start();   // 计时器继续走

                waveFormControl1.MaxPoints = 50;

                // UI 更新
                isPaused = false;
                isRecording = true; // 恢复录音状态
                btnStartPause.Text = "暂停录音"; // 按钮变回“暂停”
                btnStopRec.Enabled = true;
                btnPauseRec.Enabled = true;
                cmbDevices.Enabled = false;
                lblStatus.Text = "正在录音...";
                return;
            }

            // 情况 B：如果是全新开始 -> 启动录音
            if (!isRecording)
            {
                try
                {
                    // 1. 准备文件路径
                    string recordDir = Path.Combine(Application.StartupPath, "Recordings");
                    if (!Directory.Exists(recordDir)) Directory.CreateDirectory(recordDir);

                    string fileName = $"{DateTime.Now:yyyy-MM-dd-HHmmss}.wav";
                    currentFilePath = Path.Combine(recordDir, fileName);
                    if (File.Exists(currentFilePath)) File.Delete(currentFilePath);

                    // 2. 初始化录音设备
                    waveIn = new WaveInEvent();
                    waveIn.DeviceNumber = cmbDevices.SelectedIndex;
                    waveIn.WaveFormat = new WaveFormat(44100, 16, 1);
                    waveIn.DataAvailable += WaveIn_DataAvailable;
                    waveIn.RecordingStopped += WaveIn_RecordingStopped;

                    // 3. 准备写入文件
                    writer = new WaveFileWriter(currentFilePath, waveIn.WaveFormat);

                    // 4. 开始录音
                    waveIn.StartRecording();
                    recordingStartTime = DateTime.Now;
                    timerRecording.Start();

                    // 5. UI 更新
                    isRecording = true;
                    btnStartPause.Text = "暂停录音";
                    btnStopRec.Enabled = true;
                    btnPauseRec.Enabled = true;
                    cmbDevices.Enabled = false;
                    lblStatus.Text = "正在录音...";
                    waveFormControl1.Clear();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("录音启动失败: " + ex.Message);
                }
            }
            // -------------------------------------------------------
            // 情况 C：如果是正在录音（且没暂停） -> 触发暂停
            else if (isRecording && !isPaused)
            {
                // 标记为即将暂停
                isPaused = true;

                // 调用 StopRecording，这会触发 WaveIn_RecordingStopped 事件
                // 我们在那里处理真正的 UI 更新和资源挂起
                waveIn?.StopRecording();
            }
        }

        // --- 定时器触发事件（每100毫秒执行一次）更新录音时长 ---
        private void timerRecording_Tick(object sender, EventArgs e)
        {
            // 只有在录音中且没有暂停时才更新
            if (isRecording && !isPaused)
            {
                // 1. 计算从“这次”开始录音到现在过了多久
                TimeSpan currentSegmentDuration = DateTime.Now - recordingStartTime;

                // 2. 总时长 = 之前积累的时长 + 当前这一段时长
                TimeSpan totalDuration = totalPausedDuration + currentSegmentDuration;

                // 3. 更新UI
                lblDuration.Text = totalDuration.ToString(@"hh\:mm\:ss");

            }
        }

        private void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            // 1. 写入文件 (保持不变)
            writer?.Write(e.Buffer, 0, e.BytesRecorded);

            // 2. 添加数据（只传 2 个参数）
            waveFormControl1.AddAudioData(e.Buffer, e.BytesRecorded);

            // 3. 计算音量峰值 (保持不变)
            float max = 0;
            for (int i = 0; i < e.BytesRecorded; i += 2)
            {
                short sample = (short)((e.Buffer[i + 1] << 8) | e.Buffer[i]);
                float val = Math.Abs(sample / 32768f);
                if (val > max) max = val;
            }

            // 4. 更新电平条 (保持不变)
            targetValue = Math.Min(max * 5.0f, 1.0f);
        }

        private void WaveIn_RecordingStopped(object? sender, StoppedEventArgs e)
        {
            // --- 情况 A：如果是“暂停”操作触发的 ---
            if (isPaused)
            {
                // --- 计算暂停前的总时长并保存 ---
                // 先算出这一段的时长
                TimeSpan lastSegment = DateTime.Now - recordingStartTime;
                // 累加到总时长里
                totalPausedDuration += lastSegment;
                // 1. 暂停计时器
                timerRecording.Stop();
                // 状态重置
                isRecording = false;

                // 2. 更新 UI (切回“继续”模式)
                this.Invoke(new Action(() =>
                {
                    btnStartPause.Text = "继续录音"; // 按钮变字
                    btnStopRec.Enabled = false;      // 禁用停止
                    btnPauseRec.Enabled = false;     // 禁用暂停
                    cmbDevices.Enabled = false;      // 依然禁用设备选择
                    lblStatus.Text = "已暂停";
                    // --- 新增：强制归零 ---
                    targetValue = 0;    // 目标设为0
                    currentValue = 0;   // 当前显示设为0
                    progLevel.Value = 0; // 立即刷新界面

                    // 注意：这里不要释放 waveIn 和 writer，否则无法继续写文件！
                }));
                return; // 直接返回，不要执行后面的释放代码！
            }

            // --- 情况 B：如果是“停止”操作触发的 (真正的结束) ---

            // 1. 释放资源
            waveIn?.Dispose();
            waveIn = null;
            writer?.Dispose();
            writer = null;

            // 2. 更新 UI
            this.Invoke(new Action(() =>
            {
                isRecording = false;
                isPaused = false;
                totalPausedDuration = TimeSpan.Zero;

                btnStartPause.Text = "开始录音"; // 按钮复位
                btnStartPause.Enabled = true;
                btnStopRec.Enabled = false;
                btnPauseRec.Enabled = false;
                cmbDevices.Enabled = true;
                lblStatus.Text = "录音完成";
                // --- 新增：强制归零 ---
                targetValue = 0;    // 目标设为0
                currentValue = 0;   // 当前显示设为0
                progLevel.Value = 0; // 立即刷新界面

                // 添加文件到列表
                if (!string.IsNullOrEmpty(currentFilePath))
                {
                    LoadExistingRecordings();// 重新加载列表会自动选中最新的录音文件
                    if (lstFiles.Items.Count > 0)
                    {
                        lstFiles.SelectedIndex = 0;// 自动选中最新的录音文件
                    }
                }

                timerRecording.Stop();// 停止计时器
                progLevel.Value = 0;//电平条重置
                lblDuration.Text = "00:00:00";//时长标签重置
            }));
        }

        private void btnStopRec_Click(object sender, EventArgs e)
        {
            if (!isRecording) return;

            // 如果是暂停状态点击停止，我们需要先标记为“不是暂停”，告诉系统这是真的停止
            isPaused = false;
            waveIn?.StopRecording(); // 触发 RecordingStopped

            // 清空示波器
            waveFormControl1.Clear();
        }

        private void btnPauseRec_Click(object sender, EventArgs e)
        {
            if (!isRecording || isPaused) return;

            try
            {
                // 1. 底层暂停（注意：WaveInEvent 的 Pause 方法只是暂停回调，文件流还是开着的）
                waveIn?.StopRecording(); 
                // 注意：NAudio 的 WaveInEvent 其实没有真正的 Pause，通常是用 StopRecording 模拟，但为了不触发 RecordingStopped，我们需要特殊处理，或者直接用 StopRecording 并在逻辑里判断。
                // 修正：WaveInEvent 实际上没有 Pause 方法，只有 Start 和 Stop。
                // 为了模拟暂停，我们调用 StopRecording，但在 RecordingStopped 事件里要判断是不是“暂停”。
                // 但更简单的做法是：直接停止数据回调，但保持文件流打开。
                // 这里我们沿用 StopRecording，配合 isPaused 标志位。

                // 实际上，WaveInEvent.StopRecording() 会触发 RecordingStopped。
                // 为了不把事情搞复杂，我们这里直接用 .Pause() 如果有的话，或者仅仅停止数据写入。
                // 既然 NAudio 的 WaveInEvent 比较底层，我们这里采用“逻辑暂停”：
                // 我们不调用 waveIn.StopRecording()，而是直接暂停定时器和更新 UI。
                // 但为了真正停止占用麦克风，最好还是调用 StopRecording。

                // --- 最佳实践方案 ---
                // 我们调用 waveIn.StopRecording()。这会触发 RecordingStopped。
                // 我们需要在 RecordingStopped 里判断 isPaused。

                isPaused = true; // 标记即将暂停
                waveIn?.StopRecording(); // 这会触发下面的 RecordingStopped 事件

                // 注意：UI 更新将在 RecordingStopped 事件里统一处理，以防状态不同步
            }
            catch (Exception ex)
            {
                MessageBox.Show("暂停出错: " + ex.Message);
            }
        }

        // --- 播放按钮逻辑 ---

        private void btnPlay_Click(object sender, EventArgs e)
        {
            StartPlayback();
        }

        // 进度更新计时器事件
        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            // 1. 确保音频文件存在且已初始化
            if (audioFile == null) return;

            // 2. 只有当用户没在拖动时，才更新进度条
            if (!isUserDragging)
            {
                try
                {
                    // 获取当前播放时间（秒）
                    double currentTimeSeconds = audioFile.CurrentTime.TotalSeconds;

                    // 确保进度条的最大值足够大 (防御性编程)
                    if (trackProgress.Maximum < totalDuration)
                    {
                        trackProgress.Maximum = (int)totalDuration;
                    }

                    // 更新进度条 (注意：确保 currentTime 不超过 Maximum)
                    int currentVal = (int)currentTimeSeconds;
                    if (currentVal <= trackProgress.Maximum)
                    {
                        trackProgress.Value = currentVal;
                    }

                    // 更新时间标签
                    UpdateTimeLabel(currentTimeSeconds, totalDuration);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);// 记录日志，防止 UI 线程崩溃
                }
            }
        }

        // 用户拖动或点击进度条时触发
        private void trackProgress_Scroll(object sender, EventArgs e)
        {
            // 如果用户正在拖动，才执行跳转
            if (isUserDragging && audioFile != null)
            {
                audioFile.CurrentTime = TimeSpan.FromSeconds(trackProgress.Value); // 1. 跳转音频

                UpdateTimeLabel(trackProgress.Value, totalDuration);// 2.主动更新时间标签
            }
        }

        // 2. 添加 MouseDown 事件（用户按下鼠标）
        private void trackProgress_MouseDown(object sender, MouseEventArgs e)
        {
            isUserDragging = true;
        }

        // 3. 添加 MouseUp 事件（用户松开鼠标）
        private void trackProgress_MouseUp(object sender, MouseEventArgs e)
        {
            isUserDragging = false;
            // 松开后立即跳转一次，确保精准
            if (audioFile != null)
            {
                audioFile.CurrentTime = TimeSpan.FromSeconds(trackProgress.Value);
            }
        }

        // 播放结束事件
        private void WaveOut_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            StopAndReset();
        }

        /// <summary>
        /// 更新时间标签的显示格式：mm:ss / mm:ss
        /// </summary>
        private void UpdateTimeLabel(double current, double total)
        {
            // 将秒数转换为 TimeSpan 以便格式化
            TimeSpan currentTimeSpan = TimeSpan.FromSeconds(current);
            TimeSpan totalTimeSpan = TimeSpan.FromSeconds(total);

            // 格式化为 "mm:ss"
            // 如果小时数大于0，可以改为 hh\:mm\:ss
            string currentStr = currentTimeSpan.ToString(@"mm\:ss");
            string totalStr = totalTimeSpan.ToString(@"mm\:ss");

            lblTime.Text = $"{currentStr} / {totalStr}";
        }

        // 核心音频文件播放方法
        private void StartPlayback()
        {
            if (isPlaying)
            {
                StopAndReset();
            }

            // 额外保险：防止文件路径为空
            if (string.IsNullOrEmpty(currentFilePath)) return;

            if (!File.Exists(currentFilePath))
            {
                MessageBox.Show("请先选中需要播放的音频文件！");
                return;
            }

            try
            {
                // 1. 准备播放
                audioFile = new AudioFileReader(currentFilePath);
                totalDuration = audioFile.TotalTime.TotalSeconds;//获取总时长
                UpdateTimeLabel(0, totalDuration);// 获取总时长 
                waveOut = new WaveOutEvent();
                waveOut.Init(audioFile);


                // 2.设置进度条
                trackProgress.Maximum = (int)audioFile.TotalTime.TotalSeconds;
                trackProgress.Value = 0; // 归零

                // 3. 开始播放
                waveOut.PlaybackStopped += WaveOut_PlaybackStopped;
                waveOut.Play();

                // 4. 启动计时器
                progressTimer.Start();

                isPlaying = true;
                btnPlay.Enabled = false;
                btnPausePlay.Enabled = true;
                btnStopPlay.Enabled = true;
                btnPausePlay.Text = "暂停播放";
                lblStatus.Text = "正在播放...";
            }
            catch (Exception ex)
            {
                MessageBox.Show("播放失败: " + ex.Message);
            }
        }

        private void StopAndReset()
        {
            // 2. 停止计时器
            progressTimer?.Stop();

            // 3. 重置进度条
            trackProgress.Value = 0;

            // 重置标签
            lblTime.Text = "00:00 / 00:00";
            totalDuration = 0;

            // 重置音量控件状态
            trackVolume.Value = 100;
            chkMute.Checked = false;
            if (trackVolume.Enabled == false) trackVolume.Enabled = true;

            // 1. 停止播放
            if (waveOut != null)
            {
                // 1. 先解绑播放结束事件 防止旧事件干扰
                waveOut.PlaybackStopped -= WaveOut_PlaybackStopped;
                try
                {
                    waveOut.Stop();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("停止播放出错: " + ex.Message);
                }
                waveOut.Dispose();
                waveOut = null;
            }

            if (audioFile != null)
            {
                try { audioFile.Dispose(); } catch { }
                audioFile = null;
            }

            // 给系统一点点释放时间（可选，视情况而定）
            System.Threading.Thread.Sleep(50);

            // 5. 更新状态
            isPlaying = false;
            lblStatus.Text = "已停止";
            btnPlay.Enabled = true;
            btnPausePlay.Enabled = false;
            btnStopPlay.Enabled = false;
        }

        // 暂停/继续 按钮
        private void btnPausePlay_Click(object sender, EventArgs e)
        {
            if (waveOut == null) return;

            // 判断当前状态：如果是播放中，则暂停；如果是暂停中，则继续
            if (waveOut.PlaybackState == PlaybackState.Playing)
            {
                waveOut.Pause();
                lblStatus.Text = "播放已暂停";
                btnPausePlay.Text = "继续播放"; // 修改按钮文字提示用户
            }
            else if (waveOut.PlaybackState == PlaybackState.Paused)
            {
                waveOut.Play();
                lblStatus.Text = "正在播放...";
                btnPausePlay.Text = "暂停播放"; // 恢复按钮文字
            }
        }

        // 停止按钮
        private void btnStopPlay_Click(object sender, EventArgs e)
        {
            StopAndReset();// 直接调用之前写好的彻底清理方法
        }

        // --- 双击文件列表文件事件 触发播放音频文件 ---
        private void lstFiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            // 1. 如果没有选中任何项，直接返回
            if (lstFiles.SelectedIndex == -1) return;

            // 2. 获取选中的文本
            string selectedText = lstFiles.SelectedItem.ToString();

            // 3. 提取纯文件名
            string fileName = "";
            if (selectedText.Contains(" ["))
            {
                fileName = selectedText.Substring(0, selectedText.IndexOf(" ["));
            }
            else
            {
                fileName = selectedText;
            }

            // 4. 拼接完整路径
            currentFilePath = Path.Combine(_recordingsFolder, fileName);
        }

        // --- 文件列表按键删除处理事件（可能无效） ---
        private void lstFiles_KeyDown(object? sender, KeyEventArgs e)
        {
            // 如果按下了 Delete 键，且列表里有选中项
            if (e.KeyCode == Keys.Delete && lstFiles.SelectedItem != null)
            {
                string fileName = lstFiles.SelectedItem.ToString();

                // 二次确认
                DialogResult result = MessageBox.Show(
                    $"确定要删除文件 \"{fileName}\" 吗？",
                    "确认删除",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        // 1. 获取完整路径
                        string recordDir = Path.Combine(Application.StartupPath, "Recordings");
                        string pathToDelete = Path.Combine(recordDir, fileName);

                        // 2. 删除文件
                        if (File.Exists(pathToDelete))
                        {
                            File.Delete(pathToDelete);
                        }

                        // 3. 从列表中移除
                        lstFiles.Items.Remove(lstFiles.SelectedItem);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("删除失败: " + ex.Message);
                    }
                }
            }
        }

        // 删除按钮事件 删除选中的文件
        private void btnDelete_Click(object sender, EventArgs e)
        {
            // 1. 检查有没有选中任何文件
            if (lstFiles.SelectedItem == null)
            {
                MessageBox.Show("请先在列表中选择一个要删除的文件！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return; // 直接退出，不执行后面的代码
            }

            // 1. 获取列表显示的完整文本
            string fullText = lstFiles.SelectedItem.ToString();

            // 2. 提取真正的文件名
            string fileName = fullText.Split(new string[] { " [" }, StringSplitOptions.None)[0];

            // 3. 二次确认
            DialogResult result = MessageBox.Show($"确定要删除文件 \"{fileName}\" 吗？", "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                try
                {
                    // 4. 拼接完整路径
                    string recordDir = Path.Combine(Application.StartupPath, "Recordings");
                    string pathToDelete = Path.Combine(recordDir, fileName);

                    // 5. 执行删除
                    if (File.Exists(pathToDelete))
                    {
                        File.Delete(pathToDelete);
                    }

                    // 6. 从列表移除
                    lstFiles.Items.Remove(lstFiles.SelectedItem);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("删除失败: " + ex.Message);
                }
            }
        }

        private void lstFiles_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                StartPlayback();
            }
        }

        //音量条滚动事件
        private void trackVolume_Scroll(object sender, EventArgs e)
        {
            if (audioFile != null)
            {
                // TrackBar 的值是 0-100，AudioFileReader.Volume 需要 0.0-1.0
                float volume = trackVolume.Value / 100.0f;
                audioFile.Volume = volume;

                // 如果用户手动调节了音量，自动取消静音
                if (chkMute.Checked)
                {
                    chkMute.Checked = false;
                }
            }
        }

        //静音复选框事件
        private void chkMute_CheckedChanged(object sender, EventArgs e)
        {
            if (audioFile != null)
            {
                if (chkMute.Checked)
                {
                    // 静音：保存当前音量，然后设为 0
                    previousVolume = audioFile.Volume;
                    audioFile.Volume = 0.0f;

                    // 可选：把音量条变灰，表示不可用
                    trackVolume.Enabled = false;
                }
                else
                {
                    // 取消静音：恢复之前的音量
                    audioFile.Volume = previousVolume;

                    // 可选：恢复音量条可用
                    trackVolume.Enabled = true;
                }
            }
        }
    }
}