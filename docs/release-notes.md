## v1.5.1 — 人员状态与刷新修复 / State and refresh fixes

### 中文

- 修复延迟返回的人员列表覆盖最近的进出频道状态；保护退出、重新加入和自己切换频道后的状态。
- 修复刷新无法校正说话亮灯的问题，同时避免近期说话事件被旧列表立即覆盖。
- 手动刷新无响应时自动重试一次，约 6 秒后结束等待并显示失败提示；刷新请求、重试和失败会写入日志。
- 修复连续刷新时旧计时器影响新请求的问题，退出软件或断开连接时取消正在等待的手动刷新。
- 四种语言均增加刷新失败提示，并让悬浮窗长状态文字可以换行。
- 新增 25 项离线回归检查，并在发布 EXE 前自动运行。

**验证情况：** 本地 25 项回归检查全部通过，Release 构建零警告、零错误。测试使用模拟消息，不连接真实 TeamSpeak；Issue #10 的用户环境仍需进一步验证，因此保持开放。

**使用方法：** 下载下面的 `TS6-SpeakerOverlay.exe`，先退出旧版再运行。Windows x64 单文件版本，无需另外安装 .NET。个人配置和 API 密钥不包含在下载文件中。

### English

- Protect recent join, leave, rejoin, and channel-move events from delayed member snapshots, including changes to your own channel.
- Let refresh snapshots repair missed speaking-state events without immediately overwriting newer speaking events.
- Retry an unanswered manual refresh once and report failure after approximately six seconds instead of waiting indefinitely; log refresh requests, retries, and failures.
- Prevent cancelled refresh timers from overwriting newer results; cancel pending manual refreshes on disconnect and shutdown.
- Add refresh-failure messages in all four languages and wrap long overlay status messages.
- Add 25 offline regression checks and run them before publishing the executable.

**Validation:** All 25 local regression checks passed, and the Release build completed with no warnings or errors. Fixtures simulate messages without connecting to TeamSpeak. Verification in the reporter's real environment is still needed; issue #10 remains open.

**Installation:** Download `TS6-SpeakerOverlay.exe` below, exit the previous version, and run the new executable. This is a self-contained Windows x64 build; no separate .NET installation is required. Personal settings and API keys are not included.

Related issue: https://github.com/beka2nt/TS6-SpeakerOverlay/issues/10
