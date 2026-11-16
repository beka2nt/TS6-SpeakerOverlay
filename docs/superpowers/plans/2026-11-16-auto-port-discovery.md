# TS6 Auto Port Discovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Discover TS Remote Apps ports automatically and reconnect immediately when endpoint settings change, without modifying TS or Windows settings.

**Architecture:** Keep protocol state inside one-endpoint `Ts6Service` instances. Add a Windows endpoint discovery service and a connection manager exposing the same protocol events, replacing sessions with generation-checked forwarding. MainViewModel uses the manager by default while existing tests can continue injecting a direct Ts6Service.

**Tech Stack:** .NET 10, WPF/WinForms, existing Websocket.Client 5.3.0, Windows IP Helper API, existing console regression runner.

**Spec:** `docs/superpowers/specs/2026-11-16-auto-port-discovery-design.md`

## Global Constraints

- 原生 Windows WPF/WinForms、.NET 10；不增加第三方运行依赖。
- 检测间隔 5 秒；健康连接不反复检测；连接目标仅限本机回环地址。
- 不修改 TS 配置、授权开关、防火墙、Windows 保留端口或其他远程插件；不删除密钥。
- 保留字体搜索及既有功能，保留 20 秒心跳 / 10 秒响应超时和现有直接 Ts6Service 测试。
- Preserve all existing uncommitted work. Before execution use using-git-worktrees; isolation must contain the current v1.7.5 working baseline, not merely the older HEAD. Do not reset or discard the original checkout.
- No GitHub push, real-key tests or desktop shortcut changes are part of this plan.

## Review Focus

1. A remembered port is now owned by a different process: never send automatic authentication there; Task 1 filters owners and Task 2 rechecks after handshake.
2. Multiple TS listeners or unavailable owner inspection: do not guess or fall back to scanning arbitrary applications; Task 1 returns explicit results, Task 2 exposes manual fallback.
3. Authorization has not completed or was rejected: do not save a candidate or flood auth retries; Task 2 waits for snapshots and pauses on rejection.
4. A previous session returns data after a mode/port change or disposal: do not update users, status or saved port; Task 2 guards all forwarded events by session generation.
5. IPv6/wildcard listeners and a custom manual port outside the four old choices: normalize to loopback and allow validated 1–65535 input without restart; Tasks 1 and 3 pin this behavior.

## File responsibilities

- `Services/Ts6PortDiscovery.cs`: endpoint/result records, discovery interface and TS PID filtering.
- `Helpers/WindowsTcpListenerTable.cs`: IPv4/IPv6 IP Helper interop, network-order port conversion and native-buffer disposal.
- `Services/ITs6Connection.cs`: existing protocol event contract plus auth rejection.
- `Services/Ts6ConnectionManager.cs`: detection, current session, retry, cancellation, verified endpoint and discovery state.
- `Services/Ts6Service.cs`: implement the contract, support a supplied endpoint and disabled internal reconnect for managed sessions; keep existing protocol handling.
- `Models/AppConfig.cs`, `ViewModels/MainViewModel.cs`: mode persistence, UI state, successful-port persistence and command wiring.
- `Views/SettingsWindow.xaml`, seven language dictionaries: automatic/manual port controls and localized status.
- Regression tests: endpoint discovery, manager lifecycle, settings binding and existing connection/roster coverage.

---

### Task 1: Discover TS-owned loopback endpoints

**Files:** Create `Services/Ts6PortDiscovery.cs`, `Helpers/WindowsTcpListenerTable.cs`, `tests/TS6-SpeakerOverlay.RegressionTests/PortDiscoveryChecks.cs`; modify `tests/TS6-SpeakerOverlay.RegressionTests/Program.cs`.

**Interfaces:**
- `internal sealed record Ts6Endpoint(string Host, int Port, int ProcessId)`; Host is `127.0.0.1` or `::1`; `Uri ToUri()` builds a ws URI including IPv6 brackets correctly.
- `internal sealed record Ts6DiscoveryResult(bool IsTeamSpeakRunning, bool InspectionSucceeded, IReadOnlyList<Ts6Endpoint> Candidates)`.
- `internal interface ITs6PortDiscovery`: `Task<Ts6DiscoveryResult> DiscoverAsync(CancellationToken token)` and `bool IsOwnedByTeamSpeak(Ts6Endpoint endpoint)`.
- `internal readonly record struct TcpListenerOwner(IPAddress Address, int Port, int ProcessId)`; `WindowsTcpListenerTable.Read()` returns `IReadOnlyList<TcpListenerOwner>`.
- `WindowsTs6PortDiscovery(Func<IReadOnlyList<int>>? processIds = null, Func<IReadOnlyList<TcpListenerOwner>>? listeners = null)` implements the interface. Defaults read TeamSpeak processes and the Windows listener table; injected delegates are used only by fixtures.

