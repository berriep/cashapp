from flask import Flask, render_template, request, redirect, url_for, flash, session, jsonify
from flask_login import LoginManager, login_user, logout_user, login_required, current_user
from werkzeug.utils import secure_filename
import os
from datetime import datetime, timedelta

from config.config import Config
from auth import User
from database import db
from data_import import WorldlineCSVImporter

app = Flask(__name__)
app.config.from_object(Config)

# Ensure upload folder exists
os.makedirs(Config.UPLOAD_FOLDER, exist_ok=True)

# Login manager setup
login_manager = LoginManager()
login_manager.init_app(app)
login_manager.login_view = 'login'

@login_manager.user_loader
def load_user(username):
    return User.get(username)

# =====================================================
# AUTHENTICATION ROUTES
# =====================================================

@app.route('/login', methods=['GET', 'POST'])
def login():
    if request.method == 'POST':
        username = request.form.get('username')
        password = request.form.get('password')
        
        if User.verify_password(username, password):
            user = User.get(username)
            login_user(user)
            User.update_last_login(username)
            flash('Login successful!', 'success')
            return redirect(url_for('dashboard'))
        else:
            flash('Invalid username or password', 'error')
    
    return render_template('login.html')

@app.route('/logout')
@login_required
def logout():
    logout_user()
    flash('You have been logged out', 'info')
    return redirect(url_for('login'))

# =====================================================
# DASHBOARD
# =====================================================

@app.route('/')
@app.route('/dashboard')
@login_required
def dashboard():
    try:
        # Get summary statistics (default 30 days)
        days = int(request.args.get('days', 30))
        
        stats = db.get_worldline_summary_stats(days)
        daily_volume = db.get_daily_volume(days)
        brand_breakdown = db.get_brand_breakdown(days)
        import_stats = db.get_import_stats()
        data_range = db.get_data_date_range()
        
        return render_template('dashboard.html',
                             stats=stats,
                             daily_volume=daily_volume,
                             brand_breakdown=brand_breakdown,
                             import_stats=import_stats,
                             data_range=data_range,
                             days=days)
    except Exception as e:
        flash(f'Error loading dashboard: {str(e)}', 'error')
        return render_template('dashboard.html', stats={}, daily_volume=[], 
                             brand_breakdown=[], import_stats={}, data_range={})

# =====================================================
# WORLDLINE PAYMENTS
# =====================================================

@app.route('/payments')
@login_required
def payments():
    try:
        # Get filters from request
        page = int(request.args.get('page', 1))
        per_page = int(request.args.get('per_page', 50))
        
        # Default to last 7 days if no filters provided
        start_date = request.args.get('start_date')
        end_date = request.args.get('end_date')
        
        if not start_date and not end_date and not request.args.get('search') and not request.args.get('ref'):
            end_date = datetime.now().date().isoformat()
            start_date = (datetime.now().date() - timedelta(days=7)).isoformat()
        
        brand = request.args.get('brand')
        merchref = request.args.get('merchref')
        ref = request.args.get('ref')
        status = request.args.get('status')
        search = request.args.get('search')
        
        # Search takes precedence
        if search:
            # Try searching as-is first, then also try with underscore replaced by dot
            payments = db.search_payments(search, limit=100)
            if not payments and '_' in search:
                # Also try with dot instead of underscore
                search_alt = search.replace('_', '.')
                payments = db.search_payments(search_alt, limit=100)
            total_count = len(payments)
        else:
            # Calculate offset
            offset = (page - 1) * per_page
            
            # Get payments with filters
            payments = db.get_worldline_payments(
                start_date=start_date,
                end_date=end_date,
                brand=brand,
                merchref=merchref,
                ref=ref,
                status=status,
                limit=per_page,
                offset=offset
            )
            
            # Get total count for pagination
            total_count = db.get_worldline_payment_count(
                start_date=start_date,
                end_date=end_date,
                brand=brand,
                merchref=merchref,
                ref=ref,
                status=status
            )
        
        total_pages = (total_count + per_page - 1) // per_page
        
        return render_template('payments.html',
                             payments=payments,
                             page=page,
                             per_page=per_page,
                             total_count=total_count,
                             total_pages=total_pages,
                             filters={
                                 'start_date': start_date,
                                 'end_date': end_date,
                                 'brand': brand,
                                 'merchref': merchref,
                                 'ref': ref,
                                 'status': status,
                                 'search': search
                             })
    except Exception as e:
        flash(f'Error loading payments: {str(e)}', 'error')
        return render_template('payments.html', payments=[], page=1, 
                             per_page=50, total_count=0, total_pages=0, filters={})

