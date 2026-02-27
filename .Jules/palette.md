## 2026-02-25 - Taskbar Progress for Batch Operations
**Learning:** For desktop applications handling long-running batch processes, users often minimize the window. Providing progress and status (Normal/Paused/Error) via `TaskbarItemInfo` allows monitoring without focus.
**Action:** Always implement `TaskbarItemInfo` bindings in WPF ViewModels for batch processing apps.

## 2026-02-26 - Clickable Log Paths in Desktop Apps
**Learning:** In applications that generate log files for batch processes, users expect to inspect failures immediately. Providing a clickable hyperlink in the status grid to open the log file directly is significantly more useful than displaying just the file path, and requires minimal implementation effort.
**Action:** Replace read-only log path columns with hyperlink templates bound to a file-opening command.

## 2026-02-26 - Keyboard Accessibility in WPF
**Learning:** Adding `IsKeyboardFocused` triggers to control templates ensures keyboard users receive the same visual feedback as mouse users, which is critical for accessibility but often overlooked in default styles.
**Action:** Include `IsKeyboardFocused` triggers sharing the `IsMouseOver` visual state in all custom control templates.
