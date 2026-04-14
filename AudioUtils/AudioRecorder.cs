using NAudio.Wave;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ProAudioRecorder.AudioUtils
{
    /// <summary>
    /// 音频录制器类，负责音频的录制、暂停、恢复和停止等操作
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
        private bool isRecording = false;
        /// <summary>
        /// 是否暂停
        /// </summary>
        private bool isPaused = false;
        /// <summary>
        /// 录制开始时间
        /// </summary>
        private DateTime recordingStartTime;
        /// <summary>
        /// 总暂停时长
        /// </summary>
        private TimeSpan totalPausedDuration = TimeSpan.Zero;
        /// <summary>
        /// 目标音量值（用于音量条显示）
        /// </summary>
        private float targetValue = 0;

        /// <summary>
        /// 音量改变事件
        /// </summary>
        public event EventHandler<float>? VolumeChanged;
        /// <summary>
        /// 波形数据改变事件
        /// </summary>
        public event EventHandler<float>? WaveformDataChanged; // 新增：波形数据事件
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
        /// 当前录制时长
        /// </summary>
        public TimeSpan CurrentDuration
        {
            get
            {
                if (!isRecording && !isPaused)
                    return TimeSpan.Zero;

                if (isPaused)
                    return totalPausedDuration;

                TimeSpan currentSegmentDuration = DateTime.Now - recordingStartTime;
                return totalPausedDuration + currentSegmentDuration;
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
                // 准备文件路径
                if (!Directory.Exists(outputDirectory))
                    Directory.CreateDirectory(outputDirectory);

                string fileName = $"{DateTime.Now:yyyy-MM-dd-HHmmss}.wav";
                currentFilePath = Path.Combine(outputDirectory, fileName);
                if (File.Exists(currentFilePath))
                    File.Delete(currentFilePath);

                // 初始化录音设备
                waveIn = new WaveInEvent();
                waveIn.DeviceNumber = deviceNumber;
                waveIn.WaveFormat = new WaveFormat(44100, 16, 1);
                waveIn.DataAvailable += WaveIn_DataAvailable;
                waveIn.RecordingStopped += WaveIn_RecordingStopped;

                // 准备写入文件
                writer = new WaveFileWriter(currentFilePath, waveIn.WaveFormat);

                // 开始录音
                waveIn.StartRecording();
                recordingStartTime = DateTime.Now;
                isRecording = true;
                isPaused = false;

                // 触发录制开始事件
                RecordingStarted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                throw new Exception("录音启动失败", ex);
            }
        }

        /// <summary>
        /// 暂停录制
        /// </summary>
        public void PauseRecording()
        {
            if (!isRecording || isPaused)
                return;

            isPaused = true;
            waveIn?.StopRecording();
            
            // 不释放 WaveFileWriter，避免文件被占用
            // 继续录音时会继续使用这些资源
            
            // 触发录制暂停事件
            RecordingPaused?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 恢复录制
        /// </summary>
        public void ResumeRecording()
        {
            if (!isPaused)
                return;

            // 重新创建 WaveInEvent
            waveIn = new WaveInEvent();
            waveIn.DeviceNumber = 0; // 使用默认设备
            waveIn.WaveFormat = new WaveFormat(44100, 16, 1);
            waveIn.DataAvailable += WaveIn_DataAvailable;
            waveIn.RecordingStopped += WaveIn_RecordingStopped;

            waveIn.StartRecording();
            recordingStartTime = DateTime.Now;
            isRecording = true;
            isPaused = false;

            // 触发录制恢复事件
            RecordingResumed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 停止录制
        /// </summary>
        public void StopRecording()
        {
            if (!isRecording && !isPaused)
                return;

            isPaused = false;
            waveIn?.StopRecording();
        }

        /// <summary>
        /// 音频数据可用事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
        {
            // 写入文件
            writer?.Write(e.Buffer, 0, e.BytesRecorded);

            // 计算音量峰值（用于音量条显示）
            float max = 0;
            for (int i = 0; i < e.BytesRecorded; i += 2)
            {
                // 解析 16 位音频样本
                short sample = (short)((e.Buffer[i + 1] << 8) | e.Buffer[i]);
                // 计算样本的绝对值并归一化到 0-1 范围
                float val = Math.Abs(sample / 32768f);
                if (val > max) max = val;
            }

            // 更新音量（用于音量条显示），放大 5 倍并限制在 0-1 范围内
            targetValue = Math.Min(max * 5.0f, 1.0f);
            
            // 传递原始音频波形数据（用于波形显示）
            // 降采样：只取部分样本，避免数据量过大
            int sampleCount = e.BytesRecorded / 2; // 每个样本 2 字节
            int step = Math.Max(1, sampleCount / 100); // 每次只取 100 个样本，保证波形流畅度
            
            for (int i = 0; i < sampleCount; i += step)
            {
                // 解析 16 位音频样本
                short sample = (short)((e.Buffer[i * 2 + 1] << 8) | e.Buffer[i * 2]);
                // 归一化到 -1.0 到 1.0 范围
                float val = sample / 32768f; 
                
                // 放大波形数据，使波形更明显（放大 5 倍，与音量计算一致）
                val = Math.Max(-1.0f, Math.Min(1.0f, val * 5.0f));
                
                // 触发波形数据事件
                WaveformDataChanged?.Invoke(this, val);
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
            if (isPaused)
            {
                // 计算暂停前的总时长并保存
                TimeSpan lastSegment = DateTime.Now - recordingStartTime;
                totalPausedDuration += lastSegment;
                
                // 暂停时不释放 writer，因为继续录音时还需要使用
                waveIn?.Dispose();
                waveIn = null;
                return;
            }

            // 停止录音时释放所有资源
            waveIn?.Dispose();
            waveIn = null;
            writer?.Dispose();
            writer = null;

            // 重置状态
            isRecording = false;
            isPaused = false;
            totalPausedDuration = TimeSpan.Zero;

            // 触发录制停止事件
            RecordingStopped?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            // 停止录制
            StopRecording();
            // 释放资源
            waveIn?.Dispose();
            writer?.Dispose();
        }
    }
}