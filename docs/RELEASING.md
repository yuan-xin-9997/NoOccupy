# Release guide / 发布指南

## Prepare / 准备版本

1. Update `<Version>` in `Directory.Build.props` using `X.Y.Z` (for example, `1.0.1`). Both assemblies, the installer UI, and the Windows uninstall entry use this version.
2. Add a dated entry to `CHANGELOG.md`. Keep `README.md` and `README.cn.md` in sync when behavior changes.
3. Build and verify on Windows:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-release.ps1
```

更新唯一版本号、更新日志及中英文说明，再运行上述命令验证产物。主程序、安装器界面和 Windows 卸载项共用此版本号。

## Publish / 发布

Stage the intended source, docs, screenshots, and rebuilt root installer, review the staged files, then push the commit and a matching annotated tag:

```powershell
git diff --cached --stat
git commit -m "release: v1.0.1"
git push origin main
git tag -a v1.0.1 -m "NoOccupy v1.0.1"
git push origin v1.0.1
```

Use your actual version in place of `1.0.1`; tags must match `vX.Y.Z` exactly. Never move an already published tag to a different commit.

先暂存本次需要发布的源码、文档、截图和重新构建的根目录安装包，检查后再执行提交和推送。将示例版本换成实际版本，不要移动已发布的标签。

The [Release workflow](../.github/workflows/release.yml) runs on Windows and:

1. Rejects tags that differ from `Directory.Build.props`.
2. Builds the application and installer from the tagged source using .NET SDK 8.
3. Verifies assembly versions, embedded app/config payloads, and SHA-256 checksums.
4. Keeps a workflow artifact and publishes a GitHub Release with these assets:

| Asset | Purpose / 用途 |
| --- | --- |
| `NoOccupy-Setup.exe` | Windows installer / Windows 安装包 |
| `SHA256SUMS.txt` | Installer checksum / 安装包 SHA-256 校验值 |

The workflow uses the built-in `GITHUB_TOKEN` with `contents: write`; no personal token secret is needed. Release notes include installation instructions and GitHub-generated changes. Add a human-written summary from the changelog when needed.

工作流使用 GitHub 内置令牌发布，无需配置个人令牌。自动发布说明包含安装提示和 GitHub 生成的变更列表，需要时可补充更新日志中的版本摘要。

## Verify / 验证发布

- Confirm the [workflow run](https://github.com/yuan-xin-9997/NoOccupy/actions/workflows/release.yml) succeeded.
- Open [Releases](https://github.com/yuan-xin-9997/NoOccupy/releases) and confirm both assets exist.
- Download the installer and compare `Get-FileHash -Algorithm SHA256` with `SHA256SUMS.txt`.
- Check the README's [latest EXE link](https://github.com/yuan-xin-9997/NoOccupy/releases/latest/download/NoOccupy-Setup.exe).

确认工作流成功、Release 包含两个附件、下载文件校验值匹配，且 README 的最新安装包链接正常。私有仓库的 Release 与附件需要仓库访问权限。

## Build without publishing / 仅验证构建

Use **Actions → Release → Run workflow** on the desired branch. Manual runs build, verify, and upload artifacts; they do not create a Release. Published releases are triggered only by pushing a tag.

在 Actions 中手动运行 Release 工作流，可验证指定分支并下载构建产物。手动运行不会创建 Release；正式发布由推送标签触发。
