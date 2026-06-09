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
## 2026-03-03 - Prevent WPF Access Key Collisions
**Learning:** Assigning access keys (mnemonics prefixed with `_`) without checking for existing ones can lead to focus-cycling instead of action-triggering, creating a frustrating experience for keyboard users.
**Action:** Always verify access key uniqueness across the entire visible window, not just the local component or menu, to ensure direct action execution.

## 2026-03-08 - Prevent accidental data loss with confirmation dialogs
**Learning:** Users can accidentally click destructive actions like 'Remove All' or 'Remove Folder', which can immediately cancel running jobs and lose queue state without warning.
**Action:** Add confirmation dialogs (`MessageBox`) for any destructive action that causes immediate, unrecoverable data loss or disrupts running processes.

## 2026-03-09 - Contextual Screen Reader Labels in WPF Lists
**Learning:** Repetitive buttons in lists (like "Remove", "Cancel", "Restart" inside a DataGrid or ItemsControl) are announced by screen readers without their associated item context, causing confusion (e.g., hearing "Remove button" 10 times).
**Action:** Always use `AutomationProperties.Name` with a `StringFormat` binding to the item's context (e.g., `AutomationProperties.Name="{Binding Name, StringFormat='Remove {0}'}"`) for action buttons inside repeated list templates.

## 2026-03-10 - Comprehensive Empty State CTAs
**Learning:** Empty states should expose all primary methods of populating data, not just the most common one. Missing options in the empty state (like adding individual files instead of folders when both are supported) forces users to hunt for secondary toolbars, increasing friction.
**Action:** When designing empty states with multiple data entry methods, include clear CTAs for all primary methods (e.g., both "Add Folders" and "Add Files") to ensure a seamless onboarding experience.
## 2026-03-14 - [Visibility of structural borders in light-themed apps]
**Learning:** Hardcoded translucent colors (e.g., `#20FFFFFF`) for structural borders become invisible in light-themed applications, reducing the visual hierarchy and causing components to blend unintentionally.
**Action:** Use semantic design tokens like `SurfaceBorderBrush` (or `SurfaceBorderColor`) to maintain correct contrast and visible boundaries regardless of the overall app theme.
## 2026-03-14 - [Theme resource usage for structural borders]
**Learning:** Theme brushes should almost always use `{DynamicResource ...}` in WPF/XAML rather than `{StaticResource ...}` so that the UI updates correctly if the user switches between light and dark themes at runtime. `StaticResource` evaluates only once upon initialization.
**Action:** Use `{DynamicResource SurfaceBorderBrush}` (or similar theme resources) instead of `StaticResource` to maintain correct contrast and support dynamic theming.
## 2026-03-16 - Unique Access Keys in Empty States
**Learning:** When assigning access keys (mnemonics) in WPF views, keybindings must be unique across the entire window. Empty state views that duplicate persistent actions (like a main toolbar) should omit access keys on the temporary buttons to preserve the global hotkey mapping and prevent focus-cycling collisions.
**Action:** Remove access keys from temporary or duplicate action buttons in empty state views.

## 2026-03-24 - Screen Reader Support for Headers and Dynamic Links
**Learning:** Screen readers often fail to announce context-rich information in complex WPF headers (like `Expander` or `DataGrid` headers) and dynamic links if `AutomationProperties.Name` is not explicitly set. Providing formatted strings gives crucial context.
**Action:** Always add `AutomationProperties.Name` with `StringFormat` to provide detailed context to screen readers, especially on interactive but complex visual controls like Expanders and Hyperlinks.
## 2026-03-24 - Screen Reader Support for Headers and Dynamic Links
**Learning:** Screen readers often fail to announce context-rich information in complex WPF headers (like `Expander` or `DataGrid` headers) and dynamic links if `AutomationProperties.Name` is not explicitly set. Providing formatted strings gives crucial context.
**Action:** Always add `AutomationProperties.Name` with `StringFormat` to provide detailed context to screen readers, especially on interactive but complex visual controls like Expanders and Hyperlinks.

## 2026-03-25 - Row Hover States in DataGrids
**Learning:** Users often lose their place when reading wide rows in a `DataGrid`. Adding a visual hover state significantly improves tracking and usability.
**Action:** Always add an `IsMouseOver` trigger to `DataGridRow` styles to update the background color, matching the app's hover style.

## 2026-03-25 - Color Contrast for Status Indicators
**Learning:** Using overly bright colors (like `#EF6C00` for Orange) for text or small UI indicators fails WCAG AA contrast requirements against light backgrounds.
**Action:** Always check color contrast ratios for status colors and use darker shades (e.g., `#D84315`) to ensure readability and compliance.
