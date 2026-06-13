1. **Fix Expander Header Layout and Clickability in MainWindow.xaml**
   - Replace the hacky `Width="{Binding ... ActualWidth}"` on the `Expander.Header` Grid with `HorizontalContentAlignment="Stretch"` on the `Expander` itself.
   - Add `Background="Transparent"` and `Cursor="Hand"` to the `Expander.Header` Grid so the entire header area is clickable, not just the text.
   - Remove the `Width="40"` spacer column which is no longer needed.

2. **Add visual icon for External Link**
   - Update the "Visualise Results" button content to include `↗` so users know it opens externally.

3. **Empty State Polish**
   - Add a `📂` icon above the "No jobs queued" text to match the drag-and-drop overlay style and make it friendlier.

4. **Verify and Pre-commit**
   - Run `dotnet build` to ensure the XAML is still valid.
   - Create a PR for the changes.
