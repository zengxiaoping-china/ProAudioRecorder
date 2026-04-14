using NAudio.Wave;
using System.Collections.Generic;

namespace ProAudioRecorder.AudioUtils
{
    /// <summary>
    /// 音频设备管理器，用于获取系统中的录音和播放设备
    /// </summary>
    public class AudioDeviceManager
    {
        /// <summary>
        /// 获取所有录音设备
        /// </summary>
        /// <returns>录音设备列表</returns>
        public List<AudioDeviceInfo> GetRecordingDevices()
        {
            var devices = new List<AudioDeviceInfo>();

            // 遍历所有录音设备
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                // 获取设备 capabilities
                var caps = WaveIn.GetCapabilities(i);
                // 创建设备信息对象并添加到列表
                devices.Add(new AudioDeviceInfo(i, caps.ProductName, caps.Channels));
            }

            return devices;
        }

        /// <summary>
        /// 获取所有播放设备
        /// </summary>
        /// <returns>播放设备列表</returns>
        public List<AudioDeviceInfo> GetPlaybackDevices()
        {
            var devices = new List<AudioDeviceInfo>();

            // 遍历所有播放设备
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                // 获取设备 capabilities
                var caps = WaveOut.GetCapabilities(i);
                // 创建设备信息对象并添加到列表
                devices.Add(new AudioDeviceInfo(i, caps.ProductName, caps.Channels));
            }

            return devices;
        }
    }

    /// <summary>
    /// 音频设备信息类，存储设备的基本信息
    /// </summary>
    public class AudioDeviceInfo
    {
        /// <summary>
        /// 设备 ID
        /// </summary>
        public int DeviceId { get; }
        /// <summary>
        /// 设备名称
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// 设备通道数
        /// </summary>
        public int Channels { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="deviceId">设备 ID</param>
        /// <param name="name">设备名称</param>
        /// <param name="channels">设备通道数</param>
        public AudioDeviceInfo(int deviceId, string name, int channels)
        {
            DeviceId = deviceId;
            Name = name;
            Channels = channels;
        }

        /// <summary>
        /// 重写 ToString 方法，返回设备名称
        /// </summary>
        /// <returns>设备名称</returns>
        public override string ToString()
        {
            return Name;
        }
    }
}