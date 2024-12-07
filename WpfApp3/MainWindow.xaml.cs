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
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Windows.Data;

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
        private List<LyricLine> lyricLines = new List<LyricLine>();
        private LyricLine currentLyricLine;

        public MainWindow()
        {
            InitializeComponent();
            InitializePlayer();
            InitializePlaylistPopup();
            InitializeLyricsTimer();

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

        private void InitializeLyricsTimer()
        {
            lyricsTimer = new DispatcherTimer();
            lyricsTimer.Interval = TimeSpan.FromMilliseconds(100); // 100ms更新一次
            lyricsTimer.Tick += LyricsTimer_Tick;
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
                
                // 加载歌词
                LoadLyrics(filePath);

                // 播放音乐
                mediaPlayer.Open(new Uri(filePath));
                mediaPlayer.Play();
                timer.Start();
                lyricsTimer.Start();
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
                Debug.WriteLine("正在加载默认封面���片...");
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

        private void LoadLyrics(string musicFilePath)
        {
            lyrics.Clear();
            lyricLines.Clear();
            currentLyricLine = null;
            
            // 显示"正在加载歌词..."
            LyricsItemsControl.ItemsSource = new List<LyricLine> 
            { 
                new LyricLine("正在加载歌词...", TimeSpan.Zero) 
            };
            
            try 
            {
                Debug.WriteLine("\n=== 开始读取歌词数据 ===");
                
                if (currentTagFile != null && currentTagFile.Tag != null)
                {
                    // 尝试从不同的标签字段中获取歌词
                    string lyricsText = null;
                    
                    // 尝试从通用Lyrics标签获取
                    if (!string.IsNullOrEmpty(currentTagFile.Tag.Lyrics))
                    {
                        lyricsText = currentTagFile.Tag.Lyrics;
                        Debug.WriteLine("从通用Lyrics标签中找到歌词");
                    }
                    
                    // 尝试从FLAC注释中获取歌词
                    if (string.IsNullOrEmpty(lyricsText) && currentTagFile.Tag.Comment != null)
                    {
                        // 有些音乐文件可能在Comment字段中存储歌词
                        lyricsText = currentTagFile.Tag.Comment;
                        Debug.WriteLine("从Comment标签中找到可能的歌词");
                    }
                    
                    if (!string.IsNullOrEmpty(lyricsText))
                    {
                        Debug.WriteLine("开始解析歌词文本");
                        // 按行分割歌词文本
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
                            Debug.WriteLine("未能解析出有效歌词行");
                            // 更新为使用ItemsControl
                            LyricsItemsControl.ItemsSource = new List<LyricLine> 
                            { 
                                new LyricLine("歌词格式不支持", TimeSpan.Zero) 
                            };
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
                                Debug.WriteLine($"从外部LRC文件加载了 {lyrics.Count} 行歌词");
                                UpdateLyricsDisplay();
                            }
                            else
                            {
                                LyricsItemsControl.ItemsSource = new List<LyricLine> 
                                { 
                                    new LyricLine("暂无歌词", TimeSpan.Zero) 
                                };
                            }
                        }
                        else
                        {
                            LyricsItemsControl.ItemsSource = new List<LyricLine> 
                            { 
                                new LyricLine("暂无歌词", TimeSpan.Zero) 
                            };
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("当前TagLib文件对象为空");
                    LyricsItemsControl.ItemsSource = new List<LyricLine> 
                    { 
                        new LyricLine("无法读取歌词", TimeSpan.Zero) 
                    };
                }
                
                Debug.WriteLine("=== 歌词读取完成 ===\n");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"读取歌词时出错: {ex.Message}");
                Debug.WriteLine($"异常堆栈: {ex.StackTrace}");
                LyricsItemsControl.ItemsSource = new List<LyricLine> 
                { 
                    new LyricLine("歌词加载失败", TimeSpan.Zero) 
                };
            }
        }

        private void ProcessLyricLines(string[] lines)
        {
            int i = 0;
            while (i < lines.Length - 1) // 减1是为了防止最后一行越界
            {
                string line1 = lines[i];
                string line2 = lines[i + 1];
                
                // 尝试解析第一行和第二行
                if (TryParseLyricLine(line1, out TimeSpan time1, out string text1) &&
                    TryParseLyricLine(line2, out TimeSpan time2, out string text2))
                {
                    // 如果两行时间戳相同，认为是一对双语歌词
                    if (time1 == time2)
                    {
                        // 第一行是英文（主歌词），第二行是中文（翻译）
                        lyrics[time1] = text1; // 存储英文作为主歌词
                        var lyricLine = new LyricLine(text1, text2, time1); // text1是主歌词(外文)，text2是翻译(中文)
                        lyricLines.Add(lyricLine);
                        Debug.WriteLine($"解析双语歌词: [{time1}] {text1} | {text2}");
                        i += 2; // 跳过这两行
                        continue;
                    }
                }
                
                // 如果不是成对的双语歌词，就按单行处理
                if (TryParseLyricLine(line1, out TimeSpan time, out string text))
                {
                    lyrics[time] = text;
                    lyricLines.Add(new LyricLine(text, time));
                    Debug.WriteLine($"解析单语歌词: [{time}] {text}");
                }
                i++;
            }
            
            // 处理最后一行
            if (i < lines.Length)
            {
                string lastLine = lines[i];
                if (TryParseLyricLine(lastLine, out TimeSpan time, out string text))
                {
                    lyrics[time] = text;
                    lyricLines.Add(new LyricLine(text, time));
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
                Debug.WriteLine($"解析歌词行出错: {ex.Message}");
            }

            return false;
        }

        private void UpdateLyricsDisplay()
        {
            if (lyrics.Count == 0)
            {
                LyricsItemsControl.ItemsSource = new List<LyricLine> 
                { 
                    new LyricLine("暂无歌词", TimeSpan.Zero) 
                };
                return;
            }

            // 直接使用已经处理好的lyricLines列表
            LyricsItemsControl.ItemsSource = lyricLines.OrderBy(l => l.Time).ToList();
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
                    // 自动滚动到当前歌词
                    ScrollToCurrentLyric(currentLyric);
                }
            }
        }

        private void UpdateLyricHighlight(LyricLine currentLyric)
        {
            // 重置所有歌词的样式
            foreach (var line in lyricLines)
            {
                line.FontSize = 20;
                line.TextColor = new SolidColorBrush(Colors.Black);
                line.FontWeight = FontWeights.Normal;  // 重置为正常粗细
            }

            // 设置当前歌词的高亮样式
            currentLyric.FontSize = 24;
            currentLyric.TextColor = new SolidColorBrush(Colors.Purple);
            currentLyric.FontWeight = FontWeights.Bold;  // 设置为粗体

            // 更新当前歌词行引用
            currentLyricLine = currentLyric;

            // 强制刷新ItemsControl
            LyricsItemsControl.Items.Refresh();
        }

        private void ScrollToCurrentLyric(LyricLine currentLyric)
        {
            try
            {
                var index = lyricLines.IndexOf(currentLyric);
                if (index >= 0)
                {
                    // 等待布局更新完成后再计算滚动位置
                    Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                    {
                        var container = LyricsItemsControl.ItemContainerGenerator
                            .ContainerFromIndex(index) as FrameworkElement;

                        if (container != null)
                        {
                            // 获取当前歌词项的位置和大小信息
                            var transform = container.TransformToVisual(LyricsScrollViewer);
                            var containerRect = transform.TransformBounds(new Rect(0, 0, container.ActualWidth, container.ActualHeight));

                            // 计算目标滚动位置（使当前歌词位于滚动视图中央）
                            double scrollTarget = containerRect.Y + LyricsScrollViewer.VerticalOffset - 
                                (LyricsScrollViewer.ViewportHeight - container.ActualHeight) / 2;

                            // 平滑滚动到目标位置
                            LyricsScrollViewer.ScrollToVerticalOffset(scrollTarget);
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ScrollToCurrentLyric error: {ex.Message}");
            }
        }
    }

    // 添加歌词行类
    public class LyricLine
    {
        public string Text { get; set; }         // 主要歌词文本
        public string Translation { get; set; }   // 翻译/第二语言歌词
        public TimeSpan Time { get; set; }
        public double FontSize { get; set; }
        public SolidColorBrush TextColor { get; set; }
        public FontWeight FontWeight { get; set; }  // 添加字体粗细属性

        public LyricLine(string text, string translation, TimeSpan time)
        {
            Text = text;
            Translation = translation;
            Time = time;
            FontSize = 20;
            TextColor = new SolidColorBrush(Colors.Black);
            FontWeight = FontWeights.Normal;  // 默认正常粗细
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
}