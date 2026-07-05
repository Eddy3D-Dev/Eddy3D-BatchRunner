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

## 2026-06-10 - Visual Feedback for Drag and Drop
**Learning:** Users often drag files over an application window without knowing if the application will accept them or where to drop them. Providing clear visual feedback (like a drop overlay) improves confidence and usability during drag-and-drop operations.
**Action:** Always add a visual overlay (e.g., `DropOverlay`) that becomes visible during `DragEnter` and hidden on `DragLeave` and `Drop` to guide users dragging files or folders.

## 2026-06-12 - Ensure context menu operations work on Selected Items
**Learning:** In WPF, context menus and toolbar buttons often depend on a selection state managed in the ViewModel (like `SelectedJob` or `SelectedFolder`). If the UI control (e.g. `DataGrid` or `ListBox`) doesn't have a `SelectedItem` binding, these contextual actions will fail when invoked globally.
**Action:** Always verify that `SelectedItem` property is bound `TwoWay` to the corresponding ViewModel property on lists or grids when there are global actions depending on selection.

## 2026-04-10 - Fix WPF Expander Header Clickability
**Learning:** By default, WPF `Expander` headers only register clicks on the exact text or elements within them, not the empty space. Using width bindings to stretch the header often causes layout issues or clipping with scrollbars.
**Action:** To ensure the entire `Expander` header is clickable and layouts properly, set `HorizontalContentAlignment="Stretch"` on the `Expander` itself and apply `Background="Transparent"` and `Cursor="Hand"` to the internal `Grid` or container.

## 2026-04-10 - External Link Indicators
**Learning:** Users can be surprised when a button click unexpectedly opens a web browser instead of navigating within the app.
**Action:** Always append a `↗` (or similar icon) to buttons or links that open external web pages to clearly indicate the action.

## 2026-06-15 - Surface Keyboard Shortcuts in Tooltips
**Learning:** WPF mnemonic access keys (e.g., `_File`) allow users to trigger actions using `Alt + [Key]`, but these shortcuts are completely invisible until the user presses `Alt`. This hidden nature significantly reduces the discoverability of keyboard navigation for power users and those relying on assistive technologies.
**Action:** Always surface invisible mnemonic access keys by explicitly appending their keyboard shortcut hint (e.g., `(Alt+F)`) to the element's `ToolTip`. This creates a more intuitive and discoverable keyboard-accessible interface.

## 2026-07-20 - Contextual Tooltips for Disabled Controls
**Learning:** When toolbar action buttons (like "Cancel" or "Restart") are disabled because they require a selection or specific state, users are often left guessing why the action is unavailable. This is especially frustrating if the tooltip only describes what the button *would* do if it were enabled.
**Action:** Use dynamic tooltips (e.g., via `<Style.Triggers>` in WPF) on context-dependent buttons. When `IsEnabled="False"`, change the tooltip to explicitly explain what state is required to enable the button (e.g., "Select an active job to cancel it"). Pair this with `ToolTipService.ShowOnDisabled="True"` to ensure the tooltip is visible.
## 2024-05-18 - Context-aware Disabled Tooltips in WPF
**Learning:** WPF makes it easy to add dynamic, context-aware explanations for why a button is disabled without needing complex ViewModel logic. Setting `ToolTipService.ShowOnDisabled="True"` and using a `<Style.Triggers>` block checking `IsEnabled` is a clean, semantic pattern.
**Action:** Use this pattern across WPF views to prevent users from wondering "why can't I click this?" when features are grayed out.
## 2026-06-25 - Empty State Button Styling
**Learning:** The empty state CTA buttons had plain text. Wrapping them in a StackPanel with emoji icons improves discoverability. We must set AutomationProperties.Name directly on the Button when moving structural text so screen readers read the action name cleanly rather than reading the emoji or becoming confused by the layout container.
**Action:** Always verify accessible names on WPF elements when using composite control structures like StackPanels inside Buttons.

## 2026-06-26 - Account for Padding and Font Weight in Fixed Column Widths
**Learning:** In WPF `DataGrid` definitions, when applying padding/margins (e.g., `GridColumnTextMargin` which has an 8px margin on each side) and font variations like `FontWeight="SemiBold"`, the actual render space required for text (such as column headers or cell values) increases significantly. Simply setting fixed widths based on standard text size often leads to unexpected text truncation (e.g., "Completed" becomes "Comple...").
**Action:** When setting fixed `Width` properties for `DataGrid` columns, proactively account for applied inner margins, padding, and font weight by adding extra buffer space to prevent visual clipping.

