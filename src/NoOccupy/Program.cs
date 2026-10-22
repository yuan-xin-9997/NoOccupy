using System;
using System.IO;
using System.Windows.Forms;
using NoOccupy.Localization;

namespace NoOccupy
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length > 0 && args[0].StartsWith("--", StringComparison.Ordinal))
            {
                int exitCode;
                if (Cli.TryRun(args, out exitCode))
                {
                    return exitCode;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                LogException(e.Exception);
                UiDialog.Error(
                    null,
                    L.T("解除占用"),
                    L.T("程序遇到错误：") + (e.Exception == null ? L.T("未知错误") : e.Exception.Message),
                    L.T("确定"));
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                LogException(e.ExceptionObject as Exception);
            };

            string initialPath = null;
            if (args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal))
            {
                initialPath = args[0];
            }

            try
            {
                Application.Run(new MainForm(initialPath));
            }
            catch (Exception ex)
            {
                LogException(ex);
                throw;
            }

            return 0;
        }

        private static void LogException(Exception ex)
        {
            if (ex == null)
            {
                return;
            }

            try
            {
                string path = Path.Combine(Path.GetTempPath(), "NoOccupy-error.log");
                File.AppendAllText(
                    path,
                    "==== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ====" + Environment.NewLine +
                    ex + Environment.NewLine + Environment.NewLine);
            }
            catch
            {
                // 日志失败不影响程序。
            }
        }
    }
}
