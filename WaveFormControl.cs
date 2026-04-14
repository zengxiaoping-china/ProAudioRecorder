using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ProAudioRecorder
{
    /// <summary>
    /// 音频波形显示控件，用于实时显示录音或播放时的音频波形
    /// </summary>
    public class WaveFormControl : Control
    {
        // --- 成员变量 ---
        /// <summary>
        /// 存储音频数据点的队列
        /// </summary>
        private Queue<float> _audioPoints = new Queue<float>();
        
        /// <summary>
        /// 用于线程安全操作的锁对象
        /// </summary>
        private readonly object _lockObject = new object();

        /// <summary>
        /// 波形显示的最大点数（缓存深度）
        /// </summary>
        private int _maxPoints = 2000;

        /// <summary>
        /// 波形线条颜色
        /// </summary>
        private Color _lineColor = Color.Lime;
        
        /// <summary>
        /// 背景颜色
        /// </summary>
        private Color _backgroundColor = Color.Black;
        
        /// <summary>
        /// 是否显示网格
        /// </summary>
        private bool _showGrid = true;
        
        /// <summary>
        /// 控制是否绘制波形
        /// </summary>
        private bool _isDrawing = false;

        // --- 属性 (用于设计器和外部访问) ---

        /// <summary>
        /// 获取或设置波形显示的最大点数（缓存深度）
        /// </summary>
        [Browsable(true), EditorBrowsable(EditorBrowsableState.Always)]
        [Category("Behavior"), Description("波形显示的最大点数（缓存深度）")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public int MaxPoints
        {
            get { return _maxPoints; }
            set { _maxPoints = value; }
        }

        /// <summary>
        /// 获取或设置波形线条颜色
        /// </summary>
        [Category("Appearance"), Description("波形线条颜色")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color LineColor
        {
            get { return _lineColor; }
            set { _lineColor = value; this.Invalidate(); }
        }

        /// <summary>
        /// 获取或设置背景颜色
        /// </summary>
        [Category("Appearance"), Description("背景颜色")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color BackgroundColor
        {
            get { return _backgroundColor; }
            set { _backgroundColor = value; this.Invalidate(); }
        }

        /// <summary>
        /// 获取或设置是否显示网格
        /// </summary>
        [Category("Appearance"), Description("是否显示网格")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool ShowGrid
        {
            get { return _showGrid; }
            set { _showGrid = value; this.Invalidate(); }
        }

        // --- 构造函数 ---
        /// <summary>
        /// 初始化 WaveFormControl 类的新实例
        /// </summary>
        public WaveFormControl()
        {
            // 开启双缓冲，防止绘图闪烁
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.DoubleBuffer |
                          ControlStyles.ResizeRedraw, true);
        }

        // --- 核心方法 ---

        /// <summary>
        /// 清空波形数据并停止绘制
        /// </summary>
        public void Clear()
        {
            lock (_lockObject)
            {
                _audioPoints.Clear();
                _isDrawing = false; // 停止绘制
            }
            this.Invalidate();
        }

        /// <summary>
        /// 接收录音数据并处理
        /// </summary>
        /// <param name="buffer">音频数据缓冲区</param>
        /// <param name="bytesRecorded">记录的字节数</param>
        public void AddAudioData(byte[] buffer, int bytesRecorded)
        {
            // 简单处理：取这一段数据的最大绝对值作为波形高度
            int samples = bytesRecorded / 2;
            float maxVal = 0;

            for (int i = 0; i < samples; i++)
            {
                // 16位 PCM 数据拼接
                short sample = (short)(buffer[i * 2] | (buffer[i * 2 + 1] << 8));
                float normalized = sample / 32768f;
                if (Math.Abs(normalized) > Math.Abs(maxVal))
                {
                    maxVal = normalized;
                }
            }

            AddValue(maxVal);
        }

        /// <summary>
        /// 添加单个浮点值到波形数据中
        /// </summary>
        /// <param name="value">音频数据值（范围：-1.0 到 1.0）</param>
        public void AddValue(float value)
        {
            lock (_lockObject)
            {
                // 1. 把新数据加进来
                _audioPoints.Enqueue(value);
                
                // 2. 开始绘制
                _isDrawing = true;

                // 3.如果数据量超过了 MaxPoints，必须把最早的数据踢出去
                while (_audioPoints.Count > _maxPoints)
                {
                    _audioPoints.Dequeue();
                }

                // 4. 触发重绘
                this.Invalidate();
            }
        }

        // --- 绘图逻辑 ---
        /// <summary>
        /// 重写 OnPaint 方法，实现波形绘制
        /// </summary>
        /// <param name="e">绘图事件参数</param>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // 1. 填充背景
            using (SolidBrush backBrush = new SolidBrush(_backgroundColor))
            {
                e.Graphics.FillRectangle(backBrush, this.ClientRectangle);
            }

            int yCenter = this.Height / 2;

            // 2. 绘制网格（无论有没有波形，都要画网格）
            if (_showGrid)
            {
                using (Pen gridPen = new Pen(Color.FromArgb(50, Color.Gray)))
                {
                    // 垂直网格
                    for (int i = 0; i < this.Width; i += 20)
                    {
                        e.Graphics.DrawLine(gridPen, i, 0, i, this.Height);
                    }
                    // 水平网格
                    for (int i = 0; i < this.Height; i += 20)
                    {
                        e.Graphics.DrawLine(gridPen, 0, i, this.Width, i);
                    }
                }
            }

            // 3. 绘制波形（只有在有数据且允许绘制时才画）
            if (_audioPoints.Count > 1 && _isDrawing)
            {
                using (Pen linePen = new Pen(_lineColor, 1))
                {
                    lock (_lockObject)
                    {
                        // 计算每个像素对应的点数
                        float pointsPerPx = (float)_audioPoints.Count / this.Width;
                        int prevX = 0;
                        int prevY = yCenter;

                        int index = 0;
                        foreach (float val in _audioPoints)
                        {
                            int x = (int)(index / pointsPerPx);
                            // 限制 Y 轴范围，调整波形位置使其更居中
                            float normalizedVal = Math.Max(-1.0f, Math.Min(1.0f, val));
                            // 原始音频波形数据范围是 -1.0 到 1.0，可以在中间线上下对称波动
                            // 提升振幅，使波形更明显
                            int y = yCenter - (int)(normalizedVal * (this.Height / 2 * 1.0));

                            // 确保 Y 值在有效范围内
                            if (y < 0) y = 0;
                            if (y > this.Height) y = this.Height;

                            // 绘制线段
                            if (index > 0)
                            {
                                e.Graphics.DrawLine(linePen, prevX, prevY, x, y);
                            }
                            
                            // 更新前一个点的坐标
                            prevX = x;
                            prevY = y;
                            index++;
                        }
                    }
                }
            }
        }
    }
}