"""
BAI Monitor routes - Blueprint for BAI transaction monitoring
Uses Production Database (bai_db)
"""
from flask import Blueprint, render_template, request, redirect, url_for, flash, jsonify, make_response
from flask_login import login_required, current_user
from app.shared.database import bai_db as db  # Use BAI production database
from app.shared.auth import User
from app.shared.decorators import require_bai_access
from datetime import datetime, timedelta, date

# Create BAI blueprint
bai_bp = Blueprint('bai', __name__, template_folder='templates', static_folder='static', static_url_path='/bai/static')

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

@bai_bp.route('/dashboard')
@login_required
@require_bai_access
def dashboard():
    """Main dashboard - redirects to reconciliation report"""
    return redirect(url_for('bai.reconciliation_report'))

@bai_bp.route('/ops-dashboard')
@login_required
@require_bai_access
def ops_dashboard():
    """Operations Dashboard - Data Flow Visualization"""
    return render_template('ops_dashboard.html')

@bai_bp.route('/transaction-details')
@login_required
@require_bai_access
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


@bai_bp.route('/balances')
@login_required
@require_bai_access
def balances():
    """Balance monitoring page"""
    days = request.args.get('days', 7, type=int)
    iban = request.args.get('iban', None)
    
    # Handle empty string as None
    if iban == '':
        iban = None
    
    try:
        balance_data = db.get_balance_data(days=days, iban_filter=iban)
        reconciliation = db.get_daily_reconciliation(iban_filter=iban, days=days)
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
        return render_template('balances.html', 
            error=str(e),
            balance_data=[],
            reconciliation=[],
            accounts=[],
            selected_days=days,
            selected_iban=iban
        )

@bai_bp.route('/bank-statements')
@login_required
@require_bai_access
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

@bai_bp.route('/bank-statements/pdf')
@login_required
@require_bai_access
def bank_statements_pdf():
    """Generate PDF bank statement"""
    from datetime import date, timedelta
    from flask import make_response
    from app.bai.pdf_generator import generate_bank_statement_pdf
    
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

@bai_bp.route('/reports')
@login_required
@require_bai_access
def reports():
    """Reports page"""
    return render_template('reports.html')

@bai_bp.route('/reports/reconciliation')
@login_required
@require_bai_access
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