- [ ] Write tests asserting: only supplied TS PIDs are retained; unrelated owners are excluded even on remembered ports; loopback and Any/IPv6Any normalize correctly; duplicate listeners collapse; missing processes and inspection failure remain distinct; invalid ports and non-local specific addresses are excluded.
- [ ] Add `--check-auto-port` runner dispatch and run it; record the intended missing-feature failure before implementation.
- [ ] Implement discovery using `GetExtendedTcpTable` with owner-PID listener classes for IPv4 and IPv6. Dispose process/native resources, handle changed buffer size with bounded retry, and return an inspection failure rather than an empty-success result when lookup fails.
- [ ] Add a native smoke test with a temporary port-0 listener and an injected current PID; assert discovery returns the real assigned port for IPv4 and IPv6 when supported. No TeamSpeak socket or key is used.
- [ ] Run `dotnet run --project tests/TS6-SpeakerOverlay.RegressionTests -c Release -- --check-auto-port`; require discovery tests to pass.
- [ ] Commit this independently tested discovery task with only its owned files.

### Task 2: Manage endpoint selection and session lifetime

**Files:** Create `Services/ITs6Connection.cs`, `Services/Ts6ConnectionManager.cs`, `tests/TS6-SpeakerOverlay.RegressionTests/AutoPortConnectionChecks.cs`; modify `Services/Ts6Service.cs` and the runner.

**Interfaces:**
- `ITs6Connection : IDisposable` exposes `Task StartAsync()`, `bool SendAuth()`, and the existing Ts6Service events `OnServersUpdated`, `OnSnapshotReceived`, `OnTalkStatusChanged`, `OnUserPropertiesChanged`, `OnClientMoved`, `OnServerMembershipChanged`, `OnConnectionStateChanged` with their current signatures. Add `event Action<int>? OnAuthenticationRejected`.
- Preserve both existing Ts6Service int-port constructors. Add `internal Ts6Service(Ts6Endpoint endpoint, bool loadApiKey, bool autoReconnect)`; managed production sessions pass true/false respectively for key loading/internal reconnect. The true transport event remains before SendAuth, allowing the manager to reject changed ownership synchronously before any key is sent.
- `internal enum Ts6PortState`: `WaitingForTeamSpeak`, `Searching`, `Connecting`, `Connected`, `NoRemoteAppListener`, `AmbiguousCandidates`, `AuthorizationRequired`, `InspectionFailed`.
- `internal Ts6ConnectionManager(ITs6PortDiscovery discovery, Func<Ts6Endpoint, ITs6Connection> createConnection, bool automatic, int configuredPort, TimeSpan? detectionInterval = null)` implements ITs6Connection; production interval is 5 seconds.
- Manager produces `event Action<Ts6Endpoint>? OnEndpointVerified`, `event Action<Ts6PortState>? OnPortStateChanged`, `int? ActivePort`, `Task ConfigureAsync(bool automatic, int port)`, and `Task DetectNowAsync()`.

