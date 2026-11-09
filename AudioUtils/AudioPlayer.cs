using NAudio.Wave;
using System;
using System.IO;

namespace ProAudioRecorder.AudioUtils
{
    /// <summary>
    /// 播放停止原因
    /// </summary>
    public enum PlaybackStopReason
    {
        /// <summary>
        /// 音频自然播放结束
        /// </summary>
        ReachedEnd,
        /// <summary>
        /// 用户或程序主动停止
        /// </summary>
        UserStopped
    }

    /// <summary>
    /// 播放停止事件参数
    /// </summary>
    public class PlaybackStoppedEventArgs : EventArgs
    {
        /// <summary>
        /// 停止原因
        /// </summary>
        public PlaybackStopReason Reason { get; }
        /// <summary>
        /// 本次播放的编号，用于识别延迟到达的过期事件
        /// </summary>
        public int PlaybackId { get; }
        /// <summary>
        /// 播放过程中发生的异常（如果有）
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="reason">停止原因</param>
        /// <param name="playbackId">播放编号</param>
        /// <param name="exception">播放异常</param>
        public PlaybackStoppedEventArgs(PlaybackStopReason reason, int playbackId, Exception? exception = null)
        {
            Reason = reason;
            PlaybackId = playbackId;
            Exception = exception;
        }
    }

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
        private volatile bool isPlaying = false;
        /// <summary>
        /// 是否暂停
        /// </summary>
        private volatile bool isPaused = false;
        /// <summary>
        /// 当前音量（0.0-1.0）
        /// </summary>
        private float volume = 1.0f;
        /// <summary>
        /// 是否静音
        /// </summary>
        private bool isMuted = false;
        /// <summary>
        /// 播放编号，每次播放/停止/换文件都会递增，用于识别过期事件
        /// </summary>
        private int playbackId = 0;

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
        public event EventHandler<PlaybackStoppedEventArgs>? PlaybackStopped;
        /// <summary>
        /// 音量改变事件
        /// </summary>
        public event EventHandler<float>? VolumeChanged;
        /// <summary>
        /// 波形数据改变事件（每次触发发送一批采样点）
        /// </summary>
        public event EventHandler<float[]>? WaveformDataChanged;

        /// <summary>
        /// 是否正在播放
        /// </summary>
        public bool IsPlaying => isPlaying;
        /// <summary>
        /// 是否暂停
        /// </summary>
        public bool IsPaused => isPaused;
        /// <summary>
        /// 是否静音
        /// </summary>
        public bool IsMuted => isMuted;
        /// <summary>
        /// 当前播放编号
        /// </summary>
        public int PlaybackId => playbackId;
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
                    audioFile.Volume = isMuted ? 0.0f : volume;
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

            // 释放上一首的资源，但不触发 PlaybackStopped，避免误触发播放模式逻辑
            // 同时使上一首可能延迟到达的停止事件失效
            playbackId++;
            ReleaseResources(stopDevice: true);

            try
            {
                currentFilePath = filePath;
                audioFile = new AudioFileReader(filePath);
                audioFile.Volume = isMuted ? 0.0f : volume;

                // 触发时长改变事件
                DurationChanged?.Invoke(this, audioFile.TotalTime);
            }
            catch (Exception ex)
            {
                ReleaseResources(stopDevice: true);
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
                // 新的播放编号
                playbackId++;

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

                // 先更新状态，避免极短音频在 Play() 内立即结束时漏掉停止事件
                isPlaying = true;
                isPaused = false;

                // 开始播放
                waveOut.Play();

                // 如果播放已经在 Play() 内结束，不再触发开始事件
                if (!isPlaying)
                    return;

                // 触发播放开始事件
                PlaybackStarted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                ReleaseResources(stopDevice: true);
                throw new Exception("播放失败", ex);
            }
        }

        /// <summary>
        /// 暂停播放
        /// </summary>
        public void Pause()
        {
            if (!isPlaying || waveOut == null)
                return;

            if (waveOut.PlaybackState == PlaybackState.Playing)
            {
                waveOut.Pause();
                isPaused = true;
                PlaybackPaused?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 恢复播放
        /// </summary>
        public void Resume()
        {
            if (!isPlaying || waveOut == null)
                return;

            if (waveOut.PlaybackState == PlaybackState.Paused)
            {
                waveOut.Play();
                isPaused = false;
                PlaybackResumed?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 停止播放（用户主动停止）
        /// </summary>
        public void Stop()
        {
            Stop(raiseEvent: true);
        }

        /// <summary>
        /// 停止播放
        /// </summary>
        /// <param name="raiseEvent">是否触发 PlaybackStopped 事件</param>
        private void Stop(bool raiseEvent)
        {
            if (!isPlaying)
            {
                if (raiseEvent)
                {
                    // 即使播放器已经自然停止，也递增编号，
                    // 使可能还在消息队列中的“自然结束”事件失效
                    playbackId++;
                }
                return;
            }

            playbackId++;
            ReleaseResources(stopDevice: true);

            if (raiseEvent)
                PlaybackStopped?.Invoke(this, new PlaybackStoppedEventArgs(PlaybackStopReason.UserStopped, playbackId));
        }

        /// <summary>
        /// 释放播放资源
        /// </summary>
        /// <param name="stopDevice">是否主动停止输出设备（自然播放结束时设备已经停止）</param>
        private void ReleaseResources(bool stopDevice)
        {
            if (waveOut != null)
            {
                waveOut.PlaybackStopped -= WaveOut_PlaybackStopped;

                if (stopDevice)
                {
                    try
                    {
                        waveOut.Stop();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("停止播放出错: " + ex.Message);
                    }
                }

                try
                {
                    waveOut.Dispose();
                }
                catch
                {
                    // 忽略释放阶段异常
                }

                waveOut = null;
            }

            sampleProvider?.ClearBuffer();
            sampleProvider = null;

            if (audioFile != null)
            {
                try
                {
                    audioFile.Dispose();
                }
                catch
                {
                    // 忽略释放阶段异常
                }

                audioFile = null;
            }

            isPlaying = false;
            isPaused = false;
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
                PositionChanged?.Invoke(this, position);
            }
        }

        /// <summary>
        /// 静音/取消静音
        /// </summary>
        /// <param name="mute">是否静音</param>
        public void Mute(bool mute)
        {
            isMuted = mute;
            if (audioFile != null)
                audioFile.Volume = mute ? 0.0f : volume;
        }

        /// <summary>
        /// 更新播放位置
        /// </summary>
        public void UpdatePosition()
        {
            if (audioFile != null)
                PositionChanged?.Invoke(this, audioFile.CurrentTime);
        }

        /// <summary>
        /// 播放停止事件处理（自然播放结束，运行在音频线程）
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void WaveOut_PlaybackStopped(object? sender, StoppedEventArgs e)
        {
            // 忽略旧播放设备延迟送达的事件（例如用户已经切换到另一首歌）
            if (!ReferenceEquals(sender, waveOut))
                return;

            // 用户主动停止时会先取消订阅，这里只处理自然结束
            if (!isPlaying)
                return;

            // 先标记停止，避免与 UI 线程的 Stop 重复清理
            isPlaying = false;

            ReleaseResources(stopDevice: false);
            PlaybackStopped?.Invoke(this, new PlaybackStoppedEventArgs(PlaybackStopReason.ReachedEnd, playbackId, e.Exception));
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Stop(raiseEvent: false);
            ReleaseResources(stopDevice: true);
        }
    }
}
