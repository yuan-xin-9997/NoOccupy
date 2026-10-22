using System;
using System.IO;
using System.Windows.Forms;

namespace NoOccupy.Setup
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                InstallerEngine.Log("未处理异常：" + e.Exception);
                MessageBox.Show(
                    "程序遇到错误：" + (e.Exception == null ? "未知错误" : e.Exception.Message),
                    "解除占用 安装程序",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            };

            SetupOptions options = SetupOptions.Parse(args);

            // 安装目录中的卸载器是安装程序的副本；直接双击时按文件名进入卸载流程。
            if (!options.Uninstall &&
                string.Equals(
                    Path.GetFileName(Application.ExecutablePath),
                    "NoOccupyUninstall.exe",
                    StringComparison.OrdinalIgnoreCase))
            {
                options.Uninstall = true;
            }

            if (options.Uninstall)
            {
                if (!options.Silent)
                {
                    DialogResult answer = MessageBox.Show(
                        "确定要卸载“解除占用”吗？\r\n\r\n将删除程序文件、右键菜单和快捷方式。",
                        "卸载解除占用",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question,
                        MessageBoxDefaultButton.Button2);

                    if (answer != DialogResult.Yes)
                    {
                        return 0;
                    }
                }

                try
                {
                    InstallerEngine.Uninstall(options.InstallDir, options.FromTemp);
                    return 0;
                }
                catch (Exception ex)
                {
                    InstallerEngine.Log("卸载失败：" + ex);
                    if (!options.Silent)
                    {
                        MessageBox.Show(
                            "卸载失败：" + ex.Message + "\r\n\r\n日志：" + InstallerEngine.LogPath,
                            "解除占用",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                    return 1;
                }
            }

            if (options.Silent)
            {
                try
                {
                    InstallerEngine.Install(options.InstallDir, options.CreateDesktop);
                    return 0;
                }
                catch (Exception ex)
                {
                    InstallerEngine.Log("静默安装失败：" + ex);
                    return 1;
                }
            }

            using (var form = new SetupForm(options))
            {
                Application.Run(form);
                return form.ExitCode;
            }
        }
    }
}
