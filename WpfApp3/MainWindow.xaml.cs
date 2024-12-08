using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Controls.Primitives;
using System.IO;
using System.Linq;
using Forms = System.Windows.Forms;
using TagLib;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Windows.Data;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;

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
        private Dictionary<TimeSpan, string> lyrics = new Dictionary<TimeSpan, string>();
        private DispatcherTimer lyricsTimer;
        private ObservableCollection<LyricLine> lyricLines = new ObservableCollection<LyricLine>();
        private LyricLine currentLyricLine;
        private Dictionary<int, double> lyricPositions = new Dictionary<int, double>();
        private const double BASE_WINDOW_HEIGHT = 700; // 基准窗口高度
        private const double BASE_LYRICS_SIZE = 20;    // 基准歌词大小
        private const double BASE_CURRENT_LYRICS_SIZE = 24; // 基准当前歌词大小
        private bool isUserDraggingSlider = false;
        private LyricIslandWindow lyricIslandWindow;
        private bool isIslandVisible = false;

        public MainWindow()
        {
            InitializeComponent();
            InitializePlayer();
            InitializePlaylistPopup();
            InitializeLyricsTimer();
            
            // 订阅MediaOpened事件
            mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;
            
            // 添加窗口大小改变事件处理
            SizeChanged += MainWindow_SizeChanged;
            
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

        private void InitializeLyricsTimer()
        {
            lyricsTimer = new DispatcherTimer();
            lyricsTimer.Interval = TimeSpan.FromMilliseconds(100); // 100ms更新一次
            lyricsTimer.Tick += LyricsTimer_Tick;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (mediaPlayer.Source != null && mediaPlayer.NaturalDuration.HasTimeSpan && !isUserDraggingSlider)
            {
                ProgressSlider.Value = mediaPlayer.Position.TotalSeconds;
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
                timer.Stop();
                lyricsTimer.Stop();
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
                    PlaySelectedSong();
                }
                else
                {
                    mediaPlayer.Play();
                    timer.Start();
                    lyricsTimer.Start();
                    isPlaying = true;
                    PlayIcon.Text = "\uE769"; // 暂停图标
                }
            }
            else
            {
                mediaPlayer.Pause();
                timer.Stop();
                lyricsTimer.Stop();
                isPlaying = false;
                PlayIcon.Text = "\uE768"; // 播放图标
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Stop();
            timer.Stop();
            lyricsTimer.Stop();
            ProgressSlider.Value = 0;
            isPlaying = false;
            PlayIcon.Text = "\uE768"; // 播放图标
            UpdateLyricsDisplay(); // 重置歌词显示
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
                // 如果是第一首，则循环到最���一首
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
                
                Debug.WriteLine($"准备播放文件: {filePath}");
                
                LoadMusicMetadata(filePath);
                LoadLyrics(filePath);

                mediaPlayer.Open(new Uri(filePath));
                isPlaying = true;
                PlayIcon.Text = "\uE769"; // 暂停图标

                // 重置进度条
                ProgressSlider.Value = 0;
            }
        }

        private void LoadMusicMetadata(string filePath)
        {
            try
            {
                Debug.WriteLine($"\n=== 开始读取音乐文件元数据 ===");
                Debug.WriteLine($"文件路径: {filePath}");
                Debug.WriteLine($"文件格式: {Path.GetExtension(filePath)}");
                
                // 创建 TagLib 件对象前先检查文件是否存在和可访问
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
                    Debug.WriteLine($"Pictures组长度: {currentTagFile.Tag.Pictures.Length}");
                    
                    if (currentTagFile.Tag.Pictures.Length > 0)
                    {
                        var picture = currentTagFile.Tag.Pictures[0];
                        Debug.WriteLine($"到封面图片:");
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
                                albumArt.Freeze(); // 使图片可程使用

                                Debug.WriteLine($"BitmapImage创建成功:");
                                Debug.WriteLine($"- 宽度: {albumArt.Width}");
                                Debug.WriteLine($"- 高度: {albumArt.Height}");
                                Debug.WriteLine($"- 像宽度: {albumArt.PixelWidth}");
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
                Debug.WriteLine($"- 艺术家: {currentTagFile.Tag.FirstPerformer}");
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
                Debug.WriteLine($"认封面失败: {ex.Message}");
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
                        
                        // 应用颜色方案
                        if (albumArt != null)
                        {
                            ApplyColorSchemeFromAlbumArt(albumArt);
                        }
                        else
                        {
                            ResetColorSchemeToDefault();
                        }
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
            
            // 重新计算歌词位置
            if (currentLyricLine != null)
            {
                ScrollToCurrentLyric(currentLyricLine);
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
                    
                    // 更新位置（基于上一的位置）
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

        private void LoadLyrics(string musicFilePath)
        {
            lyrics.Clear();
            Dispatcher.Invoke(() => lyricLines.Clear());
            currentLyricLine = null;
            
            // 显示"正在加载歌词..."
            Dispatcher.Invoke(() =>
            {
                lyricLines.Clear();
                lyricLines.Add(new LyricLine("正在加载歌词...", TimeSpan.Zero));
            });
            
            try 
            {
                Debug.WriteLine("\n=== 开始读取歌词数据 ===");
                
                if (currentTagFile != null && currentTagFile.Tag != null)
                {
                    // 尝试从不同的标签字段获取歌词
                    string lyricsText = null;
                    
                    // 尝试从通用Lyrics标签获取
                    if (!string.IsNullOrEmpty(currentTagFile.Tag.Lyrics))
                    {
                        lyricsText = currentTagFile.Tag.Lyrics;
                        Debug.WriteLine("从通用Lyrics标签中找到歌词");
                    }
                    
                    // ���试从FLAC注释中获取歌词
                    if (string.IsNullOrEmpty(lyricsText) && currentTagFile.Tag.Comment != null)
                    {
                        // 有些音乐文件可能在Comment字段中存储歌词
                        lyricsText = currentTagFile.Tag.Comment;
                        Debug.WriteLine("从Comment标签中找到可能的歌词");
                    }
                    
                    if (!string.IsNullOrEmpty(lyricsText))
                    {
                        Debug.WriteLine("开始解析歌词文本");
                        // 按行分歌词文本
                        string[] lines = lyricsText.Split(new[] { "\r\n", "\r", "\n" }, 
                            StringSplitOptions.RemoveEmptyEntries);
                        
                        ProcessLyricLines(lines);
                        
                        if (lyrics.Count > 0)
                        {
                            Debug.WriteLine($"成功加载了 {lyrics.Count} 行歌词");
                            UpdateLyricsDisplay();
                        }
                        else
                        {
                            Debug.WriteLine("未能解析有效歌词行");
                            // 更新为使用ItemsControl
                            Dispatcher.Invoke(() =>
                            {
                                lyricLines.Clear();
                                lyricLines.Add(new LyricLine("歌词格式不支持", TimeSpan.Zero));
                            });
                        }
                    }
                    else
                    {
                        Debug.WriteLine("未在音频文件中找到歌词");
                        string lrcPath = Path.ChangeExtension(musicFilePath, ".lrc");
                        if (System.IO.File.Exists(lrcPath))
                        {
                            string[] lines = System.IO.File.ReadAllLines(lrcPath, System.Text.Encoding.UTF8);
                            ProcessLyricLines(lines);
                            if (lyrics.Count > 0)
                            {
                                Debug.WriteLine($"从LRC文件加载了 {lyrics.Count} 行歌词");
                                UpdateLyricsDisplay();
                            }
                            else
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    lyricLines.Clear();
                                    lyricLines.Add(new LyricLine("无歌词", TimeSpan.Zero));
                                });
                            }
                        }
                        else
                        {
                            Dispatcher.Invoke(() =>
                            {
                                lyricLines.Clear();
                                lyricLines.Add(new LyricLine("暂无歌词", TimeSpan.Zero));
                            });
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("当前TagLib文件对象为空");
                    Dispatcher.Invoke(() =>
                    {
                        lyricLines.Clear();
                        lyricLines.Add(new LyricLine("无法读取歌词", TimeSpan.Zero));
                    });
                }
                
                Debug.WriteLine("=== 歌词读取完成 ===\n");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"读取歌词时出错: {ex.Message}");
                Debug.WriteLine($"异常堆栈: {ex.StackTrace}");
                Dispatcher.Invoke(() =>
                {
                    lyricLines.Clear();
                    lyricLines.Add(new LyricLine("歌词加载失败", TimeSpan.Zero));
                });
            }
        }

        private void ProcessLyricLines(string[] lines)
        {
            double scale = this.ActualHeight / BASE_WINDOW_HEIGHT;
            Dispatcher.Invoke(() => lyricLines.Clear());
            int i = 0;
            while (i < lines.Length - 1)
            {
                string line1 = lines[i];
                string line2 = lines[i + 1];
                
                if (TryParseLyricLine(line1, out TimeSpan time1, out string text1) &&
                    TryParseLyricLine(line2, out TimeSpan time2, out string text2))
                {
                    if (time1 == time2)
                    {
                        lyrics[time1] = text1;
                        Dispatcher.Invoke(() => 
                            lyricLines.Add(new LyricLine(text1, text2, time1) 
                            { 
                                FontSize = BASE_LYRICS_SIZE * scale 
                            }));
                        Debug.WriteLine($"解析双语歌词: [{time1}] {text1} | {text2}");
                        i += 2;
                        continue;
                    }
                }
                
                if (TryParseLyricLine(line1, out TimeSpan time, out string text))
                {
                    lyrics[time] = text;
                    Dispatcher.Invoke(() => 
                        lyricLines.Add(new LyricLine(text, time) 
                        { 
                            FontSize = BASE_LYRICS_SIZE * scale 
                        }));
                    Debug.WriteLine($"解析单歌词: [{time}] {text}");
                }
                i++;
            }
            
            if (i < lines.Length)
            {
                string lastLine = lines[i];
                if (TryParseLyricLine(lastLine, out TimeSpan time, out string text))
                {
                    lyrics[time] = text;
                    Dispatcher.Invoke(() => 
                        lyricLines.Add(new LyricLine(text, time) 
                        { 
                            FontSize = BASE_LYRICS_SIZE * scale 
                        }));
                    Debug.WriteLine($"解析最后一行: [{time}] {text}");
                }
            }
        }

        private bool TryParseLyricLine(string line, out TimeSpan time, out string text)
        {
            time = TimeSpan.Zero;
            text = string.Empty;

            try
            {
                var match = Regex.Match(line, @"\[(\d{2}):(\d{2})\.(\d{2})\](.*)");
                if (match.Success)
                {
                    int minutes = int.Parse(match.Groups[1].Value);
                    int seconds = int.Parse(match.Groups[2].Value);
                    int milliseconds = int.Parse(match.Groups[3].Value) * 10;

                    time = new TimeSpan(0, 0, minutes, seconds, milliseconds);
                    text = match.Groups[4].Value.Trim();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"解析歌词行错: {ex.Message}");
            }

            return false;
        }

        private void UpdateLyricsDisplay()
        {
            if (lyrics.Count == 0)
            {
                Dispatcher.Invoke(() =>
                {
                    lyricLines.Clear();
                    lyricLines.Add(new LyricLine("暂无歌词", TimeSpan.Zero));
                });
                return;
            }

            Dispatcher.Invoke(() =>
            {
                var scrollViewer = GetScrollViewer(LyricsItemsControl);
                double currentOffset = scrollViewer?.VerticalOffset ?? 0;

                var sorted = lyricLines.OrderBy(l => l.Time).ToList();
                lyricLines.Clear();
                foreach (var line in sorted)
                {
                    lyricLines.Add(line);
                }

                if (LyricsItemsControl.ItemsSource != lyricLines)
                {
                    LyricsItemsControl.ItemsSource = lyricLines;
                }

                // 强制布局更新
                LyricsItemsControl.UpdateLayout();

                // 恢复滚动位置
                if (scrollViewer != null)
                {
                    scrollViewer.ScrollToVerticalOffset(currentOffset);
                }
            });
        }

        private void LyricsTimer_Tick(object sender, EventArgs e)
        {
            if (mediaPlayer.Source != null && isPlaying && lyricLines.Count > 0)
            {
                var currentPosition = mediaPlayer.Position;
                
                // 找到当前时间最接近的歌词
                var currentLyric = lyricLines
                    .Where(l => l.Time <= currentPosition)
                    .OrderByDescending(l => l.Time)
                    .FirstOrDefault();

                if (currentLyric != null && currentLyric != currentLyricLine)
                {
                    // 更新歌词显示状态
                    UpdateLyricHighlight(currentLyric);
                    // 自动滚动到歌词
                    ScrollToCurrentLyric(currentLyric);
                }
            }
        }

        private void UpdateLyricHighlight(LyricLine currentLyric)
        {
            double scale = this.ActualHeight / BASE_WINDOW_HEIGHT;
            
            // 获取资源中的笔
            var normalColorBrush = (SolidColorBrush)this.Resources["LyricTextColor"];
            var highlightColorBrush = (SolidColorBrush)this.Resources["LyricHighlightColor"];

            // 重置所有歌词的样式
            foreach (var line in lyricLines)
            {
                line.FontSize = BASE_LYRICS_SIZE * scale;
                line.TextColor = normalColorBrush; // 使用LyricTextColor作为默认歌词颜色
                line.FontWeight = FontWeights.Normal;
            }

            // 设置当前歌词的高亮样式
            currentLyric.FontSize = BASE_CURRENT_LYRICS_SIZE * scale;
            currentLyric.TextColor = highlightColorBrush; // 使用LyricHighlightColor作为高亮歌词颜色
            currentLyric.FontWeight = FontWeights.Bold;

            // 更新当前歌词行引用
            currentLyricLine = currentLyric;

            // 更新灵动岛歌词
            if (isIslandVisible && lyricIslandWindow != null)
            {
                lyricIslandWindow.UpdateLyric(
                    currentLyric.Text, 
                    string.IsNullOrEmpty(currentLyric.Translation) ? null : currentLyric.Translation
                );
            }
        }

        private void ScrollToCurrentLyric(LyricLine currentLyric)
        {
            try
            {
                var index = lyricLines.IndexOf(currentLyric);
                if (index >= 0)
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                    {
                        var scrollViewer = LyricScrollViewer;
                        if (scrollViewer != null)
                        {
                            scrollViewer.UpdateLayout();
                            LyricsItemsControl.UpdateLayout();

                            var container = LyricsItemsControl.ItemContainerGenerator
                                .ContainerFromIndex(index) as FrameworkElement;

                            if (container != null)
                            {
                                container.UpdateLayout();
                                
                                var point = container.TransformToAncestor(LyricsItemsControl)
                                                   .Transform(new System.Windows.Point(0, 0));
                                var containerTop = point.Y;
                                
                                var targetOffset = containerTop + (container.ActualHeight / 2) - (scrollViewer.ViewportHeight / 2);
                                targetOffset = Math.Max(0, Math.Min(targetOffset, scrollViewer.ScrollableHeight));

                               var animation = new DoubleAnimation
                                {
                                    Duration = TimeSpan.FromMilliseconds(300),
                                   EasingFunction = new QuinticEase 
                                    { 
                                        EasingMode = EasingMode.EaseInOut  // 使用 EaseInOut 动画更平滑
                                    }
                                };

                                ScrollViewerAnimator.AnimateScroll(scrollViewer, targetOffset, animation);
                            }
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ScrollToCurrentLyric error: {ex.Message}");
            }
        }

        // 添加获取 ScrollViewer 的辅助方法
        private ScrollViewer GetScrollViewer(DependencyObject element)
        {
            if (element is ScrollViewer)
                return element as ScrollViewer;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                var result = GetScrollViewer(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        // 修改 ScrollViewerAnimator 类
        public static class ScrollViewerAnimator
        {
            private static readonly Dictionary<ScrollViewer, AnimationState> _activeAnimations 
                = new Dictionary<ScrollViewer, AnimationState>();

            private class AnimationState
            {
                public double StartOffset { get; set; }
                public double TargetOffset { get; set; }
                public double StartTime { get; set; }
                public double Duration { get; set; }
                public IEasingFunction EasingFunction { get; set; }
                public CompositionTarget RenderingSubscription { get; set; }
            }

            public static void AnimateScroll(ScrollViewer scrollViewer, double targetOffset, DoubleAnimation animation)
            {
                // 如果已经有在进行的动画，先停止它
                StopAnimation(scrollViewer);

                double startOffset = scrollViewer.VerticalOffset;
                double distance = targetOffset - startOffset;
                
                // 如果距离太小，直接设置位置
                if (Math.Abs(distance) < 1)
                {
                    scrollViewer.ScrollToVerticalOffset(targetOffset);
                    return;
                }

                var state = new AnimationState
                {
                    StartOffset = startOffset,
                    TargetOffset = targetOffset,
                    StartTime = GetCurrentTime(),
                    Duration = animation.Duration.TimeSpan.TotalMilliseconds,
                    EasingFunction = animation.EasingFunction
                };

                _activeAnimations[scrollViewer] = state;

                CompositionTarget.Rendering += OnRendering;

                void OnRendering(object sender, EventArgs e)
                {
                    if (!_activeAnimations.TryGetValue(scrollViewer,out var currentState))
                    {
                        CompositionTarget.Rendering -= OnRendering;
                        return;
                    }

                    double elapsed = GetCurrentTime() - currentState.StartTime;
                    double progress = Math.Min(1.0, elapsed / currentState.Duration);

                    if (currentState.EasingFunction != null)
                    {
                        progress = currentState.EasingFunction.Ease(progress);
                    }

                    double newOffset = currentState.StartOffset + (distance * progress);
                    newOffset = Math.Max(0, Math.Min(newOffset, scrollViewer.ScrollableHeight));
                    
                    scrollViewer.ScrollToVerticalOffset(newOffset);

                    if (progress >= 1.0)
                    {
                        StopAnimation(scrollViewer);
                        CompositionTarget.Rendering -= OnRendering;
                        scrollViewer.ScrollToVerticalOffset(targetOffset);
                    }
                }
            }

            private static void StopAnimation(ScrollViewer scrollViewer)
            {
                if (_activeAnimations.TryGetValue(scrollViewer, out var state))
                {
                    _activeAnimations.Remove(scrollViewer);
                }
            }

            private static double GetCurrentTime()
            {
                return (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency * 1000.0;
            }
        }

        // 添加缓动函数
        private double EaseInOutCubic(double t)
        {
            return t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
        }

        // 添加辅助方法来查找 ScrollViewer
        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T found)
                    return found;
                    
                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateLyricsFontSize();
        }

        private void UpdateLyricsFontSize()
        {
            // 计算缩放比例
            double scale = this.ActualHeight / BASE_WINDOW_HEIGHT;
            
            // 更新所有歌词的字体大小
            foreach (var line in lyricLines)
            {
                if (line == currentLyricLine)
                {
                    line.FontSize = BASE_CURRENT_LYRICS_SIZE * scale;
                }
                else
                {
                    line.FontSize = BASE_LYRICS_SIZE * scale;
                }
            }
        }

        private void ApplyColorSchemeFromAlbumArt(BitmapImage albumArt)
        {
            try
            {
                Debug.WriteLine("开始从专辑封面提取颜色...");

                // 提取主色调
                System.Windows.Media.Color dominantColor = ColorExtractionHelper.ExtractDominantColor(albumArt);
                Debug.WriteLine($"提取到的主色调: R={dominantColor.R}, G={dominantColor.G}, B={dominantColor.B}");

                // 基础背景颜色（更亮，用于对比）
                System.Windows.Media.Color backgroundColor = ColorExtractionHelper.AdjustColorBrightness(dominantColor, 2.0);
                double bgLuminance = GetRelativeLuminance(backgroundColor);

                // 初步尝试文本颜色: 若背景较亮则将dominantColor变暗一些
                double initialFactor = bgLuminance > 0.5 ? 0.5 : 1.5;
                System.Windows.Media.Color textColor = ColorExtractionHelper.AdjustColorBrightness(dominantColor, initialFactor);

                // 调整文本颜色确保对比
                textColor = EnsureGoodContrast(textColor, backgroundColor);

                // 在此基础上获取高亮颜色, 稍微偏离 textColor 的亮度，使之清晰
                double highlightFactor = bgLuminance > 0.5 ? 0.8 : 1.2; // 背景亮则略深，背景暗则略亮
                System.Windows.Media.Color highlightColor = ColorExtractionHelper.AdjustColorBrightness(dominantColor, highlightFactor);
                highlightColor = EnsureGoodContrast(highlightColor, backgroundColor);

                // UI高亮色作为按钮和控件的颜色，可稍微调浅一点
                System.Windows.Media.Color uiHighlightColor = ColorExtractionHelper.AdjustColorBrightness(dominantColor, 1.2);

                Dispatcher.Invoke(() =>
                {
                    this.Resources["PrimaryColor"] = new SolidColorBrush(dominantColor);
                    this.Resources["HighlightColor"] = new SolidColorBrush(uiHighlightColor);
                    this.Resources["BackgroundColor"] = new SolidColorBrush(backgroundColor);
                    this.Resources["LyricTextColor"] = new SolidColorBrush(textColor);
                    this.Resources["LyricHighlightColor"] = new SolidColorBrush(highlightColor);

                    Debug.WriteLine("颜色方案已应用到界面");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ApplyColorSchemeFromAlbumArt error: {ex.Message}");
                ResetColorSchemeToDefault();
            }
        }

        // 确保颜色有足够对比度的方法
        private System.Windows.Media.Color EnsureGoodContrast(System.Windows.Media.Color fgColor, System.Windows.Media.Color bgColor)
        {
            int attempts = 0;
            // 如果对比度不够，就反复调整亮度和饱和度
            while (!HasGoodContrast(fgColor, bgColor) && attempts < 10)
            {
                double luminanceFg = GetRelativeLuminance(fgColor);
                double luminanceBg = GetRelativeLuminance(bgColor);

                if (luminanceFg > luminanceBg)
                {
                    // 前景比背景亮，但对比不够，降低亮度
                    fgColor = ColorExtractionHelper.AdjustColorBrightness(fgColor, 0.9);
                }
                else
                {
                    // 前景比背景暗，但不够对比，继续加深
                    fgColor = ColorExtractionHelper.AdjustColorBrightness(fgColor, 0.9);
                }

                // 同时提高饱和度
                fgColor = AdjustColorSaturation(fgColor, 1.1);

                attempts++;
            }
            return fgColor;
        }

        // 计算相对亮度
        private double GetRelativeLuminance(System.Windows.Media.Color c)
        {
            double r = c.R / 255.0;
            double g = c.G / 255.0;
            double b = c.B / 255.0;

            r = r <= 0.03928 ? r / 12.92 : Math.Pow((r + 0.055) / 1.055, 2.4);
            g = g <= 0.03928 ? g / 12.92 : Math.Pow((g + 0.055) / 1.055, 2.4);
            b = b <= 0.03928 ? b / 12.92 : Math.Pow((b + 0.055) / 1.055, 2.4);

            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        }

        // 检查对比度是否足够
        private bool HasGoodContrast(System.Windows.Media.Color fg, System.Windows.Media.Color bg)
        {
            double l1 = GetRelativeLuminance(fg);
            double l2 = GetRelativeLuminance(bg);
            double contrast = (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
            return contrast >= 4.5; // WCAG标准
        }

        // 调整颜色饱和度
        private System.Windows.Media.Color AdjustColorSaturation(System.Windows.Media.Color color, double factor)
        {
            var hsv = RgbToHsv(color.R, color.G, color.B);
            hsv.S = Math.Min(1.0, hsv.S * factor);
            var rgb = HsvToRgb(hsv.H, hsv.S, hsv.V);
            return System.Windows.Media.Color.FromRgb(rgb.r, rgb.g, rgb.b);
        }

        // RGB转HSV
        private (double H, double S, double V) RgbToHsv(byte r, byte g, byte b)
        {
            double rf = r / 255.0;
            double gf = g / 255.0;
            double bf = b / 255.0;

            double max = Math.Max(rf, Math.Max(gf, bf));
            double min = Math.Min(rf, Math.Min(gf, bf));
            double delta = max - min;

            double h = 0.0;
            if (delta > 0)
            {
                if (max == rf)
                    h = (gf - bf) / delta + (gf < bf ? 6 : 0);
                else if (max == gf)
                    h = (bf - rf) / delta + 2;
                else
                    h = (rf - gf) / delta + 4;
                h /= 6;
            }

            double s = max == 0 ? 0 : delta / max;
            double v = max;

            return (h, s, v);
        }

        // HSV转RGB
        private (byte r, byte g, byte b) HsvToRgb(double h, double s, double v)
        {
            double r, g, b;
            if (s == 0.0)
            {
                r = g = b = v;
            }
            else
            {
                h = h * 6.0;
                int i = (int)Math.Floor(h);
                double f = h - i;
                double p = v * (1.0 - s);
                double q = v * (1.0 - s * f);
                double t = v * (1.0 - s * (1.0 - f));
                switch (i)
                {
                    case 0: r = v; g = t; b = p; break;
                    case 1: r = q; g = v; b = p; break;
                    case 2: r = p; g = v; b = t; break;
                    case 3: r = p; g = q; b = v; break;
                    case 4: r = t; g = p; b = v; break;
                    default: r = v; g = p; b = q; break;
                }
            }

            return ((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }

        private void ResetColorSchemeToDefault()
        {
            Debug.WriteLine("重置为默认颜色方案");
            
            Dispatcher.Invoke(() =>
            {
                var defaultPrimaryColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF673AB7");
                this.Resources["PrimaryColor"] = new SolidColorBrush(defaultPrimaryColor);
                this.Resources["HighlightColor"] = new SolidColorBrush(defaultPrimaryColor);
                this.Resources["LyricTextColor"] = new SolidColorBrush(System.Windows.Media.Colors.Black);
                this.Resources["BackgroundColor"] = new SolidColorBrush(System.Windows.Media.Colors.White);
                this.Resources["LyricHighlightColor"] = new SolidColorBrush(System.Windows.Media.Colors.Purple);
            });
        }

        // 添加MediaOpened事件处理
        private void MediaPlayer_MediaOpened(object sender, EventArgs e)
        {
            if (mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                ProgressSlider.Maximum = mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
            }
            else
            {
                ProgressSlider.Maximum = 100;
            }

            if (isPlaying)
            {
                mediaPlayer.Play();
                timer.Start();
                lyricsTimer.Start();
            }

            ProgressSlider.Value = 0;
        }

        // 添加进度条拖拽事件处理
        private void ProgressSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            isUserDraggingSlider = true;
        }

        private void ProgressSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            isUserDraggingSlider = false;
            if (mediaPlayer.Source != null)
            {
                mediaPlayer.Position = TimeSpan.FromSeconds(ProgressSlider.Value);
            }
        }

        private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!isUserDraggingSlider && mediaPlayer.Source != null && mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                // 如果不是拖拽导致的值变化（比如点击进度条），也更新播放位置
                mediaPlayer.Position = TimeSpan.FromSeconds(ProgressSlider.Value);
            }
        }

        // 添加灵动岛按钮点击事件处理
        private void IslandButton_Click(object sender, RoutedEventArgs e)
        {
            if (lyricIslandWindow == null)
            {
                lyricIslandWindow = new LyricIslandWindow();
                lyricIslandWindow.IslandClosed += (s, args) =>
                {
                    isIslandVisible = false;
                    lyricIslandWindow = null;
                };
            }

            if (!isIslandVisible)
            {
                lyricIslandWindow.Show();
                isIslandVisible = true;
                
                // 如果当前有歌词在播放，立即更新灵动岛
                if (currentLyricLine != null)
                {
                    lyricIslandWindow.UpdateLyric(currentLyricLine.Text);
                }
            }
            else
            {
                lyricIslandWindow.Hide();
                isIslandVisible = false;
            }
        }

        // 在MainWindow的关闭事件中关闭灵动岛
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (lyricIslandWindow != null)
            {
                lyricIslandWindow.Close();
            }
        }
    }

    // 添加歌词行类
    public class LyricLine : INotifyPropertyChanged
    {
        private string _text;
        private string _translation;
        private double _fontSize;
        private SolidColorBrush _textColor;
        private FontWeight _fontWeight;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Translation
        {
            get => _translation;
            set
            {
                if (_translation != value)
                {
                    _translation = value;
                    OnPropertyChanged();
                }
            }
        }

        public TimeSpan Time { get; set; }

        public double FontSize
        {
            get => _fontSize;
            set
            {
                if (_fontSize != value)
                {
                    _fontSize = value;
                    OnPropertyChanged();
                }
            }
        }

        public SolidColorBrush TextColor
        {
            get => _textColor;
            set
            {
                if (_textColor != value)
                {
                    _textColor = value;
                    OnPropertyChanged();
                }
            }
        }

        public FontWeight FontWeight
        {
            get => _fontWeight;
            set
            {
                if (_fontWeight != value)
                {
                    _fontWeight = value;
                    OnPropertyChanged();
                }
            }
        }

        public LyricLine(string text, string translation, TimeSpan time)
        {
            Text = text;
            Translation = translation;
            Time = time;
            FontSize = 20;
            
            // 尝试从资源获取默认文本颜色，如果获取失败则使用黑色
            if (System.Windows.Application.Current.MainWindow?.Resources["LyricTextColor"] is SolidColorBrush defaultBrush)
            {
                TextColor = defaultBrush;
            }
            else
            {
                TextColor = new SolidColorBrush(System.Windows.Media.Colors.Black);
            }
            
            FontWeight = FontWeights.Normal;
        }

        public LyricLine(string text, TimeSpan time) : this(text, null, time) { }
    }

    public class FontSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double fontSize)
            {
                return fontSize - 4;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public static class ColorExtractionHelper 
    {
        /// <summary>
        /// 从 BitmapSource 中提取主色调
        /// </summary>
        public static System.Windows.Media.Color ExtractDominantColor(BitmapSource bitmap, int sampleCount = 1000)
        {
            if (bitmap == null) return System.Windows.Media.Colors.Gray;

            try
            {
                // 将图像转换为FormatConvertedBitmap以确保格式一致
                FormatConvertedBitmap formatConvertedBitmap = new FormatConvertedBitmap();
                formatConvertedBitmap.BeginInit();
                formatConvertedBitmap.Source = bitmap;
                formatConvertedBitmap.DestinationFormat = PixelFormats.Bgra32;
                formatConvertedBitmap.EndInit();

                int width = formatConvertedBitmap.PixelWidth;
                int height = formatConvertedBitmap.PixelHeight;
                int stride = width * 4; // 4 bytes per pixel for BGRA32
                byte[] pixels = new byte[height * stride];
                formatConvertedBitmap.CopyPixels(pixels, stride, 0);

                // 随机抽样像素
                Random rand = new Random();
                long totalR = 0, totalG = 0, totalB = 0;
                int validSamples = 0;

                for (int i = 0; i < sampleCount && i < (width * height); i++)
                {
                    int x = rand.Next(width);
                    int y = rand.Next(height);
                    int index = y * stride + x * 4;

                    if ((index + 3) < pixels.Length) // 确保不会越界
                    {
                        byte b = pixels[index];
                        byte g = pixels[index + 1];
                        byte r = pixels[index + 2];
                        byte a = pixels[index + 3];

                        if (a > 50) // 只统计透明度较高的像素
                        {
                            totalR += r;
                            totalG += g;
                            totalB += b;
                            validSamples++;
                        }
                    }
                }

                if (validSamples == 0) return System.Windows.Media.Colors.Gray;

                byte avgR = (byte)(totalR / validSamples);
                byte avgG = (byte)(totalG / validSamples);
                byte avgB = (byte)(totalB / validSamples);

                return System.Windows.Media.Color.FromRgb(avgR, avgG, avgB);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ExtractDominantColor error: {ex.Message}");
                return System.Windows.Media.Colors.Gray;
            }
        }

        /// <summary>
        /// 计算给定颜色的对比色
        /// </summary>
        public static System.Windows.Media.Color GetContrastingColor(System.Windows.Media.Color baseColor)
        {
            double luminance = (0.299 * baseColor.R + 0.587 * baseColor.G + 0.114 * baseColor.B) / 255;
            return luminance > 0.5 ? System.Windows.Media.Colors.Black : System.Windows.Media.Colors.White;
        }

        /// <summary>
        /// 调整颜色亮度
        /// </summary>
        public static System.Windows.Media.Color AdjustColorBrightness(System.Windows.Media.Color color, double factor)
        {
            byte r = (byte)Math.Min(255, color.R * factor);
            byte g = (byte)Math.Min(255, color.G * factor);
            byte b = (byte)Math.Min(255, color.B * factor);
            return System.Windows.Media.Color.FromRgb(r, g, b);
        }
    }
}