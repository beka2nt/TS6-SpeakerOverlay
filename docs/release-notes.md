## v1.6.0 — 更新检查与多语言 / Updates and languages

### 中文

- 新增 GitHub 最新稳定版检查：设置页和托盘菜单均可手动检查，默认启动时后台检查一次，可在设置中关闭。
- 显示当前版本、检查中、已是最新、发现新版、网络失败和 GitHub 请求限流状态；重复点击不会发起并行检查，退出时取消正在进行的请求。
- 发现新版后提供官方 EXE 下载入口，在浏览器中下载；附件尚未就绪时打开 Release 页面。不静默覆盖、安装或执行下载文件。
- 设置顶部常驻地球图标与 **Language / 语言** 入口，语言名称始终使用各自原生名称；托盘也增加语言入口。
- 完整新增 **한국어（韩语）、Deutsch（德语）、Español（西班牙语）**，现在支持七种语言。切换立即生效，菜单和更新状态同步变化。
- 托盘菜单使用统一图标栏、文字起点和行中心，按 DPI 计算尺寸；高分辨率自绘图标减少锯齿，长翻译不再挤压裁剪。
- 扩展离线回归检查，涵盖版本比较、下载链接校验、请求失败/取消、语言资源与占位符、顶部语言入口、托盘实际绘制及多 DPI 布局。

**验证：** 本地回归检查和 Release 构建通过；更新检查已连接真实 GitHub API 验证。测试不连接真实 TeamSpeak，也不读取个人 API 密钥。保留 v1.5.1 的人员状态修复，Issue #10 继续等待实际环境反馈。

**使用：** 下载下方 `TS6-SpeakerOverlay.exe`，先退出旧版再运行。Windows x64 自包含单文件，无需额外安装 .NET；个人配置与授权密钥不会随发布上传。

### English

- Check this repository's latest stable GitHub Release from settings or the tray. A quiet startup check is enabled by default and can be disabled.
- Show current/checking/up-to-date/available/error/rate-limit states. Prevent parallel checks and cancel pending requests at shutdown.
- Open the official EXE download in your browser, falling back to the release page while an asset is uploading. No silent replacement, installation or execution.
- Keep the globe-marked **Language / 语言** selector fixed at the top, using native language names. Add the same language entry to the tray.
- Add complete **Korean, German and Spanish** localization, bringing the total to seven languages. Menus and update status change immediately with the selected language.
- Align tray icons and text using shared DPI-aware columns and row centers; use high-resolution glyphs and reserve enough room for long translations.
- Extend offline regression checks for versions, trusted links, failures/cancellation, localization completeness/placeholders, selector placement and tray rendering/DPI layouts.

**Validation:** Local regression checks and the Release build passed. The update checker was also verified against the live GitHub API. Fixtures never connect to TeamSpeak or load personal API keys. The v1.5.1 member-state fixes are retained; issue #10 still awaits feedback from the reporter's environment.

**Installation:** Download `TS6-SpeakerOverlay.exe` below, exit the previous version and run the new executable. This is a self-contained Windows x64 build; no separate .NET installation is required. Personal settings and API keys are not uploaded.