- [ ] Add fake-discovery and fake-connection tests for remembered-port priority, unique new candidate, no candidates, multiple candidates, failed inspection, non-TS owner and manual mode. Assert no automatic connection is created for an unknown owner or ambiguous candidates.
- [ ] Add tests for: owner changes during handshake (dispose before SendAuth); auth snapshot required before OnEndpointVerified; auth rejection pauses retries; an authorization-pending healthy socket is retained; a healthy unchanged endpoint is not re-created, but a disconnected session is retried even when the chosen port is unchanged.
- [ ] Add tests for A→B→C changes with delayed A events, changing mode during discovery, disposal during startup and no activity after disposal. Assert only current-session events reach consumers and only C can be persisted.
- [ ] Run `--check-auto-port` and record the missing manager/lifecycle failures.
- [ ] Implement the manager with one current session and generation-checked forwarding. Stop the previous session before starting a different one. Revalidate owner in the auto-mode transport-connected callback before the service's auth send; reject by disposing the session. Only a valid current snapshot emits OnEndpointVerified and marks Connected.
- [ ] Select the remembered port only from trusted current candidates; otherwise accept a unique candidate. Multiple unresolved candidates remain explicit manual fallback, never an auth sweep. On failures schedule one detection loop at 5 seconds; a healthy or authorization-pending session is not churned. A rejected authorization disposes that managed session and waits for explicit retry/configuration change.
- [ ] Keep cache/movement/talk logic in Ts6Service unchanged. Emit OnAuthenticationRejected at the existing nonzero auth-status branch. Direct Ts6Service construction retains native automatic retries; managed instances disable that inner retry so discovery owns reconnection and ownership checking.
- [ ] Run `--check-auto-port` and `--check-connection`; require manager fixtures and the existing 32-second heartbeat lifecycle checks to pass.
- [ ] Commit the independently tested connection-management task.

### Task 3: Expose automatic/manual mode, persist success and deliver

**Files:** Modify `Models/AppConfig.cs`, `ViewModels/MainViewModel.cs`, `Views/SettingsWindow.xaml`, `Resources/Styles/UtilityWindowTheme.xaml` only if required for validation, all seven `Resources/Languages/*.xaml`, existing settings tests, README, release notes and project version.

**Interfaces:**
- Config adds observable `bool AutoDetectRemoteAppPort = true`. Retain `RemoteAppPort` as the remembered/specified port.
- MainViewModel's injected connection parameter and field become ITs6Connection-compatible; existing callers supplying Ts6Service remain valid. Production creates a manager, injecting discovery and a Ts6Service factory.
- Expose `string PortDetectionStatusText`, `int? ActiveRemoteAppPort`, and `DetectRemoteAppCommand`. Config mode/manual-port changes call ConfigureAsync; a verified endpoint is saved on the UI dispatcher with a feedback guard so saving does not cause another reconnect.
- Reuse the shared text-input style and add a 1–65535 port validation rule. Manual input is disabled in automatic mode. The interface shows detected port/status and a re-detect button; roster refresh keeps its existing separate behavior.

- [ ] Add failing config/UI tests: old JSON enables auto mode without losing 6200; mode persists; manual input accepts custom valid ports and rejects 0/65536; mode/port changes apply without restart; verified-port saving does not loop; language switching preserves mode and settings.
- [ ] Implement VM integration. Route manager diagnostic states to localized connection UI without allowing the old 500ms disconnected handler to overwrite the more specific state. Preserve the direct-service test path and existing refresh/roster behavior.
- [ ] Implement controls in the existing connection category, without reorganizing other settings or changing TS itself. Add all status/control/validation strings to seven dictionaries and remove the obsolete restart-required port description.
- [ ] Update automatic-port tests and settings ownership assertions for the new port input; run `--check-auto-port`, language/settings checks and a real rendered seven-language connection preview.
- [ ] Update README/release notes, bump the local version to 1.7.6, and document that no-listener/system-reserved-port failures still require TS-side action. Do not merge the separate in-app-updater design into this implementation.
- [ ] Run the full regression runner: `dotnet run --project tests/TS6-SpeakerOverlay.RegressionTests -c Release`. Require all existing and new checks to pass without real TS keys.
- [ ] Publish the self-contained win-x64 EXE into `artifacts/v1.7.6`, then copy a verified 1.7.6.0 file to `outputs/TS6-SpeakerOverlay-v1.7.6-win-x64.exe` without overwriting an existing target. Preserve old binaries and shortcuts.
- [ ] Commit this UI/integration task. Follow the agreed execution/review/integration workflow, provide the local EXE and verification results, and state whether live TS verification was performed.

## Plan self-review

- Every approved behavior has an owner above: discovery (Task 1), safe/current lifecycle and retries (Task 2), immediate mode/port application, saving and localized UI (Task 3).
- All five Review Focus conditions have explicit tests in their owning task.
- The discovery/manager/VM signatures use the same Ts6Endpoint, Ts6DiscoveryResult, ITs6Connection and Ts6PortState types throughout.
- No dependency installation, TS configuration edits, broad port scans or unrelated feature changes are required.
