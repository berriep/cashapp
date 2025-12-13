"""Convert BAI main.py to blueprint"""
import re

# Read original file
with open('BAI_Tool/Monitor/app/main.py', 'r', encoding='utf-8') as f:
    content = f.read()

# Find where routes start (after login_manager setup)
route_start = content.find('@app.route(\'/dashboard\')')
if route_start == -1:
    print("Could not find dashboard route!")
    exit(1)

# Extract only the routes section
routes_content = content[route_start:]

# Replace @app.route with @bai_bp.route
routes_content = routes_content.replace('@app.route', '@bai_bp.route')
routes_content = routes_content.replace('@app.errorhandler', '@bai_bp.app_errorhandler')

# Fix imports for pdf_generator
routes_content = routes_content.replace('from app.pdf_generator', 'from app.bai.pdf_generator')

# Remove if __name__ == '__main__': section
if_main_pos = routes_content.find("if __name__ == '__main__':")
if if_main_pos != -1:
    routes_content = routes_content[:if_main_pos]

# Add blueprint header
header = '''"""
BAI Monitor routes - Blueprint for BAI transaction monitoring
"""
from flask import Blueprint, render_template, request, redirect, url_for, flash, jsonify, make_response
from flask_login import login_required, current_user
from app.shared.database import db
from datetime import datetime, timedelta, date

# Create BAI blueprint
bai_bp = Blueprint('bai', __name__, template_folder='templates')

# Custom template filter for Dutch number formatting
@bai_bp.app_template_filter('nl_currency')
def nl_currency_filter(value):
    """Format number as Dutch currency (1.234,56)"""
    if value is None:
        return '0,00'
    try:
        formatted = f"{float(value):,.2f}"
        formatted = formatted.replace(',', 'TEMP').replace('.', ',').replace('TEMP', '.')
        return formatted
    except (ValueError, TypeError):
        return '0,00'

'''

# Combine
final = header + routes_content

# Write output
with open('app/bai/routes.py', 'w', encoding='utf-8') as f:
    f.write(final)

print('✅ routes.py created successfully!')
print(f'   Lines: {len(final.splitlines())}')