# =====================================================
# API ENDPOINTS
# =====================================================

@app.route('/api/payment/<path:payment_id>/<paydate>')
@login_required
def api_payment_details(payment_id, paydate):
    """API endpoint to get full payment details"""
    try:
        payment = db.get_payment_details(payment_id, paydate)
        if payment:
            return jsonify(dict(payment))
        else:
            return jsonify({'error': 'Payment not found'}), 404
    except Exception as e:
        return jsonify({'error': str(e)}), 500

# =====================================================
# RECONCILIATION
# =====================================================

@app.route('/reconciliation')
@login_required
def reconciliation():
    try:
        # Default to last 7 days
        end_date = datetime.now().date()
        start_date = end_date - timedelta(days=7)
        
        # Get date range from request if provided
        if request.args.get('start_date'):
            start_date = datetime.strptime(request.args.get('start_date'), '%Y-%m-%d').date()
        if request.args.get('end_date'):
            end_date = datetime.strptime(request.args.get('end_date'), '%Y-%m-%d').date()
        
        # Get reconciliation data
        summary = db.get_reconciliation_summary(start_date, end_date)
        unmatched = db.get_unmatched_worldline_payments(start_date, end_date)
        exceptions = db.get_reconciliation_exceptions(status='OPEN', limit=50)
        
        return render_template('reconciliation.html',
                             summary=summary,
                             unmatched=unmatched,
                             exceptions=exceptions,
                             start_date=start_date,
                             end_date=end_date)
    except Exception as e:
        flash(f'Error loading reconciliation data: {str(e)}', 'error')
        return render_template('reconciliation.html', summary={}, 
                             unmatched=[], exceptions=[])

# =====================================================
# REPORTS
# =====================================================

@app.route('/reports')
@login_required
def reports():
    try:
        # Check if custom date range is provided
        start_date = request.args.get('start_date')
        end_date = request.args.get('end_date')
        
        if start_date and end_date:
            # Use custom date range - calculate days between
            start_dt = datetime.strptime(start_date, '%Y-%m-%d').date()
            end_dt = datetime.strptime(end_date, '%Y-%m-%d').date()
            days = (end_dt - start_dt).days
            days = max(1, days)  # Ensure at least 1 day
        else:
            # Use days parameter (default 30)
            days = int(request.args.get('days', 30))
            start_date = None
            end_date = None
        
        merchant_breakdown = db.get_merchant_breakdown(days, limit=20)
        country_breakdown = db.get_country_breakdown(days)
        brand_breakdown = db.get_brand_breakdown(days)
        
        return render_template('reports.html',
                             merchant_breakdown=merchant_breakdown,
                             country_breakdown=country_breakdown,
                             brand_breakdown=brand_breakdown,
                             days=days,
                             start_date=start_date,
                             end_date=end_date)
    except Exception as e:
        flash(f'Error loading reports: {str(e)}', 'error')
        return render_template('reports.html', merchant_breakdown=[], 
                             country_breakdown=[], brand_breakdown=[])

# =====================================================
# DATA IMPORT
# =====================================================

@app.route('/import', methods=['GET', 'POST'])
@login_required
def import_data():
    if request.method == 'POST':
        # Check if file was uploaded
        if 'file' not in request.files:
            flash('No file uploaded', 'error')
            return redirect(request.url)
        
        file = request.files['file']
        
        if file.filename == '':
            flash('No file selected', 'error')
            return redirect(request.url)
        
        if file and allowed_file(file.filename):
            filename = secure_filename(file.filename)
            filepath = os.path.join(Config.UPLOAD_FOLDER, filename)
            
            try:
                # Save file
                file.save(filepath)
                
                # Import file
                importer = WorldlineCSVImporter()
                result = importer.import_file(filepath, username=current_user.username)
                importer.close()
                
                # Show results
                if result['status'] == 'SUCCESS':
                    flash(f"Import successful! {result['imported']} records imported, {result['duplicates']} duplicates skipped.", 'success')
                elif result['status'] == 'PARTIAL':
                    flash(f"Partial import: {result['imported']} records imported, {result['duplicates']} duplicates, {result['failed']} failed.", 'warning')
                else:
                    flash(f"Import failed: {result['failed']} errors. Check logs for details.", 'error')
                
                # Clean up uploaded file
                os.remove(filepath)
                
                return redirect(url_for('import_history'))
                
            except Exception as e:
                flash(f'Import error: {str(e)}', 'error')
                if os.path.exists(filepath):
                    os.remove(filepath)
                return redirect(request.url)
        else:
            flash('Invalid file type. Only CSV files are allowed.', 'error')
            return redirect(request.url)
    
    return render_template('import.html')

@app.route('/import/history')
@login_required
def import_history():
    try:
        history = db.get_import_history(limit=100)
        return render_template('import_history.html', history=history)
    except Exception as e:
        flash(f'Error loading import history: {str(e)}', 'error')
        return render_template('import_history.html', history=[])

# =====================================================
# SETTINGS & ADMIN
# =====================================================

@app.route('/settings')
@login_required
def settings():
    try:
        data_sources = db.get_data_sources()
        rules = db.get_reconciliation_rules()
        partitions = db.get_partition_info()
        
        return render_template('settings.html',
                             data_sources=data_sources,
                             rules=rules,
                             partitions=partitions)
    except Exception as e:
        flash(f'Error loading settings: {str(e)}', 'error')
        return render_template('settings.html', data_sources=[], 
                             rules=[], partitions=[])

# =====================================================
# API ENDPOINTS
# =====================================================

@app.route('/api/stats/daily/<int:days>')
@login_required
def api_daily_stats(days):
    try:
        data = db.get_daily_volume(days)
        return jsonify([dict(row) for row in data])
    except Exception as e:
        return jsonify({'error': str(e)}), 500

@app.route('/api/stats/brands/<int:days>')
@login_required
def api_brand_stats(days):
    try:
        data = db.get_brand_breakdown(days)
        return jsonify([dict(row) for row in data])
    except Exception as e:
        return jsonify({'error': str(e)}), 500

# =====================================================
# ERROR HANDLERS
# =====================================================

@app.errorhandler(404)
def not_found(error):
    return render_template('404.html'), 404

@app.errorhandler(500)
def internal_error(error):
    return render_template('500.html'), 500

# =====================================================
# UTILITY FUNCTIONS
# =====================================================

def allowed_file(filename):
    return '.' in filename and \
           filename.rsplit('.', 1)[1].lower() in Config.ALLOWED_EXTENSIONS

# =====================================================
# TEMPLATE FILTERS
# =====================================================

@app.template_filter('format_currency')
def format_currency(value):
    if value is None:
        return '€0.00'
    return f'€{value:,.2f}'

@app.template_filter('format_number')
def format_number(value):
    if value is None:
        return '0'
    return f'{value:,}'

@app.template_filter('format_date')
def format_date(value):
    if value is None:
        return ''
    if isinstance(value, str):
        try:
            value = datetime.strptime(value, '%Y-%m-%d')
        except:
            return value
    return value.strftime('%d/%m/%Y')

@app.template_filter('format_datetime')
def format_datetime(value):
    if value is None:
        return ''
    if isinstance(value, str):
        try:
            value = datetime.strptime(value, '%Y-%m-%d %H:%M:%S')
        except:
            return value
    return value.strftime('%d/%m/%Y %H:%M')

# =====================================================
# RUN APPLICATION
# =====================================================

if __name__ == '__main__':
    app.run(debug=False, host='0.0.0.0', port=5001)
