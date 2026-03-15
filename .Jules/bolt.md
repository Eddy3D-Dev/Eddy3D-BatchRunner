## 2026-03-06 - [Avoid JSON Serialization on Every Key Press]
**Learning:** In WPF applications utilizing `DispatcherTimer` for debouncing, complex `INotifyPropertyChanged` operations like saving state to disk via JSON serialization shouldn't block the UI thread on high-frequency properties.
**Action:** Always check the memory context for debouncer information, and verify state persistence logic doesn't unnecessarily block UI rendering.

## 2024-05-20 - Process Enumeration Optimization
**Learning:** `Process.GetProcessesByName()` scans the entire OS process list. Calling it in a loop for `N` different target process names results in O(N) full system scans, which creates significant CPU overhead when run frequently on a timer (e.g., a background watchdog).
**Action:** Replace multiple `GetProcessesByName()` calls with a single `Process.GetProcesses()` call. Iterate the resulting process array once and check `p.ProcessName` against a `HashSet<string>` (with `OrdinalIgnoreCase`). Always ensure the `Process` objects are disposed in a `finally` block to prevent handle/memory leaks.

## 2024-11-20 - Replace File.ReadAllLines with File.ReadLines
**Learning:** Large scripts, dictionary files, or log files can allocate significant arrays of strings when parsed with File.ReadAllLines, placing unnecessary pressure on the Garbage Collector.
**Action:** Always prefer `File.ReadLines` when streaming/processing text files line-by-line where possible. This is especially true for tasks that might only need to read a small portion of a file before exiting the loop, or for tasks running periodically on background timers.

## 2026-03-08 - [Avoid O(N) LINQ in UI Timers]
**Learning:** In applications with a large queue of items (like jobs in folders), executing LINQ operations like `SelectMany` + `Where` + `Sum` on every timer tick (e.g. 1-second background refresh) creates an O(N) performance bottleneck and continuous GC allocations.
**Action:** Always prefer caching the active/running subset of items in a dictionary or list, so that high-frequency background timers and property getters run in O(R) time (where R is the small subset of running items).

## 2026-03-08 - [Avoid Multiple O(N) Passes for Aggregation]
**Learning:** Extracting counts (`.Count()`) sequentially using LINQ predicates causes multiple O(N) enumerations over a collection. Additionally, `.ToList()` allocations for local lists that are just counted adds memory overhead.
**Action:** Use a single loop to compute multiple aggregates, avoiding extra memory allocation and optimizing iteration paths when dealing with high-frequency property changes updating UI components.

## 2024-06-25 - [Async State Serialization Thread Safety]
**Learning:** Offloading state persistence to `File.WriteAllTextAsync` via `Task.Run` improves UI responsiveness, but if the state relies on UI components like `ObservableCollection`, serializing inside the background task will throw an `InvalidOperationException` due to concurrent UI updates.
**Action:** Serialize the state synchronously on the UI thread to freeze the snapshot, then offload only the disk write (`File.WriteAllTextAsync`) to a background task using `SemaphoreSlim` for synchronization. Also, retain a synchronous save fallback for application shutdown to ensure completion.
