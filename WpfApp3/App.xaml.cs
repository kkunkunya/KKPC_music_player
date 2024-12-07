using System.Configuration;
using System.Data;
using System.Windows;
using System.Diagnostics;
using System.Diagnostics.Tracing;

namespace WpfApp3
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        public App()
        {
            // 添加调试监听器
            Trace.Listeners.Add(new TextWriterTraceListener("debug.log"));
            Trace.AutoFlush = true;
            
            // 添加测试输出
            Trace.WriteLine("=== 应用程序初始化 ===");
            Debug.WriteLine("=== Debug输出测试 ===");

            // 设置调试输出级别
            if (System.Diagnostics.Debugger.IsAttached)
            {
                // 当调试器附加时输出详细信息
                Trace.WriteLine("调试器已附加");
            }
        }
    }
}
