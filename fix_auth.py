"""
Script to fix auth.py by removing duplicate methods and fixing execute_query calls
"""

# Read the file
with open('app/shared/auth.py', 'r', encoding='utf-8') as f:
    lines = f.readlines()

# Find where User class ends and routes begin
output_lines = []
in_user_class = False
class_indent = 0
skip_until_decorator = False
user_class_ended = False

for i, line in enumerate(lines):
    # Track if we're in User class
    if line.strip().startswith('class User'):
        in_user_class = True
        class_indent = len(line) - len(line.lstrip())
        output_lines.append(line)
        continue
    
    # Check if User class has ended (next line at same or less indentation that's not empty/comment)
    if in_user_class and not user_class_ended:
        current_indent = len(line) - len(line.lstrip())
        if line.strip() and not line.strip().startswith('#'):
            if current_indent <= class_indent and not line.strip().startswith('"""'):
                user_class_ended = True
                in_user_class = False
    
    # Skip duplicate methods after User class ends
    if user_class_ended and '@staticmethod' in line and 'def create_user' in lines[i+1]:
        skip_until_decorator = True
        continue
    
    if skip_until_decorator:
        # Stop skipping when we hit a route decorator
        if line.strip().startswith('@shared_bp.route'):
            skip_until_decorator = False
        else:
            continue
    
    # Replace execute_query with execute_update for INSERT/UPDATE/DELETE
    # and remove fetch=False parameter
    if 'shared_db.execute_query(' in line:
        # Check next few lines to see if it's an INSERT, UPDATE or DELETE
        check_lines = ''.join(lines[max(0, i-2):min(len(lines), i+5)])
        if any(keyword in check_lines.upper() for keyword in ['INSERT INTO', 'UPDATE ', 'DELETE FROM']):
            line = line.replace('execute_query(', 'execute_update(')
    
    if 'fetch=False' in line:
        # Remove the fetch=False parameter and fix comma/parenthesis
        line = line.replace(', fetch=False', '')
        line = line.replace(',fetch=False', '')
        line = line.replace('fetch=False,', '')
        line = line.replace('fetch=False', '')
    
    output_lines.append(line)

# Write back
with open('app/shared/auth.py', 'w', encoding='utf-8') as f:
    f.writelines(output_lines)

print("✅ Fixed auth.py:")
print("   - Removed duplicate methods")
print("   - Changed execute_query to execute_update for INSERT/UPDATE/DELETE")
print("   - Removed fetch=False parameters")
