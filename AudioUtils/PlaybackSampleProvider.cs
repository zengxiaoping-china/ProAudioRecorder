using NAudio.Wave;
using System;

namespace ProAudioRecorder.AudioUtils
{
    /// <summary>
    /// 播放采样提供者，用于处理音频播放时的采样数据，并触发音量和波形数据事件
    /// </summary>
    public class PlaybackSampleProvider : ISampleProvider
    {
        /// <summary>
        /// 源采样提供者
        /// </summary>
        private readonly ISampleProvider source;
        /// <summary>
        /// 自上次波形事件以来的采样数
        /// </summary>
        private int samplesSinceLastWaveEvent;
        /// <summary>
        /// 每个波形事件的采样数
        /// </summary>
        private const int SamplesPerWaveEvent = 1102;

        /// <summary>
        /// 音量改变事件
        /// </summary>
        public event EventHandler<float>? VolumeChanged;
        /// <summary>
        /// 波形数据改变事件
        /// </summary>
        public event EventHandler<float>? WaveformDataChanged;

        /// <summary>
        /// 波形格式
        /// </summary>
        public WaveFormat WaveFormat => source.WaveFormat;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="source">源采样提供者</param>
        public PlaybackSampleProvider(ISampleProvider source)
        {
            this.source = source;
        }

        /// <summary>
        /// 读取采样数据
        /// </summary>
        /// <param name="buffer">采样缓冲区</param>
        /// <param name="offset">偏移量</param>
        /// <param name="count">要读取的采样数</param>
        /// <returns>实际读取的采样数</returns>
        public int Read(float[] buffer, int offset, int count)
        {
            // 从源读取采样数据
            int samplesRead = source.Read(buffer, offset, count);

            if (samplesRead > 0)
            {
                // 计算音量峰值（用于音量条显示）
                float maxSample = 0;
                for (int i = 0; i < samplesRead; i++)
                {
                    float absValue = Math.Abs(buffer[offset + i]);
                    if (absValue > maxSample)
                        maxSample = absValue;
                }

                // 触发音量改变事件
                VolumeChanged?.Invoke(this, maxSample);

                // 处理波形数据
                for (int i = 0; i < samplesRead; i++)
                {
                    samplesSinceLastWaveEvent++;
                    // 每隔一定采样数触发一次波形数据事件
                    if (samplesSinceLastWaveEvent >= SamplesPerWaveEvent)
                    {
                        float val = buffer[offset + i];
                        // 放大波形数据，使波形更明显（放大 5 倍）
                        val = Math.Max(-1.0f, Math.Min(1.0f, val * 5.0f));
                        // 触发波形数据改变事件
                        WaveformDataChanged?.Invoke(this, val);
                        // 重置计数器
                        samplesSinceLastWaveEvent = 0;
                    }
                }
            }

            return samplesRead;
        }

        /// <summary>
        /// 清除缓冲区
        /// </summary>
        public void ClearBuffer()
        {
            samplesSinceLastWaveEvent = 0;
        }
    }
}
