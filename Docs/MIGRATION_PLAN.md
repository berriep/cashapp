# CashApp Migration Plan

## Overview
This document guides the migration from separate BAI_Tool and Recon_Tool to a unified CashApp platform.

## Current Status ✅

### Completed:
- [x] Created unified folder structure
- [x] Setup main Flask app (`app/main.py`) with blueprint architecture
- [x] Created configuration management (`config/config.py`)
- [x] Setup shared authentication module (`app/shared/auth.py`)
- [x] Copied database module to shared (`app/shared/database.py`)
- [x] Created requirements.txt with all dependencies
- [x] Setup .gitignore and .env.example
- [x] Created README.md with project documentation

### Project Structure:
```
CashApp/
├── app/
│   ├── main.py              ✅ Main Flask app with blueprints
│   ├── shared/              ✅ Shared components
│   │   ├── auth.py          ✅ Authentication (Blueprint)
│   │   ├── database.py      ✅ Database layer
│   │   └── templates/       ⏳ Need to create
│   ├── bai/                 ⏳ Need to migrate
│   │   ├── routes.py        📝 Empty (needs BAI routes)
│   │   └── templates/       ⏳ Need to copy
│   └── recon/               ⏳ Need to build
│       ├── routes.py        📝 Empty (placeholder)
│       └── templates/       ⏳ Need to create
├── config/
│   └── config.py            ✅ Configuration
├── BAI_Tool/                ✅ Original (reference)
├── Recon_Tool/              ✅ Original (reference)
├── requirements.txt         ✅ Dependencies
├── .env.example             ✅ Example environment
├── .gitignore               ✅ Git ignore rules
└── README.md                ✅ Documentation
```

---

## Next Steps - BAI Module Migration

### Phase 1: Copy BAI Templates (15 min)

1. **Create template folders:**
   ```powershell
   cd C:\Users\bpeijmen\Documents\Code\CashApp
   mkdir app\bai\templates
   mkdir app\shared\templates
   ```

2. **Copy BAI templates:**
   ```powershell
   Copy-Item "BAI_Tool\Monitor\app\templates\*" "app\bai\templates\" -Recurse
   ```

3. **Move shared templates:**
   ```powershell
   Move-Item "app\bai\templates\base.html" "app\shared\templates\"
   Move-Item "app\bai\templates\login.html" "app\shared\templates\"
   ```

### Phase 2: Create BAI Routes Blueprint (30 min)

**File:** `app/bai/routes.py`

Need to convert `BAI_Tool/Monitor/app/main.py` to a Blueprint:

**Changes needed:**
1. Import Blueprint instead of Flask
2. Change `@app.route` to `@bai_bp.route`
3. Change `from app.database import db` to `from app.shared.database import db`
4. Remove Flask-Login setup (now in main.py)
5. Keep all route logic exactly the same

**Key routes to migrate:**
- `/dashboard` → stays as `/dashboard` (but accessed via `/bai/dashboard`)
- `/data-quality` → stays as `/data-quality`
- `/missing-days` → stays as `/missing-days`
- `/transaction-details` → stays as `/transaction-details`
- `/transaction-export` → stays as `/transaction-export`
- `/export-pdf` → stays as `/export-pdf`

**Template:**
```python
from flask import Blueprint, render_template, request, ...
from flask_login import login_required, current_user
from app.shared.database import db
from datetime import datetime, timedelta
import json

# Create BAI blueprint
bai_bp = Blueprint('bai', __name__, template_folder='templates')

@bai_bp.route('/dashboard')
@login_required
def dashboard():
    # Copy exact logic from BAI_Tool/Monitor/app/main.py
    ...

# ... rest of routes
```

###  Phase 3: Update Template References (15 min)

In all BAI templates (`app/bai/templates/*.html`):

1. **Update base template reference:**
   ```html
   <!-- OLD -->
   {% extends "base.html" %}
   
   <!-- NEW -->
   {% extends "shared/base.html" %}
   ```

2. **Update url_for() calls:**
   ```html
   <!-- OLD -->
   {{ url_for('dashboard') }}
   {{ url_for('data_quality') }}
   
   <!-- NEW -->
   {{ url_for('bai.dashboard') }}
   {{ url_for('bai.data_quality') }}
   ```

3. **Shared routes (login/logout):**
   ```html
   <!-- OLD -->
   {{ url_for('login') }}
   {{ url_for('logout') }}
   
   <!-- NEW -->
   {{ url_for('shared.login') }}
   {{ url_for('shared.logout') }}
   ```

### Phase 4: Create Shared Dashboard (15 min)

**File:** `app/shared/templates/dashboard.html`

Create a main landing page with links to both modules:

