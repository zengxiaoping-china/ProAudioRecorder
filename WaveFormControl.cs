using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ProAudioRecorder
{
    public class WaveFormControl : Control
    {
        // --- 成员变量 ---
        private Queue<float> _audioPoints = new Queue<float>();
        private readonly object _lockObject = new object();

        // 使用 _maxPoints 作为私有变量
        private int _maxPoints = 2000;

        private Color _lineColor = Color.Lime;
        private Color _backgroundColor = Color.Black;
        private bool _showGrid = true;

        // --- 属性 (用于设计器和外部访问) ---

        [Browsable(true), EditorBrowsable(EditorBrowsableState.Always)]
        [Category("Behavior"), Description("波形显示的最大点数（缓存深度）")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public int MaxPoints
        {
            get { return _maxPoints; }
            set { _maxPoints = value; }
        }

        [Category("Appearance"), Description("波形线条颜色")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color LineColor
        {
            get { return _lineColor; }
            set { _lineColor = value; this.Invalidate(); }
        }

        [Category("Appearance"), Description("背景颜色")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color BackgroundColor
        {
            get { return _backgroundColor; }
            set { _backgroundColor = value; this.Invalidate(); }
        }

        [Category("Appearance"), Description("是否显示网格")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool ShowGrid
        {
            get { return _showGrid; }
            set { _showGrid = value; this.Invalidate(); }
        }

        // --- 构造函数 ---
        public WaveFormControl()
        {
            // 开启双缓冲，防止绘图闪烁
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.DoubleBuffer |
                          ControlStyles.ResizeRedraw, true);
        }

        // --- 核心方法 ---

        // 清空波形
        public void Clear()
        {
            lock (_lockObject)
            {
                _audioPoints.Clear();
            }
            this.Invalidate();
        }

        // 接收录音数据 (byte[])
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

        // 添加单个浮点值
        public void AddValue(float value)
        {
            lock (_lockObject)
            {
                // 1. 把新数据加进来
                _audioPoints.Enqueue(value);

                // 2.如果数据量超过了 MaxPoints，必须把最早的数据踢出去
                while (_audioPoints.Count > _maxPoints)
                {
                    _audioPoints.Dequeue();
                }

                // 3. 触发重绘
                this.Invalidate();
            }
        }

        // --- 绘图逻辑 ---
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

            // 3. 绘制波形（只有有数据时才画）
            if (_audioPoints.Count > 1)
            {
                using (Pen linePen = new Pen(_lineColor, 1))
                {
                    lock (_lockObject)
                    {
                        float pointsPerPx = (float)_audioPoints.Count / this.Width;
                        int prevX = 0;
                        int prevY = yCenter;

                        int index = 0;
                        foreach (float val in _audioPoints)
                        {
                            int x = (int)(index / pointsPerPx);
                            // 限制 Y 轴范围
                            int y = yCenter - (int)(val * (this.Height / 2 * 0.9) * 2.0f);

                            if (y < 0) y = 0;
                            if (y > this.Height) y = this.Height;

                            if (index > 0)
                            {
                                e.Graphics.DrawLine(linePen, prevX, prevY, x, y);
                            }
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