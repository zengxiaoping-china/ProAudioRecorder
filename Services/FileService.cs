using ProAudioRecorder.AudioUtils;
using System;
using System.IO;

namespace ProAudioRecorder.Services
{
    /// <summary>
    /// 文件服务类，封装文件管理相关的功能
    /// </summary>
    public class FileService
    {
        /// <summary>
        /// 文件管理器
        /// </summary>
        private readonly FileManager _fileManager;
        /// <summary>
        /// 录音文件夹路径
        /// </summary>
        private readonly string _recordingsFolder;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="recordingsFolder">录音文件夹路径</param>
        public FileService(string recordingsFolder)
        {
            _recordingsFolder = recordingsFolder;
            _fileManager = new FileManager(recordingsFolder);
        }

        /// <summary>
        /// 录音文件夹路径
        /// </summary>
        public string RecordingsFolder => _recordingsFolder;

        /// <summary>
        /// 文件管理器
        /// </summary>
        public FileManager FileManager => _fileManager;

        /// <summary>
        /// 从显示文本中提取文件名
        /// </summary>
        /// <param name="displayText">显示文本，格式为 "文件名 [时长]"
        /// <returns>提取的文件名</returns>
        public string ExtractFileNameFromDisplayText(string displayText)
        {
            if (string.IsNullOrEmpty(displayText))
                return string.Empty;

            if (displayText.Contains(" ["))
            {
                return displayText.Substring(0, displayText.IndexOf(" ["));
            }

            return displayText;
        }

        /// <summary>
        /// 获取文件的完整路径
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <returns>文件的完整路径</returns>
        public string GetFullPath(string fileName)
        {
            return Path.Combine(_recordingsFolder, fileName);
        }

        /// <summary>
        /// 检查文件是否存在
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>文件是否存在</returns>
        public bool FileExists(string filePath)
        {
            return File.Exists(filePath);
        }

        /// <summary>
        /// 删除文件
        /// </summary>
        /// <param name="fileName">文件名</param>
        public void DeleteFile(string fileName)
        {
            _fileManager.DeleteFile(fileName);
        }
    }
}
