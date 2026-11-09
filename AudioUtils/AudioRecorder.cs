using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ProAudioRecorder.AudioUtils
{
    /// <summary>
    /// 音频录制器类，负责音频的录制、暂停、恢复和停止等操作。
    /// 暂停期间保持录音设备打开，但丢弃音频数据，避免暂停后无法正确停止和关闭文件。
    /// </summary>
    public class AudioRecorder : IDisposable
    {
        /// <summary>
        /// 音频输入设备
        /// </summary>
        private WaveInEvent? waveIn;
        /// <summary>
        /// 波形文件写入器
        /// </summary>
        private WaveFileWriter? writer;
        /// <summary>
        /// 当前录制的文件路径
        /// </summary>
        private string currentFilePath = "";
        /// <summary>
        /// 是否正在录制
        /// </summary>
        private volatile bool isRecording = false;
        /// <summary>
        /// 是否暂停
        /// </summary>
        private volatile bool isPaused = false;
        /// <summary>
        /// 是否正在停止录音，防止重复调用
        /// </summary>
        private volatile bool isStopping = false;
        /// <summary>
        /// 当前使用的录音设备编号
        /// </summary>
        private int deviceNumber;
        /// <summary>
        /// 录音计时器
        /// </summary>
        private readonly Stopwatch recordingStopwatch = new Stopwatch();
        /// <summary>
        /// 已累计的暂停时长
        /// </summary>
        private TimeSpan pausedDuration = TimeSpan.Zero;
        /// <summary>
        /// 本次暂停的开始时间
        /// </summary>
        private DateTime pauseStartedAtUtc;
        /// <summary>
        /// 目标音量值（用于音量条显示）
        /// </summary>
        private float targetValue = 0;

        /// <summary>
        /// 音量改变事件
        /// </summary>
        public event EventHandler<float>? VolumeChanged;
        /// <summary>
        /// 波形数据改变事件（每次回调发送一批采样点，避免 UI 线程被大量事件淹没）
        /// </summary>
        public event EventHandler<float[]>? WaveformDataChanged;
        /// <summary>
        /// 录制开始事件
        /// </summary>
        public event EventHandler? RecordingStarted;
        /// <summary>
        /// 录制停止事件
        /// </summary>
        public event EventHandler? RecordingStopped;
        /// <summary>
        /// 录制暂停事件
        /// </summary>
        public event EventHandler? RecordingPaused;
        /// <summary>
        /// 录制恢复事件
        /// </summary>
        public event EventHandler? RecordingResumed;

        /// <summary>
        /// 是否正在录制
        /// </summary>
        public bool IsRecording => isRecording;
        /// <summary>
        /// 是否暂停
        /// </summary>
        public bool IsPaused => isPaused;
        /// <summary>
        /// 当前录制的文件路径
        /// </summary>
        public string CurrentFilePath => currentFilePath;
        /// <summary>
        /// 当前使用的录音设备编号
        /// </summary>
        public int DeviceNumber => deviceNumber;

        /// <summary>
        /// 当前录制时长（不包含暂停时间）
        /// </summary>
        public TimeSpan CurrentDuration
        {
            get
            {
                if (!isRecording)
                    return TimeSpan.Zero;

                TimeSpan paused = pausedDuration;
                if (isPaused)
                    paused += DateTime.UtcNow - pauseStartedAtUtc;

                TimeSpan duration = recordingStopwatch.Elapsed - paused;
                return duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
            }
        }

        /// <summary>
        /// 开始录制
        /// </summary>
        /// <param name="deviceNumber">录音设备编号</param>
        /// <param name="outputDirectory">输出目录</param>
        /// <exception cref="Exception">录音启动失败时抛出</exception>
        public void StartRecording(int deviceNumber, string outputDirectory)
        {
            if (isRecording)
                return;

            try
            {
                // 准备文件路径，使用毫秒避免同一秒内重复录音时覆盖文件
                if (!Directory.Exists(outputDirectory))
                    Directory.CreateDirectory(outputDirectory);

                string fileName = $"{DateTime.Now:yyyy-MM-dd-HHmmss-fff}.wav";
                currentFilePath = Path.Combine(outputDirectory, fileName);

                this.deviceNumber = deviceNumber;

                // 初始化录音设备
                waveIn = new WaveInEvent
                {
                    DeviceNumber = deviceNumber,
                    WaveFormat = new WaveFormat(44100, 16, 1)
                };
                waveIn.DataAvailable += WaveIn_DataAvailable;
                waveIn.RecordingStopped += WaveIn_RecordingStopped;

                // 准备写入文件
                writer = new WaveFileWriter(currentFilePath, waveIn.WaveFormat);

                // 重置状态
                isRecording = true;
                isPaused = false;
                isStopping = false;
                pausedDuration = TimeSpan.Zero;
                recordingStopwatch.Restart();

                // 开始录音
                waveIn.StartRecording();

                // 触发录制开始事件
                RecordingStarted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                CleanupAfterFailedStart();
                throw new Exception("录音启动失败", ex);
            }
        }

        /// <summary>
        /// 启动失败时清理已分配的资源
        /// </summary>
        private void CleanupAfterFailedStart()
        {
            try
            {
                waveIn?.StopRecording();
            }
            catch
            {
                // 忽略清理阶段异常
            }

            waveIn?.Dispose();
            waveIn = null;
            writer?.Dispose();
            writer = null;

            recordingStopwatch.Reset();
            pausedDuration = TimeSpan.Zero;
            isRecording = false;
            isPaused = false;
            isStopping = false;
        }

        /// <summary>
        /// 暂停录制。暂停期间录音设备保持打开，但不再写入文件。
        /// </summary>
        public void PauseRecording()
        {
            if (!isRecording || isPaused || isStopping)
                return;

            isPaused = true;
            pauseStartedAtUtc = DateTime.UtcNow;
            RecordingPaused?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 恢复录制。录音设备一直保持打开，无需重新创建设备。
        /// </summary>
        public void ResumeRecording()
        {
            if (!isRecording || !isPaused || isStopping)
                return;

            pausedDuration += DateTime.UtcNow - pauseStartedAtUtc;
            isPaused = false;
            RecordingResumed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 停止录制
        /// </summary>
        public void StopRecording()
        {
            if (!isRecording || isStopping)
                return;

            isStopping = true;

            // 暂停状态下停止时，先累计暂停时长
            if (isPaused)
            {
                pausedDuration += DateTime.UtcNow - pauseStartedAtUtc;
                isPaused = false;
            }

            if (waveIn == null)
            {
                FinalizeRecording();
                return;
            }

            try
            {
                waveIn.StopRecording();
            }
            catch
            {
                // 某些驱动在异常状态下可能无法正常停止，直接收尾
                FinalizeRecording();
            }
        }

        /// <summary>
        /// 音频数据可用事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
        {
            // 暂停/停止期间丢弃数据，不写入文件
            if (isPaused || isStopping || !isRecording || writer == null)
                return;

            // 写入文件
            writer.Write(e.Buffer, 0, e.BytesRecorded);

            // 计算音量峰值（用于音量条显示）
            float max = 0;
            for (int i = 0; i + 1 < e.BytesRecorded; i += 2)
            {
                // 解析 16 位音频样本
                short sample = (short)((e.Buffer[i + 1] << 8) | e.Buffer[i]);
                // 计算样本的绝对值并归一化到 0-1 范围
                float val = Math.Abs(sample / 32768f);
                if (val > max) max = val;
            }

            // 更新音量（用于音量条显示），放大 5 倍并限制在 0-1 范围内
            targetValue = Math.Min(max * 5.0f, 1.0f);

            // 生成一批波形数据，避免每个采样点都触发一次 UI 更新
            int sampleCount = e.BytesRecorded / 2; // 每个样本 2 字节
            if (sampleCount > 0)
            {
                int pointCount = Math.Min(100, sampleCount);
                int step = Math.Max(1, sampleCount / pointCount);
                var points = new List<float>(pointCount + 1);

                for (int i = 0; i < sampleCount; i += step)
                {
                    int byteIndex = i * 2;
                    if (byteIndex + 1 >= e.BytesRecorded)
                        break;

                    // 解析 16 位音频样本
                    short sample = (short)((e.Buffer[byteIndex + 1] << 8) | e.Buffer[byteIndex]);
                    // 归一化到 -1.0 到 1.0 范围
                    float val = sample / 32768f;
                    // 放大波形数据，使波形更明显（放大 5 倍，与音量计算一致）
                    val = Math.Max(-1.0f, Math.Min(1.0f, val * 5.0f));
                    points.Add(val);
                }

                if (points.Count > 0)
                    WaveformDataChanged?.Invoke(this, points.ToArray());
            }

            // 触发音量事件
            VolumeChanged?.Invoke(this, targetValue);
        }

        /// <summary>
        /// 录制停止事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void WaveIn_RecordingStopped(object? sender, StoppedEventArgs e)
        {
            FinalizeRecording();
        }

        /// <summary>
        /// 释放录制资源并触发停止事件
        /// </summary>
        private void FinalizeRecording()
        {
            if (!isRecording && waveIn == null && writer == null)
                return;

            // 先阻止后续数据回调写入，再释放资源
            isStopping = true;
            isRecording = false;
            isPaused = false;

            waveIn?.Dispose();
            waveIn = null;
            writer?.Dispose();
            writer = null;

            recordingStopwatch.Reset();
            pausedDuration = TimeSpan.Zero;
            isStopping = false;

            RecordingStopped?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 释放资源。录制过程中调用会同步完成文件收尾。
        /// </summary>
        public void Dispose()
        {
            if (!isRecording && waveIn == null && writer == null)
                return;

            // 先阻止后续数据回调写入
            isStopping = true;
            isRecording = false;
            isPaused = false;

            try
            {
                if (waveIn != null)
                {
                    waveIn.DataAvailable -= WaveIn_DataAvailable;
                    waveIn.RecordingStopped -= WaveIn_RecordingStopped;
                    waveIn.StopRecording();
                }
            }
            catch
            {
                // 忽略释放阶段异常
            }

            waveIn?.Dispose();
            waveIn = null;
            writer?.Dispose();
            writer = null;

            recordingStopwatch.Reset();
            pausedDuration = TimeSpan.Zero;
            isStopping = false;
        }
    }
}
