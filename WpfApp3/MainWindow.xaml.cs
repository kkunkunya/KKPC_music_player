using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.IO;
using System.Linq;
using Forms = System.Windows.Forms;

namespace WpfApp3
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MediaPlayer mediaPlayer;
        private DispatcherTimer timer;
        private bool isPlaying = false;
        private System.Windows.Point _popupOffset;
        private bool _isDragging = false;
        private double _lastHorizontalOffset = 0;
        private double _lastVerticalOffset = 0;
        private bool _hasCustomPosition = false;

        public MainWindow()
        {
            InitializeComponent();
            InitializePlayer();
            InitializePlaylistPopup();
        }

        private void InitializePlayer()
        {
            mediaPlayer = new MediaPlayer();
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;

            mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
        }

        private void InitializePlaylistPopup()
        {
            PlaylistPopup.Placement = PlacementMode.Relative;
            _hasCustomPosition = false;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (mediaPlayer.Source != null && mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                ProgressSlider.Value = (mediaPlayer.Position.TotalSeconds / mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds) * 100;
            }
        }

        private void MediaPlayer_MediaEnded(object sender, EventArgs e)
        {
            // 当前歌曲播放结束时，自动播放下一首
            NextButton_Click(sender, new RoutedEventArgs());
            
            // 如果没有下一首歌曲可播放，则停止播放
            if (!isPlaying)
            {
                mediaPlayer.Stop();
                ProgressSlider.Value = 0;
                PlayIcon.Text = "\uE768"; // 播放图标
            }
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            if (!PlaylistPopup.IsOpen)
            {
                if (!_hasCustomPosition)
                {
                    _lastHorizontalOffset = this.ActualWidth;
                    _lastVerticalOffset = 0;
                    PlaylistPopup.HorizontalOffset = _lastHorizontalOffset;
                    PlaylistPopup.VerticalOffset = _lastVerticalOffset;
                }
            }
            PlaylistPopup.IsOpen = !PlaylistPopup.IsOpen;
        }

        private void AddMusicFiles()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "音频文件|*.flac;*.mp3;*.wav;*.m4a|FLAC文件|*.flac|MP3文件|*.mp3|WAV文件|*.wav|所有文件|*.*",
                Multiselect = true // 允许多选文件
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (string file in openFileDialog.FileNames)
                {
                    PlayList.Items.Add(file);
                }
                if (PlayList.SelectedIndex == -1 && PlayList.Items.Count > 0)
                {
                    PlayList.SelectedIndex = 0;
                }
            }
        }

        private void AddMusicFolder()
        {
            var dialog = new Forms.FolderBrowserDialog
            {
                Description = "选择音乐文件夹"
            };

            if (dialog.ShowDialog() == Forms.DialogResult.OK)
            {
                var supportedExtensions = new[] { ".mp3", ".flac", ".wav", ".m4a" };
                var files = Directory.GetFiles(dialog.SelectedPath, "*.*", SearchOption.AllDirectories)
                                   .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLower()));

                foreach (string file in files)
                {
                    PlayList.Items.Add(file);
                }
                if (PlayList.SelectedIndex == -1 && PlayList.Items.Count > 0)
                {
                    PlayList.SelectedIndex = 0;
                }
            }
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlayList.SelectedItem == null) return;

            if (!isPlaying)
            {
                if (mediaPlayer.Source == null || mediaPlayer.Position.TotalSeconds == 0)
                {
                    mediaPlayer.Open(new Uri(PlayList.SelectedItem.ToString()));
                }
                mediaPlayer.Play();
                timer.Start();
                isPlaying = true;
                PlayIcon.Text = "\uE769"; // 暂停图标
            }
            else
            {
                mediaPlayer.Pause();
                timer.Stop();
                isPlaying = false;
                PlayIcon.Text = "\uE768"; // 播放图标
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Stop();
            timer.Stop();
            ProgressSlider.Value = 0;
            isPlaying = false;
            PlayIcon.Text = "\uE768"; // 播放图标
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlayList.Items.Count == 0) return;

            int currentIndex = PlayList.SelectedIndex;
            if (currentIndex > 0)
            {
                PlayList.SelectedIndex = currentIndex - 1;
                PlaySelectedSong();
            }
            else if (currentIndex == 0)
            {
                // 如果是第一首，则循环到最后一首
                PlayList.SelectedIndex = PlayList.Items.Count - 1;
                PlaySelectedSong();
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlayList.Items.Count == 0) return;

            int currentIndex = PlayList.SelectedIndex;
            if (currentIndex < PlayList.Items.Count - 1)
            {
                PlayList.SelectedIndex = currentIndex + 1;
                PlaySelectedSong();
            }
            else if (currentIndex == PlayList.Items.Count - 1)
            {
                // 如果是最后一首，则循环到第一首
                PlayList.SelectedIndex = 0;
                PlaySelectedSong();
            }
        }

        private void PlaySelectedSong()
        {
            if (PlayList.SelectedItem != null)
            {
                mediaPlayer.Open(new Uri(PlayList.SelectedItem.ToString()));
                mediaPlayer.Play();
                timer.Start();
                isPlaying = true;
                PlayIcon.Text = "\uE769"; // 暂停图标
            }
        }

        // 修改窗口大小改变事件处理
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            if (PlaylistPopup.IsOpen)
            {
                if (!_hasCustomPosition)
                {
                    _lastHorizontalOffset = this.ActualWidth;
                    _lastVerticalOffset = 0;
                    PlaylistPopup.HorizontalOffset = _lastHorizontalOffset;
                    PlaylistPopup.VerticalOffset = _lastVerticalOffset;
                }
                PlaylistPopup.IsOpen = false;
                PlaylistPopup.IsOpen = true;
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var titleBar = sender as Border;
            if (titleBar != null)
            {
                _isDragging = true;
                titleBar.CaptureMouse();

                // 获取鼠标相对于窗口的起始位置
                _popupOffset = e.GetPosition(this);
                
                titleBar.MouseMove += TitleBar_MouseMove;
                titleBar.MouseLeftButtonUp += TitleBar_MouseLeftButtonUp;
            }
        }

        private void TitleBar_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isDragging)
            {
                var titleBar = sender as Border;
                if (titleBar != null)
                {
                    // 获取当前鼠标位置
                    System.Windows.Point currentMousePosition = e.GetPosition(this);
                    
                    // 计算鼠标移动的距离
                    double deltaX = currentMousePosition.X - _popupOffset.X;
                    double deltaY = currentMousePosition.Y - _popupOffset.Y;
                    
                    // 更新位置（基于上一次的位置）
                    _lastHorizontalOffset += deltaX;
                    _lastVerticalOffset += deltaY;
                    _hasCustomPosition = true;
                    
                    PlaylistPopup.HorizontalOffset = _lastHorizontalOffset;
                    PlaylistPopup.VerticalOffset = _lastVerticalOffset;
                    
                    // 更新鼠标位置记录
                    _popupOffset = currentMousePosition;
                }
            }
        }

        private void TitleBar_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var titleBar = sender as Border;
            if (titleBar != null)
            {
                _isDragging = false;
                titleBar.ReleaseMouseCapture();
                
                // 移除事件处理
                titleBar.MouseMove -= TitleBar_MouseMove;
                titleBar.MouseLeftButtonUp -= TitleBar_MouseLeftButtonUp;
            }
        }

        private void ClosePlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            PlaylistPopup.IsOpen = false;
        }

        private void AddFileButton_Click(object sender, RoutedEventArgs e)
        {
            AddMusicFiles();
        }

        private void AddFolderButton_Click(object sender, RoutedEventArgs e)
        {
            AddMusicFolder();
        }
    }
}