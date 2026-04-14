using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ProAudioRecorder.AudioUtils
{
    /// <summary>
    /// 文件管理器，负责管理录音文件的存储、获取和删除
    /// </summary>
    public class FileManager
    {
        /// <summary>
        /// 录音文件存储目录
        /// </summary>
        private string _recordingsFolder;
        /// <summary>
        /// 文件时长缓存，避免重复读取文件
        /// </summary>
        private Dictionary<string, TimeSpan> _fileDurationsCache = new Dictionary<string, TimeSpan>();

        /// <summary>
        /// 录音文件存储目录
        /// </summary>
        public string RecordingsFolder => _recordingsFolder;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="recordingsFolder">录音文件存储目录</param>
        public FileManager(string recordingsFolder)
        {
            _recordingsFolder = recordingsFolder;
            EnsureDirectoryExists();
        }

        /// <summary>
        /// 确保录音目录存在，如果不存在则创建
        /// </summary>
        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_recordingsFolder))
            {
                Directory.CreateDirectory(_recordingsFolder);
            }
        }

        /// <summary>
        /// 获取所有录音文件
        /// </summary>
        /// <returns>录音文件信息列表</returns>
        public List<AudioFileInfo> GetRecordingFiles()
        {
            EnsureDirectoryExists();

            // 获取所有 .wav 文件
            string[] files = Directory.GetFiles(_recordingsFolder, "*.wav");
            // 按文件路径降序排序（最新的文件在前）
            var sortedFiles = files.OrderByDescending(f => f).ToArray();

            var fileInfos = new List<AudioFileInfo>();

            // 遍历所有文件，创建 AudioFileInfo 对象
            foreach (string file in sortedFiles)
            {
                string fileName = Path.GetFileName(file);
                TimeSpan duration = GetFileDuration(file);
                fileInfos.Add(new AudioFileInfo(file, fileName, duration));
            }

            return fileInfos;
        }

        /// <summary>
        /// 获取文件时长
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>文件时长</returns>
        private TimeSpan GetFileDuration(string filePath)
        {
            string fileName = Path.GetFileName(filePath);

            // 检查缓存
            if (_fileDurationsCache.TryGetValue(fileName, out TimeSpan cachedDuration))
            {
                return cachedDuration;
            }

            try
            {
                // 使用 AudioFileReader 读取文件时长
                using (var audioFile = new AudioFileReader(filePath))
                {
                    TimeSpan duration = audioFile.TotalTime;
                    // 缓存时长
                    _fileDurationsCache[fileName] = duration;
                    return duration;
                }
            }
            catch
            {
                // 如果读取失败，返回0
                return TimeSpan.Zero;
            }
        }

        /// <summary>
        /// 删除文件
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <exception cref="Exception">删除文件失败时抛出</exception>
        public void DeleteFile(string fileName)
        {
            string filePath = Path.Combine(_recordingsFolder, fileName);

            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                    // 从缓存中移除
                    _fileDurationsCache.Remove(fileName);
                    // 不再自动触发事件，由调用方手动处理
                    // FilesUpdated?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    throw new Exception("删除文件失败", ex);
                }
            }
        }

        /// <summary>
        /// 清除缓存
        /// </summary>
        public void ClearCache()
        {
            _fileDurationsCache.Clear();
        }
    }

    /// <summary>
    /// 音频文件信息类，存储音频文件的基本信息
    /// </summary>
    public class AudioFileInfo
    {
        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath { get; }
        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName { get; }
        /// <summary>
        /// 文件时长
        /// </summary>
        public TimeSpan Duration { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="fileName">文件名</param>
        /// <param name="duration">文件时长</param>
        public AudioFileInfo(string filePath, string fileName, TimeSpan duration)
        {
            FilePath = filePath;
            FileName = fileName;
            Duration = duration;
        }

        /// <summary>
        /// 重写 ToString 方法，返回带有时长的文件名
        /// </summary>
        /// <returns>带有时长的文件名</returns>
        public override string ToString()
        {
            return $"{FileName} [{Duration.ToString(@"mmss")}]";
        }
    }
}