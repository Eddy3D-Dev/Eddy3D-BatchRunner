## 2026-03-06 - [Avoid JSON Serialization on Every Key Press]
**Learning:** In WPF applications utilizing `DispatcherTimer` for debouncing, complex `INotifyPropertyChanged` operations like saving state to disk via JSON serialization shouldn't block the UI thread on high-frequency properties.
**Action:** Always check the memory context for debouncer information, and verify state persistence logic doesn't unnecessarily block UI rendering.
