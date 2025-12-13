# CashApp User Management & Module Permissions - Implementation Summary

## Datum: 11 December 2025

## Overzicht
Complete implementatie van:
1. **Database hernoem**: `bai_monitor_users` → `cashapp_users`
2. **Module permissions**: BAI en Recon toegangsrechten per user

---

## Fase 1: Database Migratie

### SQL Scripts Gemaakt
✅ `/database/migration_001_rename_users_table.sql` - Hernoem tabel
✅ `/database/migration_002_add_module_permissions.sql` - Voeg permission kolommen toe
✅ `/database/rollback_001_rename_users_table.sql` - Rollback optie
✅ `/database/rollback_002_add_module_permissions.sql` - Rollback optie

### Uitvoeren
```powershell
cd C:\Users\bpeijmen\Documents\Code\CashApp\database
$env:PYTHONPATH="C:\Users\bpeijmen\Documents\Code\CashApp"

# Stap 1: Hernoem tabel
py run_migration.py migration_001_rename_users_table.sql

# Stap 2: Voeg permissions toe
py run_migration.py migration_002_add_module_permissions.sql
```

**⚠️ LET OP:** Voer eerst uit in test omgeving! Maak backup van productie database.

---

## Fase 2: Code Wijzigingen

### ✅ Voltooide Wijzigingen

#### 1. User Class (`app/shared/auth.py`)
- **`__init__` method**: Toegevoegd `has_bai_access` en `has_recon_access` parameters
- **`get()` method**: Query update om permissions op te halen
- **`get_all_users()` method**: Query update met permission kolommen
- **`create_user()` method**: Permissions parameters toegevoegd
- **`update_user()` method**: Permissions parameters toegevoegd
- **Alle queries**: `bai_monitor_users` → `cashapp_users`

#### 2. Access Control Decorators (`app/shared/decorators.py`) - NIEUW
```python
@require_bai_access    # Voor BAI module routes
@require_recon_access  # Voor Recon module routes
@require_admin         # Voor admin routes
```

#### 3. Dashboard (`app/shared/templates/dashboard.html`)
- Module kaarten conditionally zichtbaar op basis van permissions
- Quick access alleen voor toegestane modules
- "No Access" boodschap voor users zonder permissions

#### 4. Admin Templates
- **`admin/users.html`**: Kolommen toegevoegd voor BAI/Recon toegang
- **`admin/create_user.html`**: Checkboxes voor module permissions
- **`admin/edit_user.html`**: Checkboxes voor module permissions

#### 5. Admin Routes (`app/shared/auth.py`)
- **`admin_create_user`**: Verwerkt `has_bai_access` en `has_recon_access` POST data
- **`admin_edit_user`**: Verwerkt permission updates

---

## Fase 3: BAI Routes Protection (TODO)

### Volgende Stap: Voeg Decorators Toe
Alle BAI routes moeten beschermd worden met `@require_bai_access`:

```python
# In app/bai/routes.py
from app.shared.decorators import require_bai_access

@bai_bp.route('/dashboard')
@login_required
@require_bai_access  # <- TOEVOEGEN
def dashboard():
    ...
```

**Te beschermen routes:**
- `/bai/dashboard`
- `/bai/transaction-details`
- `/bai/balances`
- `/bai/bank-statements`
- `/bai/reports`
- `/bai/api/*`

### Automated Script
```powershell
py database\add_decorators_to_bai.py
```

---

## Fase 4: Testing

### Test Scenario's

#### Test 1: Admin User
```
✓ Ziet beide modules (BAI en Recon) op dashboard
✓ Kan BAI routes benaderen
✓ Kan Recon routes benaderen
✓ Kan users aanmaken met permissions
✓ Kan user permissions wijzigen
```

#### Test 2: BAI-only User
```
✓ Ziet alleen BAI module op dashboard
✓ Kan BAI routes benaderen
✗ Recon module niet zichtbaar
✗ Toegang tot /recon/dashboard geweigerd
```

#### Test 3: Recon-only User
```
✓ Ziet alleen Recon module op dashboard
✓ Kan Recon routes benaderen
✗ BAI module niet zichtbaar
✗ Toegang tot /bai/dashboard geweigerd
```

#### Test 4: No Access User
```
✓ Dashboard toont "No Module Access" boodschap
✗ Geen modules zichtbaar
✗ Toegang tot alle module routes geweigerd
```