@bai_bp.route('/exports/status')
@login_required
@require_bai_access
def export_status():
    """Export status page - shows bai_exports_audit_log"""
    page = request.args.get('page', 1, type=int)
    per_page = 50
    
    # Get filter parameters
    iban_filter = request.args.get('iban', '')
    format_filter = request.args.get('format', '')
    date_from = request.args.get('date_from', '')
    date_to = request.args.get('date_to', '')
    
    try:
        # Build WHERE clause based on filters
        where_conditions = []
        query_params = []
        
        if iban_filter:
            where_conditions.append("iban = %s")
            query_params.append(iban_filter)
        
        if format_filter:
            where_conditions.append("export_format = %s")
            query_params.append(format_filter)
        
        if date_from:
            where_conditions.append("closingdate >= %s")
            query_params.append(date_from)
        
        if date_to:
            where_conditions.append("closingdate <= %s")
            query_params.append(date_to)
        
        where_clause = " WHERE " + " AND ".join(where_conditions) if where_conditions else ""
        
        # Query export audit log with pagination and filters
        query = f"""
            SELECT 
                id,
                timestamp,
                bank,
                iban,
                destination,
                export_format,
                closingdate,
                filename,
                outputfilepath,
                record_count,
                success,
                error_message,
                caller_id
            FROM rpa_data.bai_exports_audit_log
            {where_clause}
            ORDER BY timestamp DESC
            LIMIT %s OFFSET %s
        """
        offset = (page - 1) * per_page
        query_params.extend([per_page, offset])
        logs = db.execute_query(query, tuple(query_params))
        
        # Get total count with filters
        count_query = f"SELECT COUNT(*) as total FROM rpa_data.bai_exports_audit_log{where_clause}"
        total_result = db.execute_query(count_query, tuple(query_params[:-2])) if where_conditions else db.execute_query(count_query)
        total_records = total_result[0]['total'] if total_result else 0
        total_pages = (total_records + per_page - 1) // per_page
        
        # Get unique IBANs and formats for filter dropdowns
        ibans_query = "SELECT DISTINCT iban FROM rpa_data.bai_exports_audit_log WHERE iban IS NOT NULL ORDER BY iban"
        formats_query = "SELECT DISTINCT export_format FROM rpa_data.bai_exports_audit_log WHERE export_format IS NOT NULL ORDER BY export_format"
        
        ibans = [row['iban'] for row in db.execute_query(ibans_query)]
        formats = [row['export_format'] for row in db.execute_query(formats_query)]
        
        # Get status counts with filters
        status_query = f"""
            SELECT 
                SUM(CASE WHEN success = TRUE AND (record_count > 0 OR record_count IS NULL) THEN 1 ELSE 0 END) as success_count,
                SUM(CASE WHEN success = TRUE AND record_count = 0 THEN 1 ELSE 0 END) as success_no_tx_count,
                SUM(CASE WHEN success = FALSE THEN 1 ELSE 0 END) as failed_count
            FROM rpa_data.bai_exports_audit_log
            {where_clause}
        """
        status_result = db.execute_query(status_query, tuple(query_params[:-2])) if where_conditions else db.execute_query(status_query)
        success_count = status_result[0]['success_count'] if status_result else 0
        success_no_tx_count = status_result[0]['success_no_tx_count'] if status_result else 0
        failed_count = status_result[0]['failed_count'] if status_result else 0
        
        return render_template('export_status.html',
            logs=logs,
            page=page,
            total_pages=total_pages,
            total_records=total_records,
            ibans=ibans,
            formats=formats,
            selected_iban=iban_filter,
            selected_format=format_filter,
            selected_date_from=date_from,
            selected_date_to=date_to,
            success_count=success_count,
            success_no_tx_count=success_no_tx_count,
            failed_count=failed_count
        )
    except Exception as e:
        flash(f'Error loading export status: {str(e)}', 'danger')
        return render_template('export_status.html', logs=[], page=1, total_pages=0, total_records=0, ibans=[], formats=[])

