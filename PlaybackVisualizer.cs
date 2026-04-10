using NAudio.Wave;
using ProAudioRecorder;
using System;

public class PlaybackVisualizer : IWaveProvider
{
    private readonly IWaveProvider _sourceProvider;
    private readonly WaveFormControl _waveFormControl;

    // ★★★ 新增：蓄水池，用来积攒数据 ★★★
    private float[] _accumulatedSamples = new float[1024]; // 目标：凑够1024个采样点画一次
    private int _accumulatedCount = 0;

    public PlaybackVisualizer(IWaveProvider sourceProvider, WaveFormControl control)
    {
        _sourceProvider = sourceProvider;
        _waveFormControl = control;
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        // 1. 从音源读取数据
        int bytesRead = _sourceProvider.Read(buffer, offset, count);

        if (bytesRead > 0 && _waveFormControl != null)
        {
            // ★★★ 2. 处理数据：注意 offset！ ★★★
            // 数据是从 buffer[offset] 开始的，长度是 bytesRead
            AnalyzeAndSendData(buffer, offset, bytesRead);
        }

        return bytesRead;
    }

    private void AnalyzeAndSendData(byte[] dataBuffer, int offset, int bytesToProcess)
    {
        // 计算这一小块数据的采样点数
        int samplesInThisChunk = bytesToProcess / 2; // 16bit = 2 bytes

        for (int i = 0; i < samplesInThisChunk; i++)
        {
            // ★★★ 关键：读取数据时要加上 offset * 2 ★★★
            // 因为 dataBuffer 是 byte 数组，而样本是 short (2字节)
            int byteIndex = offset + (i * 2);

            short sample = (short)(dataBuffer[byteIndex] | (dataBuffer[byteIndex + 1] << 8));
            float normalized = sample / 32768f;

            // 放入蓄水池
            _accumulatedSamples[_accumulatedCount] = normalized;
            _accumulatedCount++;

            // ★★★ 3. 如果蓄水池满了，就计算峰值并发送 ★★★
            if (_accumulatedCount >= _accumulatedSamples.Length)
            {
                // 计算这一批数据的最大值
                float maxVal = 0;
                for (int j = 0; j < _accumulatedCount; j++)
                {
                    if (Math.Abs(_accumulatedSamples[j]) > Math.Abs(maxVal))
                    {
                        maxVal = _accumulatedSamples[j];
                    }
                }

                // 构造一个假的 byte[] 传给控件（兼容你现有的 AddAudioData）
                // 其实可以直接改控件加个 AddValue(float)，但这样改动最小
                // 这里我们直接调用 AddValue，因为你的控件里 AddAudioData 也是转成 float 的
                _waveFormControl.AddValue(maxVal);

                // 清空蓄水池
                _accumulatedCount = 0;
            }
        }
    }

    public WaveFormat WaveFormat => _sourceProvider.WaveFormat;
}