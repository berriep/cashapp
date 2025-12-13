# CashApp Database Migrations

## Overzicht

Deze folder bevat database migratie scripts voor CashApp. Migrations worden gebruikt om de database structuur te wijzigen zonder data te verliezen.

## Migratie Scripts

### Fase 1: Hernoem Users Tabel
**File:** `migration_001_rename_users_table.sql`

**Doel:** Hernoem `bai_monitor_users` naar `cashapp_users` om generieke platformnaam te gebruiken.

**Impact:**
- ✓ Backward compatible (tabel structuur blijft hetzelfde)
- ✓ Geen data verlies
- ⚠️ Vereist code update (alle queries naar nieuwe tabelnaam)

**Rollback:** `rollback_001_rename_users_table.sql`

### Fase 2: Module Permissions
**File:** `migration_002_add_module_permissions.sql`

**Doel:** Voeg module-specifieke toegangsrechten toe (BAI, Recon).

**Wijzigingen:**
- Voegt kolom `has_bai_access` toe (default TRUE voor bestaande users)
- Voegt kolom `has_recon_access` toe (default FALSE)
- Admin users krijgen automatisch toegang tot alle modules

**Impact:**
- ✓ Backward compatible (bestaande users behouden BAI toegang)
- ✓ Geen data verlies
- ⚠️ Vereist code update voor permission checks

**Rollback:** `rollback_002_add_module_permissions.sql`

## Migrations Uitvoeren

### Veilige Volgorde

1. **Test eerst in Accept omgeving** (als beschikbaar)
2. **Maak backup van productie database**
3. **Voer migrations uit**
4. **Update code**
5. **Test grondig**

### Stap 1: Fase 1 - Hernoem Tabel

```powershell
cd C:\Users\bpeijmen\Documents\Code\CashApp\database
$env:PYTHONPATH="C:\Users\bpeijmen\Documents\Code\CashApp"
py run_migration.py migration_001_rename_users_table.sql
```

Type `yes` om te bevestigen.

### Stap 2: Update Code

Na Fase 1 moet je de code updaten om `cashapp_users` te gebruiken in plaats van `bai_monitor_users`.

**Bestanden om te updaten:**
- `app/shared/auth.py` - alle queries

Voer dit uit:
```powershell
cd C:\Users\bpeijmen\Documents\Code\CashApp
py database\update_code_for_migrations.py fase1
```

### Stap 3: Test de App

Start de app en test of login/admin nog werkt:
```powershell
cd C:\Users\bpeijmen\Documents\Code\CashApp
$env:PYTHONPATH="C:\Users\bpeijmen\Documents\Code\CashApp"
py -m app.main
```

Test:
- ✓ Login werkt
- ✓ Admin users pagina werkt
- ✓ User aanmaken werkt

### Stap 4: Fase 2 - Module Permissions

```powershell
cd C:\Users\bpeijmen\Documents\Code\CashApp\database
$env:PYTHONPATH="C:\Users\bpeijmen\Documents\Code\CashApp"
py run_migration.py migration_002_add_module_permissions.sql
```

### Stap 5: Update Code voor Permissions

```powershell
cd C:\Users\bpeijmen\Documents\Code\CashApp
py database\update_code_for_migrations.py fase2
```

### Stap 6: Test Module Permissions

- ✓ Admin ziet BAI en Recon modules
- ✓ Restricted user ziet alleen toegestane modules
- ✓ Access denied bij unauthorized toegang

## Rollback

Als er problemen zijn, gebruik de rollback scripts:

### Rollback Fase 2:
```powershell
py run_migration.py rollback_002_add_module_permissions.sql
```

### Rollback Fase 1:
```powershell
py run_migration.py rollback_001_rename_users_table.sql
```

**Let op:** Na rollback moet je ook de code terugzetten!

## Handmatig Uitvoeren (zonder Python script)

Je kunt ook direct in psql:

```bash
psql -h psql-prd-weu-rpa-02.postgres.database.azure.com -U p1pgrpacp -d p1pgrpacp

\i migration_001_rename_users_table.sql
\i migration_002_add_module_permissions.sql
```

## Database Backup

Voor de zekerheid, maak eerst een backup:

```sql
-- Backup users tabel
CREATE TABLE rpa_data.bai_monitor_users_backup_20251211 AS 
SELECT * FROM rpa_data.bai_monitor_users;
```

Of via pgAdmin: Right-click op tabel → Backup
