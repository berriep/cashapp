from flask import Flask, render_template, request, redirect, url_for, flash, jsonify
from flask_login import LoginManager, login_user, logout_user, login_required, current_user
from config.config import Config
from app.auth import User
from app.database import db
from datetime import datetime, timedelta
import json

# Initialize Flask app
app = Flask(__name__)
app.config.from_object(Config)

# Custom template filter for Dutch number formatting
@app.template_filter('nl_currency')
def nl_currency_filter(value):
    """Format number as Dutch currency (1.234,56)"""
    if value is None:
        return '0,00'
    try:
        # Format with comma as thousands separator and period as decimal
        formatted = f"{float(value):,.2f}"
        # Swap comma and period for Dutch notation
        formatted = formatted.replace(',', 'TEMP').replace('.', ',').replace('TEMP', '.')
        return formatted
    except (ValueError, TypeError):
        return '0,00'

# Initialize Flask-Login
login_manager = LoginManager()
login_manager.init_app(app)
login_manager.login_view = 'login'

@login_manager.user_loader
def load_user(user_id):
    return User.get(user_id)

# Routes
@app.route('/')
def index():
    """Redirect to dashboard or login"""
    if current_user.is_authenticated:
        return redirect(url_for('dashboard'))
    return redirect(url_for('login'))

@app.route('/login', methods=['GET', 'POST'])
def login():
    """Login page"""
    if current_user.is_authenticated:
        return redirect(url_for('dashboard'))
    
    if request.method == 'POST':
        username = request.form.get('username')
        password = request.form.get('password')
        
        if User.verify_password(username, password):
            user = User.get(username)
            login_user(user)
            User.update_last_login(username)
            next_page = request.args.get('next')
            return redirect(next_page or url_for('dashboard'))
        else:
            flash('Invalid username or password', 'danger')
    
    return render_template('login.html')

@app.route('/logout')
@login_required
def logout():
    """Logout user"""
    logout_user()
    flash('You have been logged out', 'info')
    return redirect(url_for('login'))

@app.route('/dashboard')
@login_required
def dashboard():
    """Main dashboard"""
    days = request.args.get('days', 7, type=int)
    
    try:
        # Get summary data
        transaction_summary = db.get_transaction_summary(days=days)
        balance_data = db.get_balance_data(days=days)
        data_quality = db.get_data_quality_status(days=days)
        missing_days = db.get_missing_days(days=30)
        
        return render_template('dashboard.html',
            transaction_summary=transaction_summary,
            balance_data=balance_data,
            data_quality=data_quality,
            missing_days=missing_days,
            current_date=datetime.now(),
            selected_days=days
        )
    except Exception as e:
        flash(f'Error loading dashboard: {str(e)}', 'danger')
        return render_template('dashboard.html', 
            error=str(e),
            current_date=datetime.now(),
            transaction_summary=[],
            balance_data=[],
            data_quality=[],
            missing_days=[],
            selected_days=days
        )

@app.route('/transaction-details')
@login_required
def transaction_details():
    """Individual transaction details page"""
    days = request.args.get('days', 7, type=int)
    ibans = request.args.getlist('iban')  # Get multiple IBANs
    date_from = request.args.get('date_from', None)
    date_to = request.args.get('date_to', None)
    amount_min = request.args.get('amount_min', None, type=float)
    amount_max = request.args.get('amount_max', None, type=float)
    counterparty = request.args.get('counterparty', None)
    
    # Handle empty strings as None
    if date_from == '':
        date_from = None
    if date_to == '':
        date_to = None
    if counterparty == '':
        counterparty = None
    
    # Filter out empty IBANs
    ibans = [iban for iban in ibans if iban]
    
    try:
        transactions = db.get_transaction_details(
            days=days,
            iban_filter=ibans if ibans else None,
            date_from=date_from,
            date_to=date_to,
            amount_min=amount_min,
            amount_max=amount_max,
            counterparty_filter=counterparty
        )
        accounts = db.get_account_list()
        
        return render_template('transaction_details.html',
            transactions=transactions,
            accounts=accounts,
            selected_days=days,
            selected_ibans=ibans,
            selected_date_from=date_from,
            selected_date_to=date_to,
            selected_amount_min=amount_min,
            selected_amount_max=amount_max,
            selected_counterparty=counterparty
        )
    except Exception as e:
        flash(f'Error loading transaction details: {str(e)}', 'danger')
        return render_template('transaction_details.html', error=str(e))


@app.route('/balances')
@login_required
def balances():
    """Balance monitoring page"""
    days = request.args.get('days', 7, type=int)
    iban = request.args.get('iban', None)
    
    # Handle empty string as None
    if iban == '':
        iban = None
    
    try:
        balance_data = db.get_balance_data(days=days, iban_filter=iban)
        reconciliation = db.get_daily_reconciliation(iban_filter=iban)
        accounts = db.get_account_list()
        
        return render_template('balances.html',
            balance_data=balance_data,
            reconciliation=reconciliation,
            accounts=accounts,
            selected_days=days,
            selected_iban=iban
        )
    except Exception as e:
        flash(f'Error loading balances: {str(e)}', 'danger')
        return render_template('balances.html', error=str(e))

