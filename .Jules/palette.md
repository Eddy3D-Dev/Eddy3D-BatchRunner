## 2026-02-25 - Taskbar Progress for Batch Operations
**Learning:** For desktop applications handling long-running batch processes, users often minimize the window. Providing progress and status (Normal/Paused/Error) via `TaskbarItemInfo` allows monitoring without focus.
**Action:** Always implement `TaskbarItemInfo` bindings in WPF ViewModels for batch processing apps.

## 2026-02-26 - Clickable Log Paths in Desktop Apps
**Learning:** In applications that generate log files for batch processes, users expect to inspect failures immediately. Providing a clickable hyperlink in the status grid to open the log file directly is significantly more useful than displaying just the file path, and requires minimal implementation effort.
**Action:** Replace read-only log path columns with hyperlink templates bound to a file-opening command.

## 2026-02-26 - Keyboard Accessibility in WPF
**Learning:** Adding `IsKeyboardFocused` triggers to control templates ensures keyboard users receive the same visual feedback as mouse users, which is critical for accessibility but often overlooked in default styles.
**Action:** Include `IsKeyboardFocused` triggers sharing the `IsMouseOver` visual state in all custom control templates.

## 2026-02-28 - Consistent Keyboard Navigation and Tooltips in WPF Settings
**Learning:** While primary action buttons often have access keys (mnemonics), settings checkboxes and input fields are frequently missed. Adding access keys and descriptive tooltips to all interactive elements ensures complete keyboard navigation and improves clarity for users who prefer shortcuts.
**Action:** Consistently apply access keys (via ) and  properties to all interactive  and  elements in WPF dialogs or settings areas.

## 2026-02-27 - Consistent Keyboard Navigation and Tooltips in WPF Settings
**Learning:** While primary action buttons often have access keys (mnemonics), settings checkboxes and input fields are frequently missed. Adding access keys and descriptive tooltips to all interactive elements ensures complete keyboard navigation and improves clarity for users who prefer shortcuts.
**Action:** Consistently apply access keys (via `_`) and `ToolTip` properties to all interactive `CheckBox` and `TextBox` elements in WPF dialogs or settings areas.
## 2026-03-01 - [Actionable Empty State]
**Learning:** Users benefit significantly from having direct, actionable CTA buttons within an empty state instead of just text directing them to use a button elsewhere. By embedding an "Add Folders..." button directly in the empty queue view, we reduce friction and improve onboarding flow.
**Action:** Whenever designing or improving empty states, always include the primary action(s) directly within the empty view rather than just descriptive text.

## 2026-03-02 - Complete Keyboard Navigation Mnemonic Coverage
**Learning:** Providing access keys (mnemonics) only for settings or text fields is insufficient. Power users rely heavily on keyboard shortcuts for primary interactions on main application toolbars. Failing to include mnemonics on primary application buttons prevents a fully keyboard-accessible workflow.
**Action:** When auditing or implementing keyboard accessibility, guarantee that every main toolbar action button and empty state CTA includes an assigned access key prefix (`_`) without conflicting with others.
