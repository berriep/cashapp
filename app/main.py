"""
CashApp - Unified Finance Platform
Combines BAI Monitor and Reconciliation tools
"""
import os
from flask import Flask, redirect, url_for
from flask_login import LoginManager, current_user
from config.config import Config

# Initialize Flask app with explicit template folder
app_root = os.path.dirname(os.path.abspath(__file__))
app = Flask(__name__, 
            template_folder=os.path.join(app_root, 'shared', 'templates'),
            static_folder=os.path.join(app_root, 'shared', 'static'))
app.config.from_object(Config)

# Initialize Flask-Login
login_manager = LoginManager()
login_manager.init_app(app)
login_manager.login_view = 'shared.login'

# Import and register blueprints
from app.shared.auth import shared_bp, User
from app.bai.routes import bai_bp
from app.recon.routes import recon_bp

app.register_blueprint(shared_bp)
app.register_blueprint(bai_bp, url_prefix='/bai')
app.register_blueprint(recon_bp, url_prefix='/recon')

@login_manager.user_loader
def load_user(user_id):
    return User.get(user_id)

# Root route - redirect to appropriate dashboard
@app.route('/')
def index():
    """Redirect to dashboard or login"""
    if current_user.is_authenticated:
        return redirect(url_for('shared.dashboard'))
    return redirect(url_for('shared.login'))

# Backwards compatibility redirects for old BAI URLs
@app.route('/dashboard')
def old_dashboard():
    return redirect(url_for('bai.dashboard'))

@app.route('/data-quality')
def old_data_quality():
    return redirect(url_for('bai.data_quality'))

@app.route('/missing-days')
def old_missing_days():
    return redirect(url_for('bai.missing_days'))

@app.route('/transaction-details')
def old_transaction_details():
    return redirect(url_for('bai.transaction_details'))

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000, debug=True)