@app.route('/bank-statements')
@login_required
def bank_statements():
    """Bank statements page"""
    from datetime import date, timedelta
    
    # Get parameters
    iban = request.args.get('iban', None)
    date_from = request.args.get('date_from', None)
    date_to = request.args.get('date_to', None)
    
    # Handle empty strings as None
    if iban == '':
        iban = None
    if date_from == '':
        date_from = None
    if date_to == '':
        date_to = None
    
    # Set defaults if not provided
    if not date_from and not date_to:
        # Default to yesterday
        yesterday = date.today() - timedelta(days=1)
        date_from = yesterday.strftime('%Y-%m-%d')
        date_to = yesterday.strftime('%Y-%m-%d')
    
    try:
        accounts = db.get_account_list()
        
        # Only fetch data if IBAN is selected
        if iban and date_from and date_to:
            summary_data = db.get_bank_statement_summary(iban, date_from, date_to)
            summary = summary_data[0] if summary_data else None
            transactions = db.get_bank_statement_transactions(iban, date_from, date_to)
        else:
            summary = None
            transactions = []
        
        return render_template('bank_statements.html',
            summary=summary,
            transactions=transactions,
            accounts=accounts,
            selected_iban=iban,
            selected_date_from=date_from,
            selected_date_to=date_to
        )
    except Exception as e:
        flash(f'Error loading bank statement: {str(e)}', 'danger')
        return render_template('bank_statements.html', error=str(e), accounts=[])

@app.route('/bank-statements/pdf')
@login_required
def bank_statement_pdf():
    """Generate PDF bank statement"""
    from datetime import date, timedelta
    from flask import make_response
    from app.pdf_generator import generate_bank_statement_pdf
    
    # Get parameters
    iban = request.args.get('iban', None)
    date_from = request.args.get('date_from', None)
    date_to = request.args.get('date_to', None)
    
    if not iban or not date_from or not date_to:
        flash('Please select account and date range first', 'warning')
        return redirect(url_for('bank_statements'))
    
    try:
        summary_data = db.get_bank_statement_summary(iban, date_from, date_to)
        summary = summary_data[0] if summary_data else None
        transactions = db.get_bank_statement_transactions(iban, date_from, date_to)
        
        if not summary:
            flash('No data found for the selected period', 'warning')
            return redirect(url_for('bank_statements'))
        
        # Generate PDF
        pdf_buffer = generate_bank_statement_pdf(summary, transactions)
        
        # Create response
        response = make_response(pdf_buffer.getvalue())
        response.headers['Content-Type'] = 'application/pdf'
        response.headers['Content-Disposition'] = f'attachment; filename=bank_statement_{iban}_{date_from}_{date_to}.pdf'
        
        return response
    except Exception as e:
        flash(f'Error generating PDF: {str(e)}', 'danger')
        return redirect(url_for('bank_statements'))

@app.route('/reports')
@login_required
def reports():
    """Reports page"""
    return render_template('reports.html')

@app.route('/reports/reconciliation')
@login_required
def reconciliation_report():
    """Detailed reconciliation report"""
    days = request.args.get('days', 7, type=int)
    iban_filter = request.args.get('iban', None)
    
    # Convert empty string to None
    if iban_filter == '' or iban_filter == 'None':
        iban_filter = None
    
    try:
        data = db.get_detailed_reconciliation(days=days, iban_filter=iban_filter)
        accounts = db.get_account_list()
        
        # Calculate summary statistics
        total_rows = len(data)
        perfect_matches = len([r for r in data if r['audit_status'] == 'PERFECT_MATCH'])
        minor_diffs = len([r for r in data if r['audit_status'] == 'MINOR_DIFF'])
        major_diffs = len([r for r in data if r['audit_status'] == 'MAJOR_DIFF'])
        missing_data = len([r for r in data if 'MISSING' in r['audit_status']])
        
        summary = {
            'total_rows': total_rows,
            'perfect_matches': perfect_matches,
            'minor_diffs': minor_diffs,
            'major_diffs': major_diffs,
            'missing_data': missing_data,
            'match_percentage': round((perfect_matches / total_rows * 100) if total_rows > 0 else 0, 1)
        }
        
        return render_template('reconciliation_report.html',
            data=data,
            summary=summary,
            accounts=accounts,
            selected_days=days,
            selected_iban=iban_filter
        )
    except Exception as e:
        flash(f'Error loading reconciliation report: {str(e)}', 'danger')
        return render_template('reconciliation_report.html', 
            error=str(e),
            data=[],
            summary={},
            accounts=[],
            selected_days=days,
            selected_iban=iban_filter
        )

