# QA 经验

## 在 Linux 验证 ExclusiveTouch fallback

- 直接用不存在的 VID/PID 调用 `ExclusiveTouchHost.StartDevices` 不可行：项目内的 LibUsbDotNet 会先调用 Windows `Kernel32.CreateFile`，在 Mono/Linux 下抛出 `EntryPointNotFoundException`，此时尚未进入待测的 fallback 分支。
- 验证候选遍历时，可复制构建产物到临时目录，用 Mono.Cecil 仅在临时 DLL 中将 `ExclusiveTouchBase.Start()` 替换为固定返回 `false`，再用驱动断言所有候选工厂均被调用。
- 不要改写 `Output` 中的正式构建产物；验证结束后删除临时 DLL 和驱动。
