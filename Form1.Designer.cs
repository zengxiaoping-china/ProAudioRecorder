namespace ProAudioRecorder
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            cmbDevices = new ComboBox();
            btnStartPause = new Button();
            btnStopRec = new Button();
            btnPauseRec = new Button();
            trackProgress = new TrackBar();
            btnPlay = new Button();
            btnPausePlay = new Button();
            btnStopPlay = new Button();
            lstFiles = new ListBox();
            progLevel = new ProgressBar();
            waveFormControl1 = new WaveFormControl();
            lblStatus = new Label();
            lblDuration = new Label();
            timerRecording = new System.Windows.Forms.Timer(components);
            btnDelete = new Button();
            lblTime = new Label();
            trackVolume = new TrackBar();
            chkMute = new CheckBox();
            ((System.ComponentModel.ISupportInitialize)trackProgress).BeginInit();
            ((System.ComponentModel.ISupportInitialize)trackVolume).BeginInit();
            SuspendLayout();
            // 
            // cmbDevices
            // 
            cmbDevices.FormattingEnabled = true;
            cmbDevices.Location = new Point(12, 12);
            cmbDevices.Name = "cmbDevices";
            cmbDevices.Size = new Size(399, 25);
            cmbDevices.TabIndex = 0;
            // 
            // btnStartPause
            // 
            btnStartPause.Location = new Point(12, 56);
            btnStartPause.Name = "btnStartPause";
            btnStartPause.Size = new Size(75, 23);
            btnStartPause.TabIndex = 1;
            btnStartPause.Text = "开始录音";
            btnStartPause.UseVisualStyleBackColor = true;
            // 
            // btnStopRec
            // 
            btnStopRec.Location = new Point(255, 56);
            btnStopRec.Name = "btnStopRec";
            btnStopRec.Size = new Size(75, 23);
            btnStopRec.TabIndex = 2;
            btnStopRec.Text = "停止录音";
            btnStopRec.UseVisualStyleBackColor = true;
            // 
            // btnPauseRec
            // 
            btnPauseRec.Location = new Point(336, 56);
            btnPauseRec.Name = "btnPauseRec";
            btnPauseRec.Size = new Size(75, 23);
            btnPauseRec.TabIndex = 3;
            btnPauseRec.Text = "暂停录音";
            btnPauseRec.UseVisualStyleBackColor = true;
            // 
            // trackProgress
            // 
            trackProgress.Location = new Point(12, 278);
            trackProgress.Name = "trackProgress";
            trackProgress.Size = new Size(399, 45);
            trackProgress.TabIndex = 4;
            // 
            // btnPlay
            // 
            btnPlay.Location = new Point(12, 362);
            btnPlay.Name = "btnPlay";
            btnPlay.Size = new Size(75, 23);
            btnPlay.TabIndex = 5;
            btnPlay.Text = "播放音频";
            btnPlay.UseVisualStyleBackColor = true;
            // 
            // btnPausePlay
            // 
            btnPausePlay.Location = new Point(255, 362);
            btnPausePlay.Name = "btnPausePlay";
            btnPausePlay.Size = new Size(75, 23);
            btnPausePlay.TabIndex = 6;
            btnPausePlay.Text = "暂停播放";
            btnPausePlay.UseVisualStyleBackColor = true;
            // 
            // btnStopPlay
            // 
            btnStopPlay.Location = new Point(336, 362);
            btnStopPlay.Name = "btnStopPlay";
            btnStopPlay.Size = new Size(75, 23);
            btnStopPlay.TabIndex = 7;
            btnStopPlay.Text = "停止播放";
            btnStopPlay.UseVisualStyleBackColor = true;
            // 
            // lstFiles
            // 
            lstFiles.FormattingEnabled = true;
            lstFiles.Location = new Point(442, 12);
            lstFiles.Name = "lstFiles";
            lstFiles.Size = new Size(215, 344);
            lstFiles.TabIndex = 8;
            lstFiles.MouseDoubleClick += lstFiles_MouseDoubleClick;
            // 
            // progLevel
            // 
            progLevel.Location = new Point(12, 249);
            progLevel.Maximum = 30;
            progLevel.Name = "progLevel";
            progLevel.Size = new Size(399, 23);
            progLevel.TabIndex = 9;
            // 
            // waveFormControl1
            // 
            waveFormControl1.BackColor = Color.Black;
            waveFormControl1.BackgroundColor = Color.Black;
            waveFormControl1.LineColor = Color.Lime;
            waveFormControl1.Location = new Point(12, 126);
            waveFormControl1.MaxPoints = 2000;
            waveFormControl1.Name = "waveFormControl1";
            waveFormControl1.ShowGrid = true;
            waveFormControl1.Size = new Size(399, 117);
            waveFormControl1.TabIndex = 10;
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(162, 59);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(65, 17);
            lblStatus.TabIndex = 11;
            lblStatus.Text = "就绪等待...";
            // 
            // lblDuration
            // 
            lblDuration.AutoSize = true;
            lblDuration.Location = new Point(93, 59);
            lblDuration.Name = "lblDuration";
            lblDuration.Size = new Size(63, 17);
            lblDuration.TabIndex = 12;
            lblDuration.Text = "00:00:000";
            // 
            // timerRecording
            // 
            timerRecording.Interval = 200;
            // 
            // btnDelete
            // 
            btnDelete.Location = new Point(566, 362);
            btnDelete.Name = "btnDelete";
            btnDelete.Size = new Size(91, 23);
            btnDelete.TabIndex = 13;
            btnDelete.Text = "删除选中音频";
            btnDelete.UseVisualStyleBackColor = true;
            btnDelete.Click += btnDelete_Click;
            // 
            // lblTime
            // 
            lblTime.AutoSize = true;
            lblTime.Location = new Point(12, 315);
            lblTime.Name = "lblTime";
            lblTime.Size = new Size(83, 17);
            lblTime.TabIndex = 14;
            lblTime.Text = "00:00 / 00:00";
            // 
            // trackVolume
            // 
            trackVolume.Location = new Point(320, 315);
            trackVolume.Maximum = 100;
            trackVolume.Name = "trackVolume";
            trackVolume.Size = new Size(91, 45);
            trackVolume.TabIndex = 15;
            trackVolume.Value = 100;
            // 
            // chkMute
            // 
            chkMute.AutoSize = true;
            chkMute.Location = new Point(263, 315);
            chkMute.Name = "chkMute";
            chkMute.Size = new Size(51, 21);
            chkMute.TabIndex = 16;
            chkMute.Text = "静音";
            chkMute.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(671, 405);
            Controls.Add(chkMute);
            Controls.Add(trackVolume);
            Controls.Add(lblTime);
            Controls.Add(btnDelete);
            Controls.Add(lblDuration);
            Controls.Add(lblStatus);
            Controls.Add(waveFormControl1);
            Controls.Add(progLevel);
            Controls.Add(lstFiles);
            Controls.Add(btnStopPlay);
            Controls.Add(btnPausePlay);
            Controls.Add(btnPlay);
            Controls.Add(trackProgress);
            Controls.Add(btnPauseRec);
            Controls.Add(btnStopRec);
            Controls.Add(btnStartPause);
            Controls.Add(cmbDevices);
            Name = "Form1";
            Text = "专业录音程序";
            ((System.ComponentModel.ISupportInitialize)trackProgress).EndInit();
            ((System.ComponentModel.ISupportInitialize)trackVolume).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ComboBox cmbDevices;
        private Button btnStartPause;
        private Button btnStopRec;
        private Button btnPauseRec;
        private TrackBar trackProgress;
        private Button btnPlay;
        private Button btnPausePlay;
        private Button btnStopPlay;
        private ListBox lstFiles;
        private ProgressBar progLevel;
        private WaveFormControl waveFormControl1;
        private Label lblStatus;
        private Label lblDuration;
        private System.Windows.Forms.Timer timerRecording;
        private Button btnDelete;
        private Label lblTime;
        private TrackBar trackVolume;
        private CheckBox chkMute;
    }
}
