# QA 经验

## 在 Linux 验证 ExclusiveTouch fallback

- 直接用不存在的 VID/PID 调用 `ExclusiveTouchHost.StartDevices` 不可行：项目内的 LibUsbDotNet 会先调用 Windows `Kernel32.CreateFile`，在 Mono/Linux 下抛出 `EntryPointNotFoundException`，此时尚未进入待测的 fallback 分支。
- 验证候选遍历时，可复制构建产物到临时目录，用 Mono.Cecil 仅在临时 DLL 中将 `ExclusiveTouchBase.Start()` 替换为固定返回 `false`，再用驱动断言所有候选工厂均被调用。
- 不要改写 `Output` 中的正式构建产物；验证结束后删除临时 DLL 和驱动。

## 在 Linux 无硬件验证 FL 拆帧重连

- 用 Mono 反射加载构建产物时，`MONO_PATH` 只包含 `Output`；加入 `Libs` 会优先加载游戏自带的 `mscorlib.dll`，导致 corlib 版本冲突。
- 不连接 USB 而直接实例化私有 `FlTouchDevice` 时，需反射注入 `touchSensorMapper` 并初始化 `allFingerPoints`，因为这些状态通常由 `Start()` 创建。
- 先发送 `count > 6` 的半帧，再模拟断开/连接并发送 `count == 0` 的续帧；断言旧流程会提交活动手指，而重置后的流程不会提交。
