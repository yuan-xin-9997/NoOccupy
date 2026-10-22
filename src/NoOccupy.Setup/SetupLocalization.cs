using System;
using System.Collections.Generic;
using NoOccupy.Localization;

namespace NoOccupy.Setup
{
    /// <summary>安装器文案，同样以中文原文为键。</summary>
    internal static class S
    {
        private static readonly Dictionary<string, string> English =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "解除占用", "Release Lock" },
                { "解除占用 安装程序", "Release Lock Setup" },
                { "一键检测并结束正在占用文件或文件夹的进程", "Find and end processes that are using a file or folder" },
                { "安装程序  v{0}", "Setup  v{0}" },
                { "安装目录", "Install folder" },
                { "浏览...", "Browse..." },
                { "创建桌面快捷方式", "Create desktop shortcut" },
                { "安装后将添加到文件、文件夹的右键菜单中。\r\nWindows 11 若未直接显示，请点击右键菜单底部的“显示更多选项”。", "The app will be added to the context menu for files and folders.\r\nIf it is not shown directly on Windows 11, click “Show more options” at the bottom of the context menu." },
                { "将安装以下内容", "The following will be installed" },
                { "•  主程序与卸载程序（约 0.3 MB）\r\n•  文件 / 文件夹右键菜单“{0}”\r\n•  开始菜单快捷方式与系统卸载信息", "•  Main program and uninstaller (about 0.3 MB)\r\n•  File / folder context menu “{0}”\r\n•  Start menu shortcut and uninstall entry" },
                { "准备安装", "Ready to install" },
                { "取消", "Cancel" },
                { "安装", "Install" },
                { "选择解除占用的安装目录", "Select the install folder" },
                { "无法选择目录", "Unable to select folder" },
                { "安装目录无效", "Invalid install folder" },
                { "正在安装，请稍候...", "Installing, please wait..." },
                { "安装完成", "Installation complete" },
                { "“{0}”已安装成功。\r\n\r\n安装目录：{1}\r\n\r\n现在可以右键点击文件或文件夹，选择“{0}”。\r\nWindows 11 请在“显示更多选项”中查看。", "“{0}” was installed successfully.\r\n\r\nInstall folder: {1}\r\n\r\nYou can now right-click a file or folder and choose “{0}”.\r\nOn Windows 11, look under “Show more options”." },
                { "安装成功", "Installation successful" },
                { "安装失败", "Installation failed" },
                { "安装目录不能是磁盘根目录。", "The install folder cannot be a drive root." },
                { "主程序释放失败，安装包可能不完整。", "Failed to extract the main program. The installer may be incomplete." },
                { "安装失败：", "Installation failed: " },
                { "无法完成卸载：", "Unable to complete uninstallation: " },
                { "卸载信息已移除，但安装目录删除失败：", "Uninstall information was removed, but the install folder could not be deleted: " },
                { "安装包内缺少资源：", "The installer is missing a resource: " },
                { "系统未提供 WScript.Shell。", "WScript.Shell is not available on this system." },
                { "检测并结束占用文件的进程", "Find and end processes using locked files" },
                { "程序遇到错误：", "The program encountered an error: " },
                { "未知错误", "Unknown error" },
                { "卸载解除占用", "Uninstall Release Lock" },
                { "确定要卸载“解除占用”吗？\r\n\r\n将删除程序文件、右键菜单和快捷方式。", "Uninstall “Release Lock”?\r\n\r\nProgram files, context menu entries, and shortcuts will be removed." },
                { "卸载失败：", "Uninstallation failed: " },
                { "卸载失败", "Uninstallation failed" },
                { "日志：", "Log: " },
                { "界面语言", "Language" },
                { "切换界面语言", "Change interface language" },
                { "是", "Yes" },
                { "否", "No" },
                { "确定", "OK" }
            };

        private static readonly Dictionary<string, string> Japanese =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "解除占用", "占有を解除" },
                { "解除占用 安装程序", "占有を解除 セットアップ" },
                { "一键检测并结束正在占用文件或文件夹的进程", "ファイルやフォルダーを使用しているプロセスを検出して終了します" },
                { "安装程序  v{0}", "セットアップ  v{0}" },
                { "安装目录", "インストール先" },
                { "浏览...", "参照..." },
                { "创建桌面快捷方式", "デスクトップにショートカットを作成" },
                { "安装后将添加到文件、文件夹的右键菜单中。\r\nWindows 11 若未直接显示，请点击右键菜单底部的“显示更多选项”。", "インストール後、ファイルとフォルダーの右クリックメニューに追加されます。\r\nWindows 11 では「その他のオプションを表示」から開けます。" },
                { "将安装以下内容", "以下をインストールします" },
                { "•  主程序与卸载程序（约 0.3 MB）\r\n•  文件 / 文件夹右键菜单“{0}”\r\n•  开始菜单快捷方式与系统卸载信息", "•  メインプログラムとアンインストーラー（約 0.3 MB）\r\n•  ファイル / フォルダーの右クリックメニュー「{0}」\r\n•  スタートメニューのショートカットとアンインストール情報" },
                { "准备安装", "インストールの準備完了" },
                { "取消", "キャンセル" },
                { "安装", "インストール" },
                { "选择解除占用的安装目录", "インストール先フォルダーを選択" },
                { "无法选择目录", "フォルダーを選択できません" },
                { "安装目录无效", "インストール先が無効です" },
                { "正在安装，请稍候...", "インストール中です。お待ちください..." },
                { "安装完成", "インストール完了" },
                { "“{0}”已安装成功。\r\n\r\n安装目录：{1}\r\n\r\n现在可以右键点击文件或文件夹，选择“{0}”。\r\nWindows 11 请在“显示更多选项”中查看。", "「{0}」のインストールが完了しました。\r\n\r\nインストール先：{1}\r\n\r\nファイルまたはフォルダーを右クリックして「{0}」を選択できます。\r\nWindows 11 では「その他のオプションを表示」から確認してください。" },
                { "安装成功", "インストール成功" },
                { "安装失败", "インストール失敗" },
                { "安装目录不能是磁盘根目录。", "インストール先をドライブのルートにすることはできません。" },
                { "主程序释放失败，安装包可能不完整。", "メインプログラムの展開に失敗しました。インストーラーが不完全な可能性があります。" },
                { "安装失败：", "インストールに失敗しました：" },
                { "无法完成卸载：", "アンインストールを完了できません：" },
                { "卸载信息已移除，但安装目录删除失败：", "アンインストール情報は削除されましたが、インストール先を削除できませんでした：" },
                { "安装包内缺少资源：", "インストーラーにリソースがありません：" },
                { "系统未提供 WScript.Shell。", "このシステムでは WScript.Shell を利用できません。" },
                { "检测并结束占用文件的进程", "使用中のファイルのプロセスを検出して終了" },
                { "程序遇到错误：", "プログラムでエラーが発生しました：" },
                { "未知错误", "不明なエラー" },
                { "卸载解除占用", "占有を解除のアンインストール" },
                { "确定要卸载“解除占用”吗？\r\n\r\n将删除程序文件、右键菜单和快捷方式。", "「占有を解除」をアンインストールしますか？\r\n\r\nプログラムファイル、右クリックメニュー、ショートカットを削除します。" },
                { "卸载失败：", "アンインストールに失敗しました：" },
                { "卸载失败", "アンインストール失敗" },
                { "日志：", "ログ：" },
                { "界面语言", "言語" },
                { "切换界面语言", "表示言語を切り替える" },
                { "是", "はい" },
                { "否", "いいえ" },
                { "确定", "OK" }
            };

        private static readonly Dictionary<string, string> Korean =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "解除占用", "점유 해제" },
                { "解除占用 安装程序", "점유 해제 설치 프로그램" },
                { "一键检测并结束正在占用文件或文件夹的进程", "파일이나 폴더를 사용 중인 프로세스를 찾아 종료합니다" },
                { "安装程序  v{0}", "설치 프로그램  v{0}" },
                { "安装目录", "설치 폴더" },
                { "浏览...", "찾아보기..." },
                { "创建桌面快捷方式", "바탕 화면 바로 가기 만들기" },
                { "安装后将添加到文件、文件夹的右键菜单中。\r\nWindows 11 若未直接显示，请点击右键菜单底部的“显示更多选项”。", "설치 후 파일 및 폴더의 마우스 오른쪽 버튼 메뉴에 추가됩니다.\r\nWindows 11에서 바로 표시되지 않으면 마우스 오른쪽 버튼 메뉴 아래의 “추가 옵션 표시”를 클릭하세요." },
                { "将安装以下内容", "다음 항목이 설치됩니다" },
                { "•  主程序与卸载程序（约 0.3 MB）\r\n•  文件 / 文件夹右键菜单“{0}”\r\n•  开始菜单快捷方式与系统卸载信息", "•  기본 프로그램 및 제거 프로그램(약 0.3 MB)\r\n•  파일 / 폴더 마우스 오른쪽 버튼 메뉴 “{0}”\r\n•  시작 메뉴 바로 가기 및 제거 정보" },
                { "准备安装", "설치 준비 완료" },
                { "取消", "취소" },
                { "安装", "설치" },
                { "选择解除占用的安装目录", "설치 폴더 선택" },
                { "无法选择目录", "폴더를 선택할 수 없습니다" },
                { "安装目录无效", "설치 폴더가 올바르지 않습니다" },
                { "正在安装，请稍候...", "설치 중입니다. 잠시만 기다려 주세요..." },
                { "安装完成", "설치 완료" },
                { "“{0}”已安装成功。\r\n\r\n安装目录：{1}\r\n\r\n现在可以右键点击文件或文件夹，选择“{0}”。\r\nWindows 11 请在“显示更多选项”中查看。", "“{0}”이(가) 설치되었습니다.\r\n\r\n설치 폴더: {1}\r\n\r\n이제 파일이나 폴더를 마우스 오른쪽 버튼으로 클릭하고 “{0}”을(를) 선택할 수 있습니다.\r\nWindows 11에서는 “추가 옵션 표시”에서 확인하세요." },
                { "安装成功", "설치 성공" },
                { "安装失败", "설치 실패" },
                { "安装目录不能是磁盘根目录。", "설치 폴더는 드라이브 루트일 수 없습니다." },
                { "主程序释放失败，安装包可能不完整。", "기본 프로그램을 추출하지 못했습니다. 설치 파일이 불완전할 수 있습니다." },
                { "安装失败：", "설치 실패: " },
                { "无法完成卸载：", "제거를 완료할 수 없습니다: " },
                { "卸载信息已移除，但安装目录删除失败：", "제거 정보는 삭제되었지만 설치 폴더를 삭제하지 못했습니다: " },
                { "安装包内缺少资源：", "설치 파일에 리소스가 없습니다: " },
                { "系统未提供 WScript.Shell。", "이 시스템에서는 WScript.Shell을 사용할 수 없습니다." },
                { "检测并结束占用文件的进程", "점유된 파일을 사용하는 프로세스를 찾아 종료" },
                { "程序遇到错误：", "프로그램에서 오류가 발생했습니다: " },
                { "未知错误", "알 수 없는 오류" },
                { "卸载解除占用", "점유 해제 제거" },
                { "确定要卸载“解除占用”吗？\r\n\r\n将删除程序文件、右键菜单和快捷方式。", "“점유 해제”를 제거하시겠습니까?\r\n\r\n프로그램 파일, 마우스 오른쪽 버튼 메뉴, 바로 가기가 삭제됩니다." },
                { "卸载失败：", "제거 실패: " },
                { "卸载失败", "제거 실패" },
                { "日志：", "로그: " },
                { "界面语言", "언어" },
                { "切换界面语言", "인터페이스 언어 변경" },
                { "是", "예" },
                { "否", "아니요" },
                { "确定", "확인" }
            };

        internal static UiLanguage Language
        {
            get { return LanguageSettings.Current; }
        }

        internal static string T(string chinese)
        {
            if (string.IsNullOrEmpty(chinese))
            {
                return chinese ?? string.Empty;
            }

            UiLanguage language = LanguageSettings.Current;
            if (language == UiLanguage.Chinese)
            {
                return chinese;
            }

            Dictionary<string, string> table = language == UiLanguage.Japanese
                ? Japanese
                : language == UiLanguage.Korean
                    ? Korean
                    : English;

            string translated;
            return table.TryGetValue(chinese, out translated) ? translated : chinese;
        }

        internal static string T(string chinese, params object[] args)
        {
            return string.Format(T(chinese), args);
        }

        internal static void SetLanguage(UiLanguage language)
        {
            LanguageSettings.Save(language);
            LanguageSettings.UpdateContextMenuLabel(MenuLabel);
        }

        internal static string MenuLabel
        {
            get { return T("解除占用"); }
        }

        internal static string[] NativeNames
        {
            get { return LanguageSettings.NativeNames; }
        }
    }
}
