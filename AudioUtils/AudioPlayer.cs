using NAudio.Wave;
using System;
using System.IO;

namespace ProAudioRecorder.AudioUtils
{
    /// <summary>
    /// 音频播放器类，负责音频文件的加载、播放、暂停、停止等操作
    /// </summary>
    public class AudioPlayer : IDisposable
    {
        /// <summary>
        /// 音频输出设备
        /// </summary>
        private WaveOutEvent? waveOut;
        /// <summary>
        /// 音频文件读取器
        /// </summary>
        private AudioFileReader? audioFile;
        /// <summary>
        /// 播放采样提供者，用于处理音频数据和生成波形
        /// </summary>
        private PlaybackSampleProvider? sampleProvider;
        /// <summary>
        /// 当前播放的文件路径
        /// </summary>
        private string currentFilePath = "";
        /// <summary>
        /// 是否正在播放
        /// </summary>
        private bool isPlaying = false;
        /// <summary>
        /// 是否暂停
        /// </summary>
        private bool isPaused = false;
        /// <summary>
        /// 当前音量
        /// </summary>
        private float volume = 1.0f;
        /// <summary>
        /// 静音前的音量
        /// </summary>
        private float previousVolume = 1.0f;

        /// <summary>
        /// 播放位置改变事件
        /// </summary>
        public event EventHandler<TimeSpan>? PositionChanged;
        /// <summary>
        /// 音频时长改变事件
        /// </summary>
        public event EventHandler<TimeSpan>? DurationChanged;
        /// <summary>
        /// 播放开始事件
        /// </summary>
        public event EventHandler? PlaybackStarted;
        /// <summary>
        /// 播放暂停事件
        /// </summary>
        public event EventHandler? PlaybackPaused;
        /// <summary>
        /// 播放恢复事件
        /// </summary>
        public event EventHandler? PlaybackResumed;
        /// <summary>
        /// 播放停止事件
        /// </summary>
        public event EventHandler? PlaybackStopped;
        /// <summary>
        /// 音量改变事件
        /// </summary>
        public event EventHandler<float>? VolumeChanged;
        /// <summary>
        /// 波形数据改变事件
        /// </summary>
        public event EventHandler<float>? WaveformDataChanged;

        /// <summary>
        /// 是否正在播放
        /// </summary>
        public bool IsPlaying => isPlaying;
        /// <summary>
        /// 是否暂停
        /// </summary>
        public bool IsPaused => isPaused;
        /// <summary>
        /// 当前播放的文件路径
        /// </summary>
        public string CurrentFilePath => currentFilePath;
        /// <summary>
        /// 当前播放位置
        /// </summary>
        public TimeSpan CurrentPosition => audioFile?.CurrentTime ?? TimeSpan.Zero;
        /// <summary>
        /// 音频总时长
        /// </summary>
        public TimeSpan TotalDuration => audioFile?.TotalTime ?? TimeSpan.Zero;
        /// <summary>
        /// 音量（0.0-1.0）
        /// </summary>
        public float Volume
        {
            get => volume;
            set
            {
                // 限制音量范围在 0.0 到 1.0 之间
                volume = Math.Max(0.0f, Math.Min(1.0f, value));
                if (audioFile != null)
                    audioFile.Volume = volume;
            }
        }

        /// <summary>
        /// 加载音频文件
        /// </summary>
        /// <param name="filePath">音频文件路径</param>
        /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
        /// <exception cref="Exception">加载失败时抛出</exception>
        public void LoadFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("音频文件不存在", filePath);

            // 停止当前播放
            Stop();

            try
            {
                currentFilePath = filePath;
                audioFile = new AudioFileReader(filePath);
                audioFile.Volume = volume;
                // 触发时长改变事件
                DurationChanged?.Invoke(this, audioFile.TotalTime);
            }
            catch (Exception ex)
            {
                throw new Exception("加载音频文件失败", ex);
            }
        }

