"""
Reconciliation Module - Blueprint Routes
Worldline payments reconciliation and management

IMPORTANT: This module connects to the ACCEPT DATABASE (recon_db)
- Used for safe testing and development of Worldline reconciliation
- Does NOT use production data
- Configured via RECON_DB_* environment variables
"""
from flask import Blueprint, render_template, request, redirect, url_for, flash, jsonify
from flask_login import login_required, current_user
from werkzeug.utils import secure_filename
from datetime import datetime, timedelta
import os

from app.shared.decorators import require_recon_access
from app.recon.database import ReconDatabase

# Initialize database connection to ACCEPT environment
db = ReconDatabase()

# Create blueprint
recon_bp = Blueprint('recon', __name__, 
                     template_folder='templates',
                     static_folder='static')

# Custom template filters
@recon_bp.app_template_filter('format_currency')
def format_currency(value):
    if value is None:
        return '€0.00'
    return f'€{value:,.2f}'

@recon_bp.app_template_filter('format_number')
def format_number(value):
    if value is None:
        return '0'
    return f'{value:,}'

@recon_bp.app_template_filter('format_date')
def format_date(value):
    if value is None:
        return ''
    if isinstance(value, str):
        try:
            value = datetime.strptime(value, '%Y-%m-%d')
        except:
            return value
    return value.strftime('%d/%m/%Y')

# Debug route to check URL generation
@recon_bp.route('/debug-urls')
@login_required
@require_recon_access
def debug_urls():
    from flask import render_template_string, render_template
    css_url = url_for('recon.static', filename='css/style.css')
    
    # Render the actual dashboard template and capture output
    html = render_template('dashboard.html', 
                          stats={}, 
                          daily_volume=[], 
                          brand_breakdown=[],
                          import_stats={},
                          data_range={},
                          days=30)
    
    # Extract just the head section
    import re
    head_match = re.search(r'<head>(.*?)</head>', html, re.DOTALL)
    head_content = head_match.group(1) if head_match else "HEAD NOT FOUND"
    
    return f"<h3>url_for result:</h3>{css_url}<br><br><h3>Actual rendered HEAD:</h3><pre>{head_content}</pre>"

@recon_bp.app_template_filter('format_datetime')
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
# DASHBOARD
# =====================================================

@recon_bp.route('/')
@recon_bp.route('/dashboard')
@login_required
@require_recon_access
def dashboard():
    try:
        # DEBUG: Print template info
        import sys
        print(f"DEBUG: Blueprint name = {recon_bp.name}", file=sys.stderr)
        print(f"DEBUG: Template folder = {recon_bp.template_folder}", file=sys.stderr)
        print(f"DEBUG: Root path = {recon_bp.root_path}", file=sys.stderr)
        
        # Get summary statistics (default 30 days)
        days = int(request.args.get('days', 30))
        
        stats = db.get_worldline_summary_stats(days)
        daily_volume = db.get_daily_volume(days)
        brand_breakdown = db.get_brand_breakdown(days)
        import_stats = db.get_import_stats()
        data_range = db.get_data_date_range()
        
        return render_template('recon_dashboard.html',
                             stats=stats,
                             daily_volume=daily_volume,
                             brand_breakdown=brand_breakdown,
                             import_stats=import_stats,
                             data_range=data_range,
                             days=days)
    except Exception as e:
        flash(f'Error loading dashboard: {str(e)}', 'danger')
        return render_template('dashboard.html', stats={}, daily_volume=[], 
                             brand_breakdown=[], import_stats={}, data_range={})

# =====================================================
# WORLDLINE PAYMENTS
# =====================================================

