import re

with open('BatchRunner/MainWindow.xaml', 'r', encoding='utf-8') as f:
    content = f.read()

# Check elements and their access keys
def find_element_content(element_type):
    # This is a bit simplistic and assumes Content= or Text=
    patterns = [
        rf'<{element_type}[^>]*?Content="([^"]+)"',
        rf'<{element_type}[^>]*?Text="([^"]+)"'
    ]
    for p in patterns:
        for match in re.finditer(p, content):
            print(f"{element_type}: {match.group(1)}")

print("Buttons:")
find_element_content('Button')
print("\nCheckBoxes:")
find_element_content('CheckBox')
print("\nLabels:")
find_element_content('Label')
