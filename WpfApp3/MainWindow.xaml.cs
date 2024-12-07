﻿using Microsoft.Win32;
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
using WinForms = System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Windows.Data;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System.Diagnostics;

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
                    Debug.WriteLine($"Pictures数组长度: {currentTagFile.Tag.Pictures.Length}");
                    
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
                            Debug.WriteLine("未能解析出有效歌词行");
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
                
                Debug.WriteLine("=== ��词读取完成 ===\n");
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
                            lyricLines.Add(new LyricLine(text1, text2, time1)));
                        Debug.WriteLine($"解析双语歌词: [{time1}] {text1} | {text2}");
                        i += 2;
                        continue;
                    }
                }
                
                if (TryParseLyricLine(line1, out TimeSpan time, out string text))
                {
                    lyrics[time] = text;
                    Dispatcher.Invoke(() => 
                        lyricLines.Add(new LyricLine(text, time)));
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
                        lyricLines.Add(new LyricLine(text, time)));
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
                    // 自动滚动到��歌词
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
                line.FontWeight = FontWeights.Normal;
            }

            // 设置当前歌词的高亮样式
            currentLyric.FontSize = 24;
            currentLyric.TextColor = new SolidColorBrush(Colors.Purple);
            currentLyric.FontWeight = FontWeights.Bold;

            // 更新当前歌词行引用
            currentLyricLine = currentLyric;
            
            // 不再需要手动刷新
            // LyricsItemsControl.Items.Refresh();
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
                                        EasingMode = EasingMode.EaseInOut  // 使用 EaseInOut 使动画更平滑
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
                // 如果已经有正在进行的动画，先停止它
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
            TextColor = new SolidColorBrush(Colors.Black);
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
}