@bai_bp.route('/exports/config', methods=['GET', 'POST'])
@login_required
@require_bai_access
def export_config():
    """Export configuration page - shows bai_exports table"""
    if request.method == 'POST':
        try:
            action = request.form.get('action', 'create')
            
            if action == 'update':
                # Update existing config
                config_id = request.form.get('id')
                enabled = request.form.get('enabled') == 'on'
                exportformatversion = request.form.get('exportformatversion')
                outputpath = request.form.get('outputpath')
                fileprefix = request.form.get('fileprefix')
                fileextension = request.form.get('fileextension')
                includedate = request.form.get('includedate') == 'on'
                dateformat = request.form.get('dateformat')
                
                update_query = """
                    UPDATE rpa_data.bai_exports 
                    SET enabled = %s, exportformatversion = %s, outputpath = %s, 
                        fileprefix = %s, fileextension = %s, includedate = %s, 
                        dateformat = %s, updatedat = NOW()
                    WHERE id = %s
                """
                db.execute_query(update_query, (
                    enabled, exportformatversion, outputpath, fileprefix, 
                    fileextension, includedate, dateformat, config_id
                ))
                
                flash('Export configuration updated successfully!', 'success')
            else:
                # Create new config
                enabled = request.form.get('enabled') == 'on'
                bank = request.form.get('bank')
                iban = request.form.get('iban')
                exportformat = request.form.get('exportformat')
                exportformatversion = request.form.get('exportformatversion')
                destination = request.form.get('destination')
                outputpath = request.form.get('outputpath')
                fileprefix = request.form.get('fileprefix')
                fileextension = request.form.get('fileextension')
                includedate = request.form.get('includedate') == 'on'
                dateformat = request.form.get('dateformat')
                
                insert_query = """
                    INSERT INTO rpa_data.bai_exports 
                    (enabled, bank, iban, exportformat, exportformatversion, destination, 
                     outputpath, fileprefix, fileextension, includedate, dateformat, createdat, updatedat)
                    VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, NOW(), NOW())
                """
                db.execute_query(insert_query, (
                    enabled, bank, iban, exportformat, exportformatversion, destination,
                    outputpath, fileprefix, fileextension, includedate, dateformat
                ))
                
                flash('Export configuration created successfully!', 'success')
            
            return redirect(url_for('bai.export_config'))
        except Exception as e:
            flash(f'Error saving export config: {str(e)}', 'danger')
    
    try:
        # Query export configurations
        query = """
            SELECT 
                id,
                enabled,
                bank,
                iban,
                exportformat,
                exportformatversion,
                destination,
                outputpath,
                fileprefix,
                fileextension,
                includedate,
                dateformat,
                createdat,
                updatedat
            FROM rpa_data.bai_exports
            ORDER BY bank, iban
        """
        configs = db.execute_query(query)
        
        # Get unique banks and IBANs for dropdowns
        banks_query = "SELECT DISTINCT bank FROM rpa_data.bai_exports_audit_log WHERE bank IS NOT NULL ORDER BY bank"
        ibans_query = "SELECT DISTINCT iban FROM rpa_data.bai_exports_audit_log WHERE iban IS NOT NULL ORDER BY iban"
        
        banks = [row['bank'] for row in db.execute_query(banks_query)]
        ibans = [row['iban'] for row in db.execute_query(ibans_query)]
        
        return render_template('export_config.html',
            configs=configs,
            banks=banks,
            ibans=ibans
        )
    except Exception as e:
        flash(f'Error loading export config: {str(e)}', 'danger')
        return render_template('export_config.html', configs=[], banks=[], ibans=[])

@bai_bp.route('/api/transaction-chart')
@login_required
@require_bai_access
def transaction_chart():
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