@recon_bp.route('/payments')
@login_required
@require_recon_access
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
        
        # Table filters
        payment_id = request.args.get('filter_id')
        order = request.args.get('filter_order')
        owner = request.args.get('filter_owner')
        country = request.args.get('filter_country')
        amount_min = request.args.get('filter_amount_min')
        amount_max = request.args.get('filter_amount_max')
        
        # Convert amount to float if provided
        if amount_min:
            try:
                amount_min = float(amount_min)
            except:
                amount_min = None
        if amount_max:
            try:
                amount_max = float(amount_max)
            except:
                amount_max = None
        
        # Search takes precedence
        if search:
            # Try searching as-is first, then also try with underscore replaced by dot
            payments_list = db.search_payments(search, limit=100)
            if not payments_list and '_' in search:
                # Also try with dot instead of underscore
                search_alt = search.replace('_', '.')
                payments_list = db.search_payments(search_alt, limit=100)
            total_count = len(payments_list)
        else:
            # Calculate offset
            offset = (page - 1) * per_page
            
            # Get payments with filters
            payments_list = db.get_worldline_payments(
                start_date=start_date,
                end_date=end_date,
                brand=brand,
                merchref=merchref,
                ref=ref,
                status=status,
                payment_id=payment_id,
                order=order,
                owner=owner,
                country=country,
                amount_min=amount_min,
                amount_max=amount_max,
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
                status=status,
                payment_id=payment_id,
                order=order,
                owner=owner,
                country=country,
                amount_min=amount_min,
                amount_max=amount_max
            )
        
        total_pages = (total_count + per_page - 1) // per_page
        
        return render_template('payments.html',
                             payments=payments_list,
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
                                 'search': search,
                                 'filter_id': payment_id,
                                 'filter_order': order,
                                 'filter_owner': owner,
                                 'filter_country': country,
                                 'filter_amount_min': amount_min,
                                 'filter_amount_max': amount_max
                             })
    except Exception as e:
        flash(f'Error loading payments: {str(e)}', 'danger')
        return render_template('payments.html', payments=[], page=1, 
                             per_page=50, total_count=0, total_pages=0, filters={})

# =====================================================
# API ENDPOINTS
# =====================================================

@recon_bp.route('/api/payment/<path:payment_id>/<paydate>')
@login_required
@require_recon_access
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

@recon_bp.route('/reconciliation')
@login_required
@require_recon_access
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
        flash(f'Error loading reconciliation data: {str(e)}', 'danger')
        return render_template('reconciliation.html', summary={}, 
                             unmatched=[], exceptions=[])

# =====================================================
# REPORTS
# =====================================================

@recon_bp.route('/reports')
@login_required
@require_recon_access
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
        flash(f'Error loading reports: {str(e)}', 'danger')
        return render_template('reports.html', merchant_breakdown=[], 
                             country_breakdown=[], brand_breakdown=[])

# =====================================================
# DATA IMPORT
# =====================================================

def allowed_file(filename):
    """Check if file extension is allowed"""
    ALLOWED_EXTENSIONS = {'csv'}
    return '.' in filename and filename.rsplit('.', 1)[1].lower() in ALLOWED_EXTENSIONS

@recon_bp.route('/import', methods=['GET', 'POST'])
@login_required
@require_recon_access
def import_data():
    if request.method == 'POST':
        # Check if file was uploaded
        if 'file' not in request.files:
            flash('No file uploaded', 'danger')
            return redirect(request.url)
        
        file = request.files['file']
        
        if file.filename == '':
            flash('No file selected', 'danger')
            return redirect(request.url)
        
        if file and allowed_file(file.filename):
            from app.recon.data_import import WorldlineCSVImporter
            
            filename = secure_filename(file.filename)
            upload_folder = os.path.join(os.path.dirname(__file__), 'uploads')
            os.makedirs(upload_folder, exist_ok=True)
            filepath = os.path.join(upload_folder, filename)
            
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
                    flash(f"Import failed: {result['failed']} errors. Check logs for details.", 'danger')
                
                # Clean up uploaded file
                os.remove(filepath)
                
                return redirect(url_for('recon.import_history'))
                
            except Exception as e:
                flash(f'Import error: {str(e)}', 'danger')
                if os.path.exists(filepath):
                    os.remove(filepath)
                return redirect(request.url)
        else:
            flash('Invalid file type. Only CSV files are allowed.', 'danger')
            return redirect(request.url)
    
    return render_template('import.html')

@recon_bp.route('/import/history')
@login_required
@require_recon_access
def import_history():
    try:
        history = db.get_import_history(limit=100)
        return render_template('import_history.html', history=history)
    except Exception as e:
        flash(f'Error loading import history: {str(e)}', 'danger')
        return render_template('import_history.html', history=[])

# =====================================================
# SETTINGS & ADMIN
# =====================================================

@recon_bp.route('/settings')
@login_required
@require_recon_access
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
        flash(f'Error loading settings: {str(e)}', 'danger')
        return render_template('settings.html', data_sources=[], 
                             rules=[], partitions=[])

# =====================================================
# API ENDPOINTS - Statistics
# =====================================================

@recon_bp.route('/api/stats/daily/<int:days>')
@login_required
@require_recon_access
def api_daily_stats(days):
    try:
        data = db.get_daily_volume(days)
        return jsonify([dict(row) for row in data])
    except Exception as e:
        return jsonify({'error': str(e)}), 500

@recon_bp.route('/api/stats/brands/<int:days>')
@login_required
@require_recon_access
def api_brand_stats(days):
    try:
        data = db.get_brand_breakdown(days)
        return jsonify([dict(row) for row in data])
    except Exception as e:
        return jsonify({'error': str(e)}), 500
