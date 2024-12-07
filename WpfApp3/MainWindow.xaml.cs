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
using TagLib;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using WinForms = System.Windows.Forms;

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
        private BitmapImage currentAlbumArt;
        private TagLib.File currentTagFile;

        public MainWindow()
        {
            InitializeComponent();
            InitializePlayer();
            InitializePlaylistPopup();

            // 只保留这一行测试输出
            Debug.WriteLine("=== 程序启动 ===");
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
                Filter = "音频文件|*.mp3;*.flac;*.wav;*.m4a;*.wma|所有文件|*.*",
                Multiselect = true
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
            if (!isPlaying)
            {
                if (mediaPlayer.Source == null && PlayList.SelectedItem != null)
                {
                    // 如果是新选择的歌曲，调用 PlaySelectedSong
                    PlaySelectedSong();
                }
                else
                {
                    // 如果是暂停后继续播放，只需要恢复播放
                    mediaPlayer.Play();
                    timer.Start();
                    isPlaying = true;
                    PlayIcon.Text = "\uE769"; // 暂停图标
                }
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
                string filePath = PlayList.SelectedItem.ToString();
                
                // 添加调试输出
                Debug.WriteLine($"准备播放文件: {filePath}");
                
                // 加载并显示元数据
                LoadMusicMetadata(filePath);

                // 播放音乐
                mediaPlayer.Open(new Uri(filePath));
                mediaPlayer.Play();
                timer.Start();
                isPlaying = true;
                PlayIcon.Text = "\uE769"; // 暂停图标
            }
        }

        private void LoadMusicMetadata(string filePath)
        {
            try
            {
                Debug.WriteLine($"\n=== 开始读取音乐文件元数据 ===");
                Debug.WriteLine($"文件路径: {filePath}");
                Debug.WriteLine($"文件格式: {Path.GetExtension(filePath)}");
                
                // 创建 TagLib 文件对象前先检查文件是否存在和可访问
                if (!System.IO.File.Exists(filePath))
                {
                    Debug.WriteLine("错误: 文件不存在");
                    return;
                }

                Debug.WriteLine("正在创建 TagLib.File 对象...");
                currentTagFile = TagLib.File.Create(filePath);
                Debug.WriteLine("TagLib.File 对象创建成功");
                
                // 检查 Tag 是否可用
                Debug.WriteLine($"Tag 是否为空: {currentTagFile.Tag == null}");
                if (currentTagFile.Tag != null)
                {
                    Debug.WriteLine($"Tag 类型: {currentTagFile.Tag.GetType().Name}");
                }
                
                // 详细的封面信息调试
                Debug.WriteLine($"\n封面信息:");
                if (currentTagFile.Tag.Pictures != null)
                {
                    Debug.WriteLine($"Pictures数组长度: {currentTagFile.Tag.Pictures.Length}");
                    
                    if (currentTagFile.Tag.Pictures.Length > 0)
                    {
                        var picture = currentTagFile.Tag.Pictures[0];
                        Debug.WriteLine($"找到封面图片:");
                        Debug.WriteLine($"- 类型: {picture.Type}");
                        Debug.WriteLine($"- MIME类型: {picture.MimeType}");
                        Debug.WriteLine($"- 描述: {picture.Description}");
                        Debug.WriteLine($"- 数据大小: {picture.Data?.Data?.Length ?? 0} bytes");
                        
                        if (picture.Data?.Data != null)
                        {
                            using (MemoryStream ms = new MemoryStream(picture.Data.Data))
                            {
                                Debug.WriteLine("\n开始创建BitmapImage...");
                                BitmapImage albumArt = new BitmapImage();
                                albumArt.BeginInit();
                                albumArt.CacheOption = BitmapCacheOption.OnLoad;
                                albumArt.StreamSource = ms;
                                albumArt.EndInit();
                                albumArt.Freeze(); // 使图片可以跨线程使用

                                Debug.WriteLine($"BitmapImage创建成功:");
                                Debug.WriteLine($"- 宽度: {albumArt.Width}");
                                Debug.WriteLine($"- 高度: {albumArt.Height}");
                                Debug.WriteLine($"- 像素宽度: {albumArt.PixelWidth}");
                                Debug.WriteLine($"- 像素高度: {albumArt.PixelHeight}");

                                currentAlbumArt = albumArt;
                                UpdateAlbumArt(albumArt);
                            }
                        }
                        else
                        {
                            Debug.WriteLine("警告: 图片数据为空");
                        }
                    }
                    else
                    {
                        Debug.WriteLine("警告: 该音乐文件没有包含封面图片");
                        currentAlbumArt = null;
                        UpdateAlbumArt(null);
                    }
                }
                else
                {
                    Debug.WriteLine("警告: Pictures 数组为空");
                }

                // 其他元数据信息
                Debug.WriteLine($"\n音乐信息:");
                Debug.WriteLine($"- 标题: {currentTagFile.Tag.Title}");
                Debug.WriteLine($"- 艺术��: {currentTagFile.Tag.FirstPerformer}");
                Debug.WriteLine($"- 专辑: {currentTagFile.Tag.Album}");
                Debug.WriteLine($"- 年份: {currentTagFile.Tag.Year}");
                Debug.WriteLine($"- 音轨号: {currentTagFile.Tag.Track}");
                Debug.WriteLine($"- 流派: {currentTagFile.Tag.FirstGenre}");
                
                string title = currentTagFile.Tag.Title ?? 
                    System.IO.Path.GetFileNameWithoutExtension(filePath);
                string artist = currentTagFile.Tag.FirstPerformer ?? "未知艺术家";
                string album = currentTagFile.Tag.Album ?? "未知专辑";

                UpdateMusicInfo(title, artist, album);
                Debug.WriteLine("=== 元数据读取完成 ===\n");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"\n错误: 读取元数据时发生异常");
                Debug.WriteLine($"异常类型: {ex.GetType().Name}");
                Debug.WriteLine($"异常信息: {ex.Message}");
                Debug.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                
                System.Windows.MessageBox.Show($"读取音乐文件元数据时出错: {ex.Message}");
                
                currentAlbumArt = null;
                UpdateAlbumArt(null);
                
                string fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
                UpdateMusicInfo(fileName, "未知艺术家", "未知专辑");
            }
        }

        private void LoadDefaultAlbumArt()
        {
            try
            {
                Debug.WriteLine("正在加载默认封面图片...");
                BitmapImage defaultArt = new BitmapImage();
                defaultArt.BeginInit();
                defaultArt.UriSource = new Uri("pack://application:,,,/Resources/default_album_art.png");
                defaultArt.EndInit();
                currentAlbumArt = defaultArt;
                UpdateAlbumArt(defaultArt);
                Debug.WriteLine("默认封面加载成功");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载默认封面失败: {ex.Message}");
                currentAlbumArt = null;
                UpdateAlbumArt(null);
            }
        }

        private void UpdateAlbumArt(BitmapImage albumArt)
        {
            if (AlbumArtImage != null)
            {
                Debug.WriteLine("\n开始更新UI上的封面图片...");
                Dispatcher.Invoke(() =>
                {
                    AlbumArtImage.Source = albumArt;
                    
                    // 强制布局更新
                    AlbumArtImage.UpdateLayout();
                    
                    // 等待布局完成后再获取尺寸
                    Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                    {
                        Debug.WriteLine($"UI封面更新完成: Source = {(albumArt == null ? "null" : "有图片")}");
                        Debug.WriteLine($"AlbumArtImage实际尺寸: {AlbumArtImage.ActualWidth} x {AlbumArtImage.ActualHeight}");
                        Debug.WriteLine($"AlbumArtImage期望尺寸: {AlbumArtImage.Width} x {AlbumArtImage.Height}");
                        Debug.WriteLine($"父容器Border尺寸: {((Border)AlbumArtImage.Parent).ActualWidth} x {((Border)AlbumArtImage.Parent).ActualHeight}");
                    }));
                });
            }
            else
            {
                Debug.WriteLine("警告: AlbumArtImage 控件为空");
            }
        }

        private void UpdateMusicInfo(string title, string artist, string album)
        {
            Dispatcher.Invoke(() =>
            {
                if (TitleTextBlock != null) TitleTextBlock.Text = title;
                if (ArtistTextBlock != null) ArtistTextBlock.Text = artist;
                if (AlbumTextBlock != null) AlbumTextBlock.Text = album;
            });
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

        private void PlayList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (PlayList.SelectedItem != null)
            {
                PlaySelectedSong();
            }
        }
    }
}