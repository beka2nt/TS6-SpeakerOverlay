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