        /// <summary>
        /// 开始播放
        /// </summary>
        /// <exception cref="Exception">播放失败时抛出</exception>
        public void Play()
        {
            // 如果已经在播放或没有加载音频文件，则直接返回
            if (isPlaying || audioFile == null)
                return;

            try
            {
                // 创建播放采样提供者
                sampleProvider = new PlaybackSampleProvider(audioFile);
                // 订阅音量和波形数据改变事件
                sampleProvider.VolumeChanged += (s, v) => VolumeChanged?.Invoke(this, v);
                sampleProvider.WaveformDataChanged += (s, data) => WaveformDataChanged?.Invoke(this, data);

                // 初始化音频输出设备
                waveOut = new WaveOutEvent();
                waveOut.Init(sampleProvider);
                // 订阅播放停止事件
                waveOut.PlaybackStopped += WaveOut_PlaybackStopped;
                // 开始播放
                waveOut.Play();
                // 更新状态
                isPlaying = true;
                isPaused = false;
                // 触发播放开始事件
                PlaybackStarted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                throw new Exception("播放失败", ex);
            }
        }

        /// <summary>
        /// 暂停播放
        /// </summary>
        public void Pause()
        {
            // 如果不在播放或没有音频输出设备，则直接返回
            if (!isPlaying || waveOut == null)
                return;

            // 如果当前正在播放，则暂停
            if (waveOut.PlaybackState == PlaybackState.Playing)
            {
                waveOut.Pause();
                isPaused = true;
                // 触发播放暂停事件
                PlaybackPaused?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 恢复播放
        /// </summary>
        public void Resume()
        {
            // 如果不在播放或没有音频输出设备，则直接返回
            if (!isPlaying || waveOut == null)
                return;

            // 如果当前处于暂停状态，则恢复播放
            if (waveOut.PlaybackState == PlaybackState.Paused)
            {
                waveOut.Play();
                isPaused = false;
                // 触发播放恢复事件
                PlaybackResumed?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 停止播放
        /// </summary>
        public void Stop()
        {
            // 如果不在播放，则直接返回
            if (!isPlaying)
                return;

            // 清理音频输出设备
            if (waveOut != null)
            {
                // 取消订阅播放停止事件
                waveOut.PlaybackStopped -= WaveOut_PlaybackStopped;
                try
                {
                    waveOut.Stop();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("停止播放出错: " + ex.Message);
                }
                // 释放资源
                waveOut.Dispose();
                waveOut = null;
            }

            // 清理采样提供者
            if (sampleProvider != null)
            {
                sampleProvider.ClearBuffer();
                sampleProvider = null;
            }

            // 清理音频文件读取器
            if (audioFile != null)
            {
                try
                { 
                    audioFile.Dispose(); 
                }
                catch { }
                audioFile = null;
            }

            // 更新状态
            isPlaying = false;
            isPaused = false;
            // 触发播放停止事件
            PlaybackStopped?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 跳转到指定位置
        /// </summary>
        /// <param name="position">目标位置</param>
        public void Seek(TimeSpan position)
        {
            if (audioFile != null)
            {
                audioFile.CurrentTime = position;
                // 触发位置改变事件
                PositionChanged?.Invoke(this, position);
            }
        }

        /// <summary>
        /// 静音/取消静音
        /// </summary>
        /// <param name="mute">是否静音</param>
        public void Mute(bool mute)
        {
            if (audioFile != null)
            {
                if (mute)
                {
                    // 保存当前音量
                    previousVolume = audioFile.Volume;
                    // 设置音量为 0
                    audioFile.Volume = 0.0f;
                }
                else
                {
                    // 恢复之前的音量
                    audioFile.Volume = previousVolume;
                }
            }
        }

        /// <summary>
        /// 更新播放位置
        /// </summary>
        public void UpdatePosition()
        {
            if (audioFile != null)
            {
                // 触发位置改变事件
                PositionChanged?.Invoke(this, audioFile.CurrentTime);
            }
        }

        /// <summary>
        /// 播放停止事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void WaveOut_PlaybackStopped(object? sender, StoppedEventArgs e)
        {
            // 调用停止方法
            Stop();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            // 停止播放
            Stop();
            // 释放资源
            waveOut?.Dispose();
            audioFile?.Dispose();
        }
    }
}