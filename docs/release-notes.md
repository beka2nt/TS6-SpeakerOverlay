## v1.7.6 — 自动识别端口 / Automatic port discovery

- 默认自动识别本机 TeamSpeak 的远程应用端口，收到有效快照后记忆；旧配置保留原端口，连接中断后约每 5 秒重新检测。
- 只连接 TeamSpeak 进程拥有的本机监听，连接建立后再次检查归属；多个候选无法确定时提示手动设置，不扫描其他应用。
- 保留手动模式，支持 1–65535 任意有效端口。修改端口或识别模式后立即重连，无需重启；旧连接的迟到消息不会覆盖新状态。
- 设置页新增当前连接端口、重新检测和七语言诊断。等待授权时保留连接，授权被拒后暂停自动重试。
- TS 未启用远程应用或因 Windows 保留端口绑定失败时，仍需在 TS 内处理；悬浮窗不会修改 TS、Windows 或其他插件设置。
- Automatically discover local TS-owned Remote Apps listeners, remember verified ports, and reconnect immediately after manual port/mode changes. Add localized diagnostics and retain manual fallback.
- 验证：758 项回归检查通过；原生 IPv4/IPv6 监听与模拟会话测试不读取真实密钥、不连接真实 TeamSpeak。七语言设置页已渲染检查，真实 TS 连通性待用户试用。
- 本地自包含 Windows x64 构建；本次未上传 GitHub，也未更改桌面快捷方式。应用内下载并更新仍是独立待实现功能。

## v1.7.5 — 字体搜索 / Font search

- 保留原字体下拉框，在字体标签旁增加搜索框，支持中英文名字、部分名称和不区分大小写筛选。
- 搜索不会改变已应用字体；当前字体始终保留，无匹配时显示提示，清除按钮可恢复完整列表。
- 搜索文字只用于当前界面，不写入配置；选中搜索结果后，预览和浮窗名字即时更新。
- Keep the font dropdown and add search by localized or English family names. Preserve the applied font, show a no-match hint, and clear the query without resetting preferences.
- 包含 v1.7.4 的连接修复，本次不再修改连接逻辑。
- 验证：624 项完整回归检查通过，包括中英文匹配、搜索结果选择、无匹配保留字体、清除搜索及搜索状态下恢复默认。

## v1.7.4 — 名字字体与连接修复 / Name fonts and connection fix

- 在显示外观中新增成员名字字体选择与字号输入框，支持本机字体、10–36 字号和即时预览；保留滑块，设置自动保存。
- 字体仅应用到悬浮窗成员名字，不改变设置、状态提示或头像文字；旧配置继续沿用原字体回退与字号。
- 修复 5 秒没有说话或频道事件就主动断线的问题。取消应用消息静默超时，改用 20 秒 WebSocket 心跳与 10 秒响应超时检测失效连接。
- 真正断开或连接失败后保留 5 秒自动重试；断线日志新增端口、断线类型、关闭状态与异常详情，认证错误仅记录状态码，不记录原始授权消息或密钥。
- Add an installed-font selector and numeric 10–36 size field for member names, with live preview and saved preferences.
- Stop reconnecting solely because the channel is silent. Use protocol-level PING/PONG and retain automatic retries after real failures, with more specific diagnostics.
- 真实 TeamSpeak 连接需用户启动后验证；回归测试使用隔离的本机模拟服务，不读取真实授权密钥。
- 验证：616 项完整回归通过；包含 32 秒静默与心跳窗口、真实关闭后的重连、字体输入校验及实际浮窗绑定。

## v1.7.3 — 小范围界面精修 / Focused UI polish