@bai_bp.route('/api/ops-status')
@login_required
@require_bai_access
def ops_status():
    """API endpoint for OPS Dashboard - returns real-time system status"""
    try:
        from datetime import datetime, timedelta
        
        # Get real data from BAI database using same logic as reconciliation report
        yesterday = date.today() - timedelta(days=1)
        
        # Use same query structure as reconciliation report
        ops_query = """
        WITH ib_all AS (
            -- Get all known IBANs from our data tables
            SELECT DISTINCT iban
            FROM (
                SELECT DISTINCT iban FROM rpa_data.bai_rabobank_balances
                UNION
                SELECT DISTINCT iban FROM rpa_data.bai_rabobank_transactions
                UNION
                SELECT DISTINCT iban FROM rpa_data.bai_rabobank_account_info
            ) all_ibans
        ),
        audit_pivot AS (
            -- Check audit log for successful API calls for yesterday
            SELECT
                l.iban,
                CASE WHEN bool_or(l.response_status = 200) FILTER (WHERE l.endpoint = 'balances') 
                    THEN 'OK' ELSE 'MISSING' END AS balances_status,
                CASE WHEN bool_or(l.response_status = 200) FILTER (WHERE l.endpoint = 'transactions') 
                    THEN 'OK' ELSE 'MISSING' END AS transactions_status
            FROM rpa_data.bai_api_audit_log l
            WHERE l.closingdate::date = %s
                AND l.endpoint IN ('balances','transactions')
            GROUP BY l.iban
        ),
        daily_closing AS (
            SELECT
                iban,
                amount AS closing_balance
            FROM rpa_data.bai_rabobank_balances
            WHERE LOWER(balance_type) IN ('closingbooked', 'closing_booked')
                AND reference_date::date = %s
        ),
        daily_opening AS (
            SELECT
                iban,
                amount AS opening_balance
            FROM rpa_data.bai_rabobank_balances
            WHERE LOWER(balance_type) IN ('closingbooked', 'closing_booked')
                AND reference_date::date = %s
        ),
        daily_transactions AS (
            SELECT
                iban,
                SUM(transaction_amount) AS total_transactions
            FROM rpa_data.bai_rabobank_transactions
            WHERE booking_date::date = %s
            GROUP BY iban
        ),
        reconciliation AS (
            SELECT
                i.iban,
                CASE
                    WHEN o.opening_balance IS NOT NULL 
                        AND c.closing_balance IS NOT NULL
                        AND ABS(COALESCE(c.closing_balance, 0) - (COALESCE(o.opening_balance, 0) + COALESCE(dt.total_transactions, 0))) < 0.01 
                    THEN 'PERFECT_MATCH'
                    ELSE 'NO_MATCH'
                END AS match_status
            FROM ib_all i
            LEFT JOIN daily_opening o ON o.iban = i.iban
            LEFT JOIN daily_closing c ON c.iban = i.iban
            LEFT JOIN daily_transactions dt ON dt.iban = i.iban
        )
        SELECT
            COUNT(DISTINCT i.iban) as total_ibans,
            COUNT(DISTINCT CASE WHEN a.transactions_status = 'OK' THEN i.iban END) as tx_ok,
            COUNT(DISTINCT CASE WHEN a.balances_status = 'OK' THEN i.iban END) as bal_ok,
            COUNT(DISTINCT CASE WHEN r.match_status = 'PERFECT_MATCH' THEN i.iban END) as match_ok
        FROM ib_all i
        LEFT JOIN audit_pivot a ON i.iban = a.iban
        LEFT JOIN reconciliation r ON r.iban = i.iban
        """
        # Parameters: audit_log_date, closing_balance_date, opening_balance_date, transactions_date
        day_before_yesterday = yesterday - timedelta(days=1)
        result = db.execute_query(ops_query, (yesterday, yesterday, day_before_yesterday, yesterday))
        
        if not result or len(result) == 0:
            # Fallback if query returns no results
            expected_count = 28
            tx_count = 0
            bal_count = 0
            match_count = 0
        else:
            expected_count = result[0]['total_ibans'] if result[0]['total_ibans'] else 28
            tx_count = result[0]['tx_ok'] if result[0]['tx_ok'] else 0
            bal_count = result[0]['bal_ok'] if result[0]['bal_ok'] else 0
            match_count = result[0]['match_ok'] if result[0]['match_ok'] else 0
        
        # Calculate health status
        # Green: all 26 OK, Yellow: 20-25 OK, Red: <20 OK
        all_ok = (tx_count == expected_count and bal_count == expected_count and match_count == expected_count)
        most_ok = (tx_count >= 20 and bal_count >= 20 and match_count >= 20)
        
        if all_ok:
            bank_health = 'green'
            bank_status = 'Complete'
        elif most_ok:
            bank_health = 'yellow'
            bank_status = 'Partial'
        else:
            bank_health = 'red'
            bank_status = 'Issues'
        
        # Total transactions last day for CashApp node
        day_query = """
            SELECT COUNT(*) as total_count
            FROM rpa_data.bai_rabobank_transactions
            WHERE value_date = CURRENT_DATE - INTERVAL '1 day'
        """
        day_data = db.execute_query(day_query)
        total_day = day_data[0]['total_count'] if day_data else 0
        
        cashapp_health = 'green' if total_day > 0 else 'red'
        
        # Get last sync time
        sync_query = """
            SELECT MAX(created_at) as last_sync
            FROM rpa_data.bai_rabobank_transactions
            WHERE created_at::date = CURRENT_DATE
        """
        sync_data = db.execute_query(sync_query)
        last_sync = sync_data[0]['last_sync'] if sync_data and sync_data[0]['last_sync'] else None
        last_update_str = last_sync.strftime('%H:%M') if last_sync else 'N/A'
        
        # Get Autobank export data (live)
        # First get total configured exports
        autobank_total_query = """
            SELECT COUNT(1) as total_exports
            FROM rpa_data.bai_exports
            WHERE destination = 'Autobank'
                AND enabled = true
        """
        autobank_total_data = db.execute_query(autobank_total_query)
        autobank_total = autobank_total_data[0]['total_exports'] if autobank_total_data else 0
        
        # Get successful exports from audit log
        autobank_status_query = """
            SELECT 
                COUNT(DISTINCT iban) FILTER (WHERE success = true AND record_count > 0) as success_with_data,
                COUNT(DISTINCT iban) FILTER (WHERE success = true AND record_count = 0) as success_no_data,
                COUNT(DISTINCT iban) FILTER (WHERE success = false) as failed,
                MAX(timestamp) as last_created
            FROM rpa_data.bai_exports_audit_log
            WHERE destination = 'Autobank'
                AND timestamp::date = CURRENT_DATE - INTERVAL '1 day'
        """
        autobank_data = db.execute_query(autobank_status_query)
        
        if autobank_data and autobank_data[0]:
            autobank_success_data = autobank_data[0]['success_with_data'] or 0
            autobank_success_no_data = autobank_data[0]['success_no_data'] or 0
            autobank_failed = autobank_data[0]['failed'] or 0
            autobank_success = autobank_success_data + autobank_success_no_data
            autobank_last_export = autobank_data[0]['last_created']
            autobank_last_export_str = autobank_last_export.strftime('%H:%M') if autobank_last_export else 'N/A'
        else:
            autobank_success_data = 0
            autobank_success_no_data = 0
            autobank_failed = 0
            autobank_success = 0
            autobank_last_export_str = 'N/A'
        
        # Determine Autobank health
        if autobank_success == autobank_total and autobank_total > 0:
            autobank_health = 'green'
            autobank_status = 'Complete'
        elif autobank_success > 0:
            autobank_health = 'yellow'
            autobank_status = 'Partial'
        else:
            autobank_health = 'red'
            autobank_status = 'No Export'
        
        # Get database health statistics
        db_health_query = """
            SELECT 
                SUM(seq_scan) as sequential_scans,
                SUM(idx_scan) as index_scans,
                ROUND(SUM(idx_scan)::numeric / NULLIF(SUM(seq_scan + idx_scan), 0) * 100, 1) as index_hit_rate_pct,
                SUM(n_dead_tup) as total_dead_rows,
                SUM(n_live_tup) as total_live_rows,
                COUNT(*) as table_count
            FROM pg_stat_user_tables
            WHERE schemaname = 'rpa_data'
        """
        db_health_data = db.execute_query(db_health_query)
        
        # Get database size
        db_size_query = "SELECT pg_size_pretty(pg_database_size(current_database())) as db_size"
        db_size_data = db.execute_query(db_size_query)
        
        # Get active connections
        db_conn_query = """
            SELECT 
                COUNT(*) FILTER (WHERE state = 'active') as active_connections,
                COUNT(*) as total_connections
            FROM pg_stat_activity 
            WHERE datname = current_database()
        """
        db_conn_data = db.execute_query(db_conn_query)
        
        # Get cache hit ratio
        db_cache_query = """
            SELECT 
                ROUND(sum(blks_hit)::numeric / NULLIF(sum(blks_hit) + sum(blks_read), 0) * 100, 2) as cache_hit_ratio
            FROM pg_stat_database
            WHERE datname = current_database()
        """
        db_cache_data = db.execute_query(db_cache_query)
        
        # Get partition info for bai_rabobank_transactions
        db_partition_query = """
            SELECT 
                COUNT(*) as total_partitions,
                COUNT(*) FILTER (WHERE n_live_tup > 0) as filled_partitions,
                SUM(n_live_tup) as total_partition_rows
            FROM pg_stat_user_tables
            WHERE schemaname = 'rpa_data'
            AND (relname = 'bai_rabobank_transactions' OR relname LIKE 'bai_rabobank_transactions_%')
        """
        db_partition_data = db.execute_query(db_partition_query)
        
        if db_health_data and db_health_data[0]:
            db_stats = db_health_data[0]
            db_index_hit_rate = float(db_stats['index_hit_rate_pct']) if db_stats['index_hit_rate_pct'] else 0
            db_dead_rows = db_stats['total_dead_rows'] or 0
            db_seq_scans = db_stats['sequential_scans'] or 0
            db_idx_scans = db_stats['index_scans'] or 0
            db_live_rows = db_stats['total_live_rows'] or 0
            db_table_count = db_stats['table_count'] or 0
        else:
            db_index_hit_rate = 0
            db_dead_rows = 0
            db_seq_scans = 0
            db_idx_scans = 0
            db_live_rows = 0
            db_table_count = 0
        
        db_size = db_size_data[0]['db_size'] if db_size_data and db_size_data[0] else 'N/A'
        db_active_conn = db_conn_data[0]['active_connections'] if db_conn_data and db_conn_data[0] else 0
        db_total_conn = db_conn_data[0]['total_connections'] if db_conn_data and db_conn_data[0] else 0
        db_cache_hit = float(db_cache_data[0]['cache_hit_ratio']) if db_cache_data and db_cache_data[0] and db_cache_data[0]['cache_hit_ratio'] else 0
        db_total_partitions = db_partition_data[0]['total_partitions'] if db_partition_data and db_partition_data[0] else 0
        db_filled_partitions = db_partition_data[0]['filled_partitions'] if db_partition_data and db_partition_data[0] else 0
        
        return jsonify({
            'bank_input': {
                'source': 'Rabobank',
                'last_update': last_update_str,
                'tx_count': tx_count,
                'bal_count': bal_count,
                'match_count': match_count,
                'expected': expected_count,
                'status': bank_status,
                'health': bank_health
            },
            'bank_bnp': {
                'source': 'BNP Paribas',
                'tx_count': 14,
                'bal_count': 14,
                'match_count': 14,
                'expected': 14,
                'status': 'Healthy',
                'health': 'green',
                'last_update': datetime.now().strftime('%H:%M')
            },
            'bank_db': {
                'source': 'Deutsche Bank',
                'tx_count': 6,
                'bal_count': 6,
                'match_count': 6,
                'expected': 20,
                'status': 'Partial',
                'health': 'yellow',
                'last_update': datetime.now().strftime('%H:%M')
            },
            'cashapp_processing': {
                'total_day': int(total_day),
                'last_sync': datetime.now().strftime('%H:%M'),
                'status': 'Healthy',
                'health': cashapp_health
            },
            'autobank_output': {
                'rabo_accounts': autobank_success,
                'rabo_total': autobank_total,
                'success_data': autobank_success_data,
                'success_no_data': autobank_success_no_data,
                'failed': autobank_failed,
                'last_export': autobank_last_export_str,
                'status': autobank_status,
                'health': autobank_health,
                'message': f'Rabobank: {autobank_success}/{autobank_total} accounts'
            },
            'globes_output': {
                'rabo_accounts': 0,
                'rabo_total': 28,
                'last_export': 'N/A',
                'status': 'Not Enabled Yet',
                'health': 'grey',
                'message': 'Rabobank: 0/28 accounts'
            },
            'database_health': {
                'index_hit_rate': db_index_hit_rate,
                'dead_rows': db_dead_rows,
                'live_rows': db_live_rows,
                'sequential_scans': db_seq_scans,
                'index_scans': db_idx_scans,
                'table_count': db_table_count,
                'db_size': db_size,
                'active_connections': db_active_conn,
                'total_connections': db_total_conn,
                'cache_hit_ratio': db_cache_hit,
                'total_partitions': db_total_partitions,
                'filled_partitions': db_filled_partitions
            },
            'timestamp': datetime.now().isoformat()
        })
    except Exception as e:
        import traceback
        print(f"Error in ops_status: {str(e)}")
        print(traceback.format_exc())
        return jsonify({'error': str(e)}), 500

# Error handlers
@bai_bp.app_errorhandler(404)
def not_found(e):
    return render_template('404.html'), 404

@bai_bp.app_errorhandler(500)
def server_error(e):
    return render_template('500.html'), 500



