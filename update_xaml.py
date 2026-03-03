import re

with open('BatchRunner/MainWindow.xaml', 'r', encoding='utf-8') as f:
    content = f.read()

# Replace Buttons
content = content.replace('Content="Add Folder..."', 'Content="_Add Folder..."')
content = content.replace('Content="Add *.bat"', 'Content="Add *._bat"')
content = content.replace('Content="Expand All"', 'Content="_Expand All"')
content = content.replace('Content="Collapse All"', 'Content="_Collapse All"')
content = content.replace('Content="Remove All"', 'Content="Re_move All"')
content = content.replace('Content="Start queue"', 'Content="_Start queue"')
content = content.replace('Content="Cancel Job"', 'Content="Ca_ncel Job"')
content = content.replace('Content="Restart Job"', 'Content="_Restart Job"')
content = content.replace('Content="Visualise Results"', 'Content="_Visualise Results"')
content = content.replace('Content="Add Folders..."', 'Content="_Add Folders..."') # Empty state

# Replace CheckBoxes
content = content.replace('Content="Auto-retry failed jobs (1x)"', 'Content="A_uto-retry failed jobs (1x)"')
content = content.replace('Content="Show job window"', 'Content="S_how job window"')
content = content.replace('Content="Compress &amp; Delete completed"', 'Content="Com_press &amp; Delete completed"')

# Replace Cores TextBlock -> Label and add Name to TextBox
content = content.replace(
    '<TextBlock Text="Cores:" VerticalAlignment="Center" Margin="0,0,4,0" />',
    '<Label Content="C_ores:" Target="{Binding ElementName=CoresTextBox}" VerticalAlignment="Center" Margin="0,0,4,0" Padding="0" Foreground="{StaticResource TextBrush}" />'
)
content = content.replace(
    '<TextBox Text="{Binding TotalCores',
    '<TextBox x:Name="CoresTextBox" Text="{Binding TotalCores'
)

with open('BatchRunner/MainWindow.xaml', 'w', encoding='utf-8') as f:
    f.write(content)

print("Updated MainWindow.xaml")
