## 2026-02-25 - Taskbar Progress for Batch Operations
**Learning:** For desktop applications handling long-running batch processes, users often minimize the window. Providing progress and status (Normal/Paused/Error) via `TaskbarItemInfo` allows monitoring without focus.
**Action:** Always implement `TaskbarItemInfo` bindings in WPF ViewModels for batch processing apps.