- 保留浅色风格和五类设置，压缩设置页顶部与页脚留白，让首屏显示更多常用选项。
- 深化按钮与数值文字的绿色，提高文字对比度；统一间距、说明文字、圆角和键盘焦点。
- 设置与日志窗口使用共享样式。日志工具栏支持窄窗口换行，筛选状态更明确。
- 托盘统一浅灰配色、图标线宽和菜单密度，保留现有功能、语音亮灯与七种语言。
- Keep the light theme, five settings categories and all existing behavior. Refine spacing, text contrast and shared window styles; wrap log actions in narrow windows and unify tray styling.
- 本次不修改 TeamSpeak 连接或重连逻辑 / TeamSpeak connection behavior is unchanged.
- 验证：586 项离线回归检查通过，含七语言、共享样式、对比度与日志操作 / 586 offline checks passed.
- 设计流程参考 [unslop](https://github.com/heydamir/unslop)，Damir Sokolovsky / @heydamir，CC BY 4.0；适配原生 WPF/WinForms。

## v1.7.2 — 设置分类优化 / Organized settings

- 设置重组为显示外观、排列定位、进出通知、连接服务器、应用与日志五个分类，默认打开常用外观设置。
- 将只显示说话者与显示模式放在一起；列表间距和整体缩放归入定位；通知总开关与服务器/频道选项集中显示。
- 服务器选择、远程应用端口、连接状态和刷新统一放在连接分类；启动方式、更新、日志集中在应用分类。
- 顶部保留固定语言入口，右侧仅保留实时预览与自动保存说明。每个分类保留独立滚动位置，切换分类和语言不重置设置。
- 为七种语言补齐分类标题与说明，支持长文本换行和键盘焦点提示；将自动保存后的关闭按钮改为“完成”。
- Organize settings into Display & appearance, Layout & position, Notifications, Connection, and App & logs. Each category retains its scroll position and live preferences.
- Keep language selection fixed at the top and reserve the right rail for preview only. Localize category labels and help text in all seven languages.

**验证 / Validation:** 467 项本地回归检查通过，涵盖七种语言的分类导航、控件归属、通知依赖、颜色预设、刷新动作及独立滚动；不连接 TeamSpeak。467 local regression checks passed without connecting to TeamSpeak.

## v1.7.1 — Shift 锁轴修复 / Shift axis-lock fix

- 拖动期间直接读取左右 Shift 的实时按下状态，不再依赖悬浮窗的键盘焦点；鼠标静止时仍检测松键。
- 按下或松开 Shift 时，以窗口当前位置重新建立拖动起点，忽略锁轴期间积累的垂直方向偏移。
- 修复靠近边缘松键时的额外吸附：刚释放的轴只有实际移动后才参与吸附。
- 补充不可见真实窗口的横向、纵向、斜向输入、松键续拖与边缘吸附回归测试。
- Sample physical left/right Shift state throughout dragging, including while the pointer is stationary and the overlay lacks keyboard focus.
- Rebase from the actual window position on modifier changes; never apply accumulated perpendicular cursor offsets or snap a newly released axis before it moves.

**验证 / Validation:** 180 项本地回归检查通过，包括无键盘焦点的真实窗口拖动流程；不连接 TeamSpeak。180 local checks passed, including real-window drag-controller checks without keyboard focus. No TeamSpeak connection was opened.

## v1.7.0 — 对齐与精确定位 / Alignment and precise positioning

### 中文

- 用户列表支持顶部 Top、居中 Center、底部 Bottom。人数变化、静默成员隐藏或锁定提示切换时，居中名单保持稳定。
- 新增八方向快速定位：四角与四条边的中心。预设自动设置列表纵向与横向对齐，右侧预设将实际成员卡片靠右。
- 在悬浮窗当前屏幕的可用区域定位，避开任务栏；支持多屏负坐标和 Windows 显示缩放。
- 边缘间距可在 0–80 个逻辑像素内调整，默认 16；选中预设后修改间距立即生效。
- 新增可关闭的边缘吸附，拖动到边缘附近会保留设定间距；使用绝对鼠标位移，避免吸附引起累计漂移。
- 按住 Shift，根据首段移动方向锁定横轴或纵轴；中途按下、松开不会跳位，另一方向的精确间距保持不变。
- 手动拖动后取消预设绑定，保留对齐方式。位置、对齐、吸附与间距自动保存。
- 通知与拖动提示不再占用名单布局空间，底部对齐时提示移到顶部，避免遮挡最后一位成员。
- 新设置沿用浅色风格，提供八方向选择器与实时对齐预览，七种语言均补齐相应文本。

**验证：** 168 项本地回归检查通过，包括不可见真实窗口的定位、任务栏边界、间距调整、实际 WPF 名单对齐和原有功能；测试不连接 TeamSpeak。

**试用：** 先从托盘退出旧版，再运行新版 EXE。在“设置 → 排列与定位”选择位置与间距。Windows x64 自包含程序，无需单独安装 .NET。

### English

- Add Top, Center and Bottom member alignment. Centered lists remain stable as members leave, silent rows hide or lock hints change.
- Add eight corner/edge-center presets with automatic vertical and horizontal alignment, including right-aligned member cards.
- Target the overlay's current monitor work area, excluding the taskbar; support negative monitor coordinates and Windows display scaling.
- Add a configurable 0–80 logical-pixel edge gap (default 16), applied immediately while a preset is selected.
- Add optional edge snapping. Absolute cursor displacement avoids accumulated drift during snapping.
- Hold Shift to lock the first horizontal or vertical direction. Pressing/releasing Shift mid-drag does not jump or alter the frozen coordinate.
- Manual dragging detaches the preset while retaining alignment. Position, alignment, snapping and gap are saved.
- Keep notifications and drag guides outside list layout calculations. Move the unlocked guide above bottom-aligned members to avoid obscuring them.
- Add a light-theme eight-direction selector, live alignment preview and localized controls in all seven languages.

**Validation:** 168 local regression checks passed, including real but invisible native window positioning, work-area boundaries, gap changes, actual WPF list alignment and existing features. Fixtures do not connect to TeamSpeak.

**Try it:** Exit the previous version from the tray before running the new EXE. Open settings → Layout and position. Self-contained Windows x64 build; no separate .NET installation required.
