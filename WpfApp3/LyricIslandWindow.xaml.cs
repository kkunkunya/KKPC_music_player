using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace WpfApp3
{
    public partial class LyricIslandWindow : Window
    {
        private System.Windows.Point _lastPosition;
        private bool _isDragging = false;

        public LyricIslandWindow()
        {
            InitializeComponent();

            // 设置启动位置为屏幕顶部中间
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            
            this.Left = (screenWidth - this.Width) / 2;
            this.Top = 20;

            // 添加窗口关闭事件
            this.Closed += LyricIslandWindow_Closed;
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isDragging = true;
            _lastPosition = e.GetPosition(this);
            this.CaptureMouse();
        }

        protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
        {
            if (_isDragging)
            {
                System.Windows.Point currentPosition = e.GetPosition(this);
                Vector diff = currentPosition - _lastPosition;

                this.Left += diff.X;
                this.Top += diff.Y;

                // 确保窗口不会完全移出屏幕
                this.Left = Math.Max(0, Math.Min(this.Left, SystemParameters.PrimaryScreenWidth - this.Width));
                this.Top = Math.Max(0, Math.Min(this.Top, SystemParameters.PrimaryScreenHeight - this.Height));
            }
        }

        protected override void OnMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                this.ReleaseMouseCapture();
            }
            base.OnMouseLeftButtonUp(e);
        }

        private void Border_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            CloseButton.Visibility = Visibility.Visible;
        }

        private void Border_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            CloseButton.Visibility = Visibility.Collapsed;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        private void LyricIslandWindow_Closed(object sender, EventArgs e)
        {
            // 通知主窗口灵动岛已关闭
            IslandClosed?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler IslandClosed;

        // 更新歌词的方法
        public void UpdateLyric(string text, string translation = null)
        {
            Dispatcher.Invoke(() =>
            {
                // 创建渐变动画
                var fadeOut = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.0,
                    Duration = TimeSpan.FromMilliseconds(150)
                };

                var fadeIn = new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(150)
                };

                fadeOut.Completed += (s, e) =>
                {
                    // 更新主歌词
                    MainLyricTextBlock.Text = text;

                    // 更新翻译歌词
                    if (!string.IsNullOrEmpty(translation))
                    {
                        TranslationLyricTextBlock.Text = translation;
                        TranslationLyricTextBlock.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        TranslationLyricTextBlock.Visibility = Visibility.Collapsed;
                    }

                    // 应用淡入动画
                    MainLyricTextBlock.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                    if (TranslationLyricTextBlock.Visibility == Visibility.Visible)
                    {
                        TranslationLyricTextBlock.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                    }
                };

                // 应用淡出动画
                MainLyricTextBlock.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                if (TranslationLyricTextBlock.Visibility == Visibility.Visible)
                {
                    TranslationLyricTextBlock.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                }
            });
        }
    }
} 