### Test Users Aanmaken
```sql
-- Admin (alles)
INSERT INTO rpa_data.cashapp_users (username, password_hash, is_admin, has_bai_access, has_recon_access, is_active)
VALUES ('admin', '<hash>', TRUE, TRUE, TRUE, TRUE);

-- BAI alleen
INSERT INTO rpa_data.cashapp_users (username, password_hash, is_admin, has_bai_access, has_recon_access, is_active)
VALUES ('bai_user', '<hash>', FALSE, TRUE, FALSE, TRUE);

-- Recon alleen
INSERT INTO rpa_data.cashapp_users (username, password_hash, is_admin, has_bai_access, has_recon_access, is_active)
VALUES ('recon_user', '<hash>', FALSE, FALSE, TRUE, TRUE);

-- Geen toegang
INSERT INTO rpa_data.cashapp_users (username, password_hash, is_admin, has_bai_access, has_recon_access, is_active)
VALUES ('no_access', '<hash>', FALSE, FALSE, FALSE, TRUE);
```

---

## Database Schema na Migratie

```sql
-- rpa_data.cashapp_users (voorheen bai_monitor_users)
id              INTEGER PRIMARY KEY
username        VARCHAR(255) UNIQUE NOT NULL
password_hash   VARCHAR(255) NOT NULL
email           VARCHAR(255)
full_name       VARCHAR(255)
is_admin        BOOLEAN DEFAULT FALSE
has_bai_access  BOOLEAN DEFAULT TRUE    -- NIEUW
has_recon_access BOOLEAN DEFAULT FALSE  -- NIEUW
is_active       BOOLEAN DEFAULT TRUE
created_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP
last_login      TIMESTAMP
```

---

## Rollback Plan

Als er problemen zijn:

### Stap 1: Rollback Database
```powershell
cd C:\Users\bpeijmen\Documents\Code\CashApp\database
py run_migration.py rollback_002_add_module_permissions.sql
py run_migration.py rollback_001_rename_users_table.sql
```

### Stap 2: Restore Code
```powershell
# Restore auth.py
Copy-Item app\shared\auth_before_permissions.py.backup app\shared\auth.py -Force

# Verwijder decorators.py
Remove-Item app\shared\decorators.py

# Restore dashboard.html
git checkout app/shared/templates/dashboard.html
```

---

## Productie Deployment Checklist

- [ ] Backup productie database
- [ ] Test migrations in accept omgeving
- [ ] Verifieer alle test scenario's werken
- [ ] Communiceer downtime (5-10 minuten)
- [ ] Voer Fase 1 migration uit (hernoem tabel)
- [ ] Restart Flask app
- [ ] Test login en admin functionaliteit
- [ ] Voer Fase 2 migration uit (add permissions)
- [ ] Restart Flask app
- [ ] Test module access control
- [ ] Update alle bestaande users met correcte permissions
- [ ] Monitor logs voor errors

---

## Voordelen van Deze Implementatie

✅ **Backward Compatible**: Bestaande users behouden BAI toegang
✅ **Flexible**: Eenvoudig nieuwe modules toevoegen
✅ **Admin Override**: Admins hebben altijd toegang tot alles
✅ **User-Friendly**: Duidelijke UI voor permission management
✅ **Secure**: Decorator-based access control op route niveau
✅ **Rollback Ready**: Complete rollback scripts beschikbaar

---

## Toekomstige Uitbreidingen

### Meer Granulaire Permissions
In de toekomst kun je uitbreiden naar role-based permissions:

```sql
CREATE TABLE rpa_data.cashapp_roles (
    id SERIAL PRIMARY KEY,
    name VARCHAR(50) UNIQUE NOT NULL,
    description TEXT
);

CREATE TABLE rpa_data.cashapp_user_roles (
    user_id INTEGER REFERENCES cashapp_users(id),
    role_id INTEGER REFERENCES cashapp_roles(id),
    PRIMARY KEY (user_id, role_id)
);

CREATE TABLE rpa_data.cashapp_role_permissions (
    role_id INTEGER REFERENCES cashapp_roles(id),
    module VARCHAR(50),
    can_view BOOLEAN DEFAULT TRUE,
    can_edit BOOLEAN DEFAULT FALSE,
    can_delete BOOLEAN DEFAULT FALSE,
    can_export BOOLEAN DEFAULT FALSE
);
```

Maar voor nu is de simpele `has_bai_access` / `has_recon_access` voldoende!

---

## Contact & Support

Voor vragen over deze implementatie, zie:
- `/database/README.md` - Gedetailleerde migration instructies
- `/app/shared/decorators.py` - Decorator documentatie
- `/app/shared/auth.py` - User class implementatie
