## 2026-03-06 - [Avoid JSON Serialization on Every Key Press]
**Learning:** In WPF applications utilizing `DispatcherTimer` for debouncing, complex `INotifyPropertyChanged` operations like saving state to disk via JSON serialization shouldn't block the UI thread on high-frequency properties.
**Action:** Always check the memory context for debouncer information, and verify state persistence logic doesn't unnecessarily block UI rendering.

## 2024-05-20 - Process Enumeration Optimization
**Learning:** `Process.GetProcessesByName()` scans the entire OS process list. Calling it in a loop for `N` different target process names results in O(N) full system scans, which creates significant CPU overhead when run frequently on a timer (e.g., a background watchdog).
**Action:** Replace multiple `GetProcessesByName()` calls with a single `Process.GetProcesses()` call. Iterate the resulting process array once and check `p.ProcessName` against a `HashSet<string>` (with `OrdinalIgnoreCase`). Always ensure the `Process` objects are disposed in a `finally` block to prevent handle/memory leaks.