## 2026-08-15 - Empty states for dynamic hyperlinks in DataGrids
**Learning:** When a DataGrid column uses a `Hyperlink` template bound to a property that can be null (like a file path), it creates accessibility and UX issues. If left empty, the cell is blank and the focusable hyperlink becomes an invisible, silent tab stop for screen readers, or announces unhelpful empty strings. Furthermore, hardcoded ToolTips like "Open log file: " appear even when there's no file, confusing users.
**Action:** Always provide empty states for dynamic hyperlinks in tables. Use a `Style` with a `DataTrigger` on `{x:Null}` to set `IsEnabled="False"`, remove `ToolTip`, clear `AutomationProperties.Name`, and set `TextDecorations="None"`. Additionally, provide a visual placeholder using `TargetNullValue='-'` in the binding so the cell doesn't look accidentally empty.
## 2024-05-24 - Focus Visible Styles for Custom Button Templates
**Learning:** When using custom `ControlTemplate`s for WPF Controls like `Button`, the default dashed focus rectangle often looks misaligned or disappears entirely against custom backgrounds. Relying solely on `IsKeyboardFocused` to change the border color can be too subtle and fails WCAG non-text contrast requirements if the border color change isn't distinct enough from the background or the previous border.
**Action:** Always add an explicit, high-contrast `FocusRing` (e.g., a 2px `Border` slightly offset or with a contrasting color) inside the custom `ControlTemplate` and set `FocusVisualStyle="{x:Null}"` on the control to disable the default Windows dashed outline. Toggle the `FocusRing`'s visibility using a trigger on `IsKeyboardFocused="True"`.

## 2026-06-29 - Empty states for nullable text in DataGrids
**Learning:** When a DataGrid column is bound to a nullable property (like dates or duration), it displays a blank cell if empty. Adding a placeholder value improves readability, but setting `TargetNullValue='-'` causes unhelpful tooltips (e.g., just displaying "-") to appear if the cell's ToolTip is bound to its Text.
**Action:** Set `TargetNullValue='-'` on nullable text column bindings to show a neat placeholder instead of a blank cell, and pair it with a Style Trigger on `Text="-"` to set `ToolTipService.IsEnabled="False"`, suppressing the unhelpful placeholder tooltip.
## 2026-07-03 - Clean Screen Reader Names for File Extensions
**Learning:** When WPF UI elements contain code syntax, symbols, or wildcards (e.g., `*._bat`), screen readers announce them awkwardly (e.g., 'star dot bat').
**Action:** Explicitly set `AutomationProperties.Name` to plain, descriptive text (e.g., 'Add batch files') so screen readers announce the action cleanly.

## 2026-07-03 - Conditional Exit Confirmations
**Learning:** Prompting users with 'Are you sure you want to exit?' every time they close the app causes click fatigue, especially when state is auto-saved.
**Action:** Only show an exit confirmation warning if there are active background tasks or a real risk of data loss. If idle, exit silently.

## 2026-07-04 - Keyboard Focus Visibility in WPF Rows
**Learning:** When users navigate a `DataGrid` using the keyboard (e.g., tabbing through interactive elements like buttons within cells), the row lacks visual tracking feedback by default, unlike when hovering with a mouse. Adding an `IsKeyboardFocusWithin` trigger mirroring the `IsMouseOver` state provides essential context for keyboard users.
**Action:** Always add an `IsKeyboardFocusWithin` trigger alongside `IsMouseOver` to `DataGridRow` styles to ensure consistent row-tracking feedback for keyboard users.

## 2026-10-27 - Contextual Item Counts on Collapsible Headers
**Learning:** Users lack context about the contents of a collapsed list header (like an `Expander`), requiring them to manually expand it to see how many items are inside. This adds unnecessary clicks and friction.
**Action:** When designing collapsible headers that group items (like folders containing jobs), always append a contextual count of the items (e.g., `(6)`) using a muted text brush. This provides immediate value while maintaining visual hierarchy.