# Admin routes
@app.route('/admin')
@login_required
def admin():
    """Admin dashboard - redirect to users"""
    if not current_user.is_admin:
        flash('Access denied. Admin privileges required.', 'danger')
        return redirect(url_for('dashboard'))
    return redirect(url_for('admin_users'))

@app.route('/admin/users')
@login_required
def admin_users():
    """User management page"""
    if not current_user.is_admin:
        flash('Access denied. Admin privileges required.', 'danger')
        return redirect(url_for('dashboard'))
    
    users = User.get_all_users()
    return render_template('admin/users.html', users=users)

@app.route('/admin/users/create', methods=['GET', 'POST'])
@login_required
def admin_create_user():
    """Create new user"""
    if not current_user.is_admin:
        flash('Access denied. Admin privileges required.', 'danger')
        return redirect(url_for('dashboard'))
    
    if request.method == 'POST':
        username = request.form.get('username')
        password = request.form.get('password')
        email = request.form.get('email')
        full_name = request.form.get('full_name')
        is_admin = request.form.get('is_admin') == 'on'
        
        if not username or not password:
            flash('Username and password are required', 'danger')
        else:
            try:
                User.create_user(username, password, email, full_name, is_admin)
                flash(f'User {username} created successfully', 'success')
                return redirect(url_for('admin_users'))
            except Exception as e:
                flash(f'Error creating user: {str(e)}', 'danger')
    
    return render_template('admin/create_user.html')

@app.route('/admin/users/<int:user_id>/edit', methods=['GET', 'POST'])
@login_required
def admin_edit_user(user_id):
    """Edit user"""
    if not current_user.is_admin:
        flash('Access denied. Admin privileges required.', 'danger')
        return redirect(url_for('dashboard'))
    
    if request.method == 'POST':
        email = request.form.get('email')
        full_name = request.form.get('full_name')
        is_admin = request.form.get('is_admin') == 'on'
        is_active = request.form.get('is_active') == 'on'
        
        try:
            User.update_user(user_id, email=email, full_name=full_name, is_admin=is_admin, is_active=is_active)
            flash('User updated successfully', 'success')
            return redirect(url_for('admin_users'))
        except Exception as e:
            flash(f'Error updating user: {str(e)}', 'danger')
    
    users = User.get_all_users()
    user = next((u for u in users if u['id'] == user_id), None)
    if not user:
        flash('User not found', 'danger')
        return redirect(url_for('admin_users'))
    
    return render_template('admin/edit_user.html', user=user)

@app.route('/admin/users/<int:user_id>/delete', methods=['POST'])
@login_required
def admin_delete_user(user_id):
    """Delete user"""
    if not current_user.is_admin:
        flash('Access denied. Admin privileges required.', 'danger')
        return redirect(url_for('dashboard'))
    
    try:
        User.delete_user(user_id)
        flash('User deleted successfully', 'success')
    except Exception as e:
        flash(f'Error deleting user: {str(e)}', 'danger')
    
    return redirect(url_for('admin_users'))

@app.route('/admin/users/<int:user_id>/change-password', methods=['POST'])
@login_required
def admin_change_password(user_id):
    """Change user password"""
    if not current_user.is_admin:
        flash('Access denied. Admin privileges required.', 'danger')
        return redirect(url_for('dashboard'))
    
    new_password = request.form.get('new_password')
    if not new_password:
        flash('Password is required', 'danger')
    else:
        try:
            User.change_password(user_id, new_password)
            flash('Password changed successfully', 'success')
        except Exception as e:
            flash(f'Error resetting password: {str(e)}', 'danger')
    
    return redirect(url_for('admin_users'))

@app.route('/api/transaction-chart')
@login_required
def api_transaction_chart():
    """API endpoint for transaction chart data"""
    days = request.args.get('days', 7, type=int)
    
    try:
        summary = db.get_transaction_summary(days=days)
        
        # Prepare data for charts - count transactions per day
        from collections import defaultdict
        daily_counts = defaultdict(lambda: {'credit': 0, 'debit': 0})
        
        for row in summary:
            date_str = row['date'].strftime('%d-%m')
            # Count number of transaction records (each row is one IBAN on that day)
            # We need to get actual transaction counts from the transaction_count field
            daily_counts[date_str]['total'] = daily_counts[date_str].get('total', 0) + row['transaction_count']
        
        dates = sorted(daily_counts.keys())
        counts = [daily_counts[date]['total'] for date in dates]
        
        return jsonify({
            'dates': dates,
            'counts': counts
        })
    except Exception as e:
        return jsonify({'error': str(e)}), 500

# Error handlers
@app.errorhandler(404)
def not_found(e):
    return render_template('404.html'), 404

@app.errorhandler(500)
def server_error(e):
    return render_template('500.html'), 500

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000, debug=Config.DEBUG)