```html
{% extends "base.html" %}

{% block title %}CashApp - Finance Platform{% endblock %}

{% block content %}
<div class="container mt-5">
    <h1>Welcome to CashApp</h1>
    <p class="lead">Unified Finance Platform</p>
    
    <div class="row mt-4">
        <div class="col-md-6">
            <div class="card">
                <div class="card-body">
                    <h3><i class="bi bi-bank"></i> BAI Monitor</h3>
                    <p>Transaction monitoring and data quality tracking</p>
                    <a href="{{ url_for('bai.dashboard') }}" class="btn btn-primary">
                        Open BAI Dashboard
                    </a>
                </div>
            </div>
        </div>
        
        <div class="col-md-6">
            <div class="card">
                <div class="card-body">
                    <h3><i class="bi bi-check2-square"></i> Reconciliation</h3>
                    <p>Automated reconciliation and matching</p>
                    <a href="{{ url_for('recon.dashboard') }}" class="btn btn-primary">
                        Open Recon Dashboard
                    </a>
                </div>
            </div>
        </div>
    </div>
</div>
{% endblock %}
```

### Phase 5: Setup Environment (10 min)

1. **Create virtual environment:**
   ```powershell
   cd C:\Users\bpeijmen\Documents\Code\CashApp
   python -m venv venv
   .\venv\Scripts\Activate.ps1
   ```

2. **Install dependencies:**
   ```powershell
   pip install -r requirements.txt
   ```

3. **Create .env file:**
   ```powershell
   Copy-Item .env.example .env
   ```

4. **Edit .env with your database credentials**

### Phase 6: Test BAI Module (15 min)

1. **Run the app:**
   ```powershell
   $env:FLASK_ENV="development"
   python -m app.main
   ```

2. **Test these URLs:**
   - http://localhost:5000 → Should redirect to login
   - http://localhost:5000/login → Login page
   - http://localhost:5000/dashboard → Main dashboard (after login)
   - http://localhost:5000/bai/dashboard → BAI dashboard
   - http://localhost:5000/bai/data-quality → Data quality page
   - http://localhost:5000/bai/missing-days → Missing days page
   - http://localhost:5000/bai/transaction-details → Transaction details

3. **Test backwards compatibility:**
   - http://localhost:5000/dashboard → Should redirect to /bai/dashboard
   - http://localhost:5000/data-quality → Should redirect to /bai/data-quality

---

## Recon Module - Future Work

### Phase 7: Recon Module Structure (Later)

**File:** `app/recon/routes.py`

```python
from flask import Blueprint, render_template
from flask_login import login_required

recon_bp = Blueprint('recon', __name__, template_folder='templates')

@recon_bp.route('/dashboard')
@login_required
def dashboard():
    """Recon dashboard"""
    return render_template('recon_dashboard.html')

# Add more routes as needed
```

---

## Docker & Azure Deployment - Future

### Phase 8: Containerization (Later)

Files to create:
- `Dockerfile`
- `docker-compose.yml`
- `.dockerignore`

### Phase 9: Azure Deployment (Later)

Files to create:
- `.github/workflows/deploy.yml` (CI/CD)
- Azure Container Apps configuration
- Database connection setup

---

## Troubleshooting

### Common Issues:

**Import errors:**
- Make sure all `from app.database` are changed to `from app.shared.database`
- Make sure all `from config.config` use correct path

**Template not found:**
- Check template_folder in Blueprint definition
- Verify file paths in `app/bai/templates/`
- Use `shared/` prefix for shared templates

**Database connection:**
- Verify .env file has correct credentials
- Test database connection independently
- Check PostgreSQL is running

**URL routing:**
- Remember routes now have `/bai/` prefix
- Update all `url_for()` to include blueprint name
- Check backwards compatibility redirects

---

## Rollback Plan

If anything goes wrong:
1. Stop the new app
2. Go back to original folders:
   - `cd C:\Users\bpeijmen\Documents\Code\BAI_Tool\Monitor`
   - Run original app as before
3. Review errors and fix in CashApp
4. Try again

---

## Progress Checklist

- [ ] Phase 1: Copy BAI templates
- [ ] Phase 2: Create BAI routes blueprint
- [ ] Phase 3: Update template references
- [ ] Phase 4: Create shared dashboard
- [ ] Phase 5: Setup environment
- [ ] Phase 6: Test BAI module
- [ ] Phase 7: Recon module (future)
- [ ] Phase 8: Docker (future)
- [ ] Phase 9: Azure (future)

---

## Notes

- Original `BAI_Tool/` and `Recon_Tool/` folders remain as reference
- Don't delete originals until CashApp is fully tested
- Git commit after each completed phase
- Test thoroughly before moving to next phase
