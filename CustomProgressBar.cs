using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ProAudioRecorder
{
    public class CustomProgressBar : ProgressBar
    {
        private Color progressBarColor = Color.Lime;
        private Color borderColor = Color.Gray;

        [Category("Appearance")]
        [Description("The color of the progress bar")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color ProgressBarColor
        {
            get { return progressBarColor; }
            set
            {
                progressBarColor = value;
                this.Invalidate();
            }
        }

        [Category("Appearance")]
        [Description("The color of the border")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color BorderColor
        {
            get { return borderColor; }
            set
            {
                borderColor = value;
                this.Invalidate();
            }
        }

        public CustomProgressBar()
        {
            this.SetStyle(ControlStyles.UserPaint, true);
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Rectangle rec = new Rectangle(0, 0, this.Width, this.Height);
            
            // 绘制背景
            using (SolidBrush backgroundBrush = new SolidBrush(this.BackColor))
            {
                e.Graphics.FillRectangle(backgroundBrush, rec);
            }

            // 绘制进度条
            double scaleFactor = (((double)this.Value - this.Minimum) / ((double)this.Maximum - this.Minimum));
            int width = (int)((double)rec.Width * scaleFactor);
            
            if (width > 0)
            {
                Rectangle progressRec = new Rectangle(0, 0, width, rec.Height);
                using (SolidBrush progressBrush = new SolidBrush(progressBarColor))
                {
                    e.Graphics.FillRectangle(progressBrush, progressRec);
                }
            }

            // 绘制边框
            using (Pen borderPen = new Pen(borderColor, 1))
            {
                e.Graphics.DrawRectangle(borderPen, new Rectangle(0, 0, this.Width - 1, this.Height - 1));
            }
        }
    }
}
