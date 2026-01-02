# Multi Business Line Implementation Plan

## Executive Summary

This document outlines the implementation plan to extend the CashApp BAI Monitor to support multiple Business Lines (BL). The current application is designed for a single BL, but future expansion requires support for multiple BLs with separate bank configurations while maintaining data integrity and isolation.

**Key Principle**: Use IBAN as the natural key to link data to Business Line configuration without altering existing transaction/statement tables.

---

## Current State

### Architecture
- Single Business Line (NL)
- File-based configuration (client_id, tokens, consent)
- Rabobank API integration
- Tables: `bai_files`, `bank_statements`, `transactions`, `reconciliation_payments`, etc.
- IBANs directly linked to data tables

### Limitations
- Cannot support multiple BLs
- Configuration tied to filesystem
- No BL isolation
- Cannot handle same bank with different API accounts per BL

---

## Target State

### Architecture
- Multi Business Line support
- Database-based configuration per BL
- Multiple bank configs per BL
- IBAN-based lookup for BL/config association
- No changes to existing data tables
- Backward compatible

### Key Features
- BL selector in UI
- Separate API credentials per BL per Bank
- Data filtering by BL via IBAN lookup
- Support for: BL-A (Rabobank), BL-B (Rabobank + ING)

---

## Database Design

### New Tables

```sql
-- ============================================
-- Business Lines (Top Level)
-- ============================================
CREATE TABLE business_lines (
    id INT PRIMARY KEY AUTO_INCREMENT,
    code VARCHAR(10) UNIQUE NOT NULL COMMENT 'Short code (e.g., NL, BE, DE)',
    name VARCHAR(100) NOT NULL COMMENT 'Display name (e.g., Netherlands, Belgium)',
    description TEXT,
    active BOOLEAN DEFAULT 1,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX idx_code (code),
    INDEX idx_active (active)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ============================================
-- Bank Configurations per BL
-- ============================================
CREATE TABLE bank_configs (
    id INT PRIMARY KEY AUTO_INCREMENT,
    business_line_id INT NOT NULL,
    bank_name VARCHAR(50) NOT NULL COMMENT 'Rabobank, ING, BNP Paribas, etc.',
    bank_code VARCHAR(10) COMMENT 'RABO, ING, BNP',
    
    -- API Credentials
    client_id VARCHAR(255) NOT NULL,
    client_secret_encrypted VARCHAR(500) COMMENT 'AES encrypted',
    
    -- OAuth/Consent
    consent_id VARCHAR(255),
    consent_expiry DATETIME,
    consent_status VARCHAR(20) DEFAULT 'ACTIVE' COMMENT 'ACTIVE, EXPIRED, REVOKED',
    
    -- Tokens
    access_token_encrypted TEXT COMMENT 'AES encrypted',
    refresh_token_encrypted TEXT COMMENT 'AES encrypted',
    token_expiry DATETIME,
    
    -- API Configuration
    api_base_url VARCHAR(255),
    api_version VARCHAR(10) DEFAULT 'v1',
    
    -- Status
    active BOOLEAN DEFAULT 1,
    last_api_call TIMESTAMP NULL,
    last_token_refresh TIMESTAMP NULL,
    
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    
    FOREIGN KEY (business_line_id) REFERENCES business_lines(id) ON DELETE CASCADE,
    UNIQUE KEY unique_bl_bank (business_line_id, bank_code),
    INDEX idx_bl_bank (business_line_id, bank_name),
    INDEX idx_active (active)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ============================================
-- IBANs linked to Bank Configs (KEY TABLE!)
-- ============================================
CREATE TABLE bl_ibans (
    id INT PRIMARY KEY AUTO_INCREMENT,
    bank_config_id INT NOT NULL,
    iban VARCHAR(34) UNIQUE NOT NULL COMMENT 'IBAN is the natural key!',
    owner_name VARCHAR(255),
    currency VARCHAR(3) DEFAULT 'EUR',
    account_type VARCHAR(20) DEFAULT 'CHECKING' COMMENT 'CHECKING, SAVINGS, etc.',
    active BOOLEAN DEFAULT 1,
    
    -- Metadata
    first_transaction_date DATE,
    last_transaction_date DATE,
    
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    
    FOREIGN KEY (bank_config_id) REFERENCES bank_configs(id) ON DELETE CASCADE,
    INDEX idx_iban (iban),
    INDEX idx_bank_config (bank_config_id),
    INDEX idx_active (active)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ============================================
-- API Call Log (for debugging/auditing)
-- ============================================
CREATE TABLE api_call_log (
    id INT PRIMARY KEY AUTO_INCREMENT,
    bank_config_id INT NOT NULL,
    iban VARCHAR(34),
    call_type VARCHAR(50) COMMENT 'GET_BALANCES, GET_TRANSACTIONS, REFRESH_TOKEN',
    request_date DATE,
    response_status INT COMMENT 'HTTP status code',
    response_time_ms INT,
    error_message TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (bank_config_id) REFERENCES bank_configs(id) ON DELETE CASCADE,
    INDEX idx_bank_iban_date (bank_config_id, iban, request_date),
    INDEX idx_created (created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
```

### Existing Tables
**No changes required!** Tables blijven zoals ze zijn:
- `bai_files`
- `bank_statements`
- `transactions`
- `reconciliation_payments`
- `export_status`

---

## Data Flow

### 1. IBAN Lookup Flow

```python
def get_business_line_for_iban(iban):
    """Returns BL and bank config info based on IBAN"""
    query = """
        SELECT 
            bl.id as bl_id,
            bl.code as bl_code,
            bl.name as bl_name,
            bc.id as bank_config_id,
            bc.bank_name,
            bc.client_id,
            bi.owner_name
        FROM bl_ibans bi
        JOIN bank_configs bc ON bi.bank_config_id = bc.id
        JOIN business_lines bl ON bc.business_line_id = bl.id
        WHERE bi.iban = ? AND bi.active = 1 AND bc.active = 1
    """
    return db.execute_query(query, (iban,))
```

### 2. API Call with Config

```python
def fetch_transactions(iban, date_from, date_to):
    # 1. Get config for this IBAN
    config = get_business_line_for_iban(iban)
    
    if not config:
        raise ValueError(f"No active configuration found for IBAN: {iban}")
    
    # 2. Get tokens for this bank config
    tokens = get_tokens(config['bank_config_id'])
    
    # 3. Make API call with correct credentials
    response = rabobank_api.get_transactions(
        client_id=config['client_id'],
        access_token=tokens['access_token'],
        iban=iban,
        date_from=date_from,
        date_to=date_to
    )
    
    # 4. Log API call
    log_api_call(config['bank_config_id'], iban, 'GET_TRANSACTIONS', response.status_code)
    
    return response.data
```

### 3. Dashboard Filtering

```python
@bai_bp.route('/dashboard')
def dashboard():
    # Get selected BL from session or query param
    selected_bl = request.args.get('bl') or session.get('business_line')
    
    if selected_bl:
        # Get IBANs for this BL
        ibans = get_ibans_for_business_line(selected_bl)
        iban_list = [iban['iban'] for iban in ibans]
        
        # Filter existing queries with iban_list
        transactions = db.get_transactions(iban_filter=iban_list)
    else:
        # Show all (or first BL by default)
        transactions = db.get_transactions()
    
    # Get all BLs for selector
    business_lines = db.get_active_business_lines()
    
    return render_template('dashboard.html', 
                         transactions=transactions,
                         business_lines=business_lines,
                         selected_bl=selected_bl)
```

---

## Implementation Phases

### Phase 1: Database Setup (Week 1)
**Goal**: Create new tables and migrate current config

#### Tasks
1. **Create tables** (1 day)
   - Execute SQL scripts for new tables
   - Add indexes
   - Test foreign key constraints

2. **Migration script** (2 days)
   - Create initial BL entry: `NL - Netherlands`
   - Migrate Rabobank file config to `bank_configs` table
   - Link existing IBANs to bank config in `bl_ibans`
   - Encrypt sensitive data (client_secret, tokens)
   
   ```python
   # migration_add_business_lines.py
   def migrate_config_to_database():
       # 1. Create NL business line
       bl_id = insert_business_line('NL', 'Netherlands')
       
       # 2. Read current file config
       config = load_rabobank_config()
       tokens = load_rabobank_tokens()
       
       # 3. Create bank config
       bc_id = insert_bank_config(
           bl_id, 
           'Rabobank',
           config['client_id'],
           encrypt(config['client_secret']),
           encrypt(tokens['access_token']),
           encrypt(tokens['refresh_token'])
       )
       
       # 4. Link existing IBANs
       existing_ibans = get_distinct_ibans_from_transactions()
       for iban in existing_ibans:
           insert_bl_iban(bc_id, iban)
   ```

3. **Testing** (1 day)
   - Verify data migration
   - Test IBAN lookup
   - Test config retrieval

**Deliverables**:
- New tables in database
- Migrated configuration
- Migration documentation

---

### Phase 2: Core Application Updates (Week 2)
**Goal**: Update application logic to use new config structure

#### Tasks
1. **Configuration Module** (2 days)
   - Create `config_manager.py` for database-based config
   - Encryption/decryption helpers
   - Config caching layer
   
   ```python
   # app/shared/config_manager.py
   class ConfigManager:
       def get_config_for_iban(self, iban):
           """Get bank config for specific IBAN"""
           
       def get_tokens(self, bank_config_id):
           """Get decrypted tokens"""
           
       def refresh_token(self, bank_config_id):
           """Refresh OAuth token and update DB"""
           
       def get_ibans_for_bl(self, business_line_code):
           """Get all IBANs for a business line"""
   ```

2. **Update API Calls** (2 days)
   - Modify Rabobank API service to use config from DB
   - Update token refresh logic
   - Add API call logging
   
   ```python
   # app/bai/rabobank_service.py
   def fetch_transactions(iban, date_from, date_to):
       config = config_manager.get_config_for_iban(iban)
       tokens = config_manager.get_tokens(config['bank_config_id'])
       # ... make API call with config
   ```

3. **Update Database Queries** (1 day)
   - Add helper functions for BL filtering
   - Test existing queries still work
   
   ```python
   # app/shared/database.py
   def get_transactions(self, iban_filter=None, bl_code=None):
       if bl_code:
           # Get IBANs for this BL
           iban_filter = self.get_ibans_for_bl(bl_code)
       # Continue with existing logic
   ```

**Deliverables**:
- Working config manager
- Updated API integration
- Backward compatible queries

---

### Phase 3: UI Updates (Week 3)
**Goal**: Add BL selection and filtering in UI

#### Tasks
1. **Navigation/Header** (1 day)
   - Add BL selector dropdown
   - Session/cookie for selected BL
   - Update all navigation links to include BL
   
   ```html
   <!-- app/bai/templates/base.html -->
   <nav class="navbar">
       <select id="bl-selector" class="form-select">
           <option value="">All Business Lines</option>
           {% for bl in business_lines %}
           <option value="{{ bl.code }}" 
                   {% if selected_bl == bl.code %}selected{% endif %}>
               {{ bl.name }}
           </option>
           {% endfor %}
       </select>
   </nav>
   
   <script>
   $('#bl-selector').change(function() {
       window.location.href = '?bl=' + $(this).val();
   });
   </script>
   ```

2. **Dashboard Updates** (1 day)
   - Filter data by selected BL
   - Show BL info in cards
   - Update charts to respect BL filter

3. **Admin Pages** (1 day)
   - BL management page (CRUD)
   - Bank config management page
   - IBAN assignment page
   
   ```
   /admin/business-lines
   /admin/bank-configs
   /admin/ibans
   ```

4. **Reports** (1 day)
   - Add BL column to exports
   - Filter reports by BL
   - Update reconciliation report

**Deliverables**:
- BL selector in UI
- Admin management pages
- Filtered dashboards and reports

---

### Phase 4: Testing & Second BL (Week 4)
**Goal**: Test with actual second BL, fix issues

#### Tasks
1. **Setup Second BL** (2 days)
   - Create BE (Belgium) business line
   - Configure bank credentials for BE
   - Add BE IBANs
   - Test API calls for BE

2. **Integration Testing** (2 days)
   - Test switching between BLs
   - Test data isolation
   - Test API calls use correct config
   - Test concurrent usage of multiple BLs

3. **Bug Fixes** (1 day)
   - Fix any issues found during testing
   - Performance optimization
   - Edge case handling

**Deliverables**:
- Working second BL
- Test report
- Bug fixes

---

### Phase 5: Documentation & Deployment (Week 5)
**Goal**: Document and deploy to production

#### Tasks
1. **Documentation** (2 days)
   - Admin guide: How to add new BL
   - User guide: How to switch between BLs
   - API documentation updates
   - Database schema documentation

2. **Deployment** (2 days)
   - Database migration scripts
   - Deployment runbook
   - Rollback plan
   - Production deployment

3. **Training** (1 day)
   - Train administrators
   - Train end users
   - Q&A session

**Deliverables**:
- Complete documentation
- Production deployment
- Trained users

---

## Technical Considerations

### Security
- **Token Encryption**: Use AES-256 encryption for sensitive data
- **Access Control**: Role-based access per BL
- **Audit Log**: Track all config changes and API calls

### Performance
- **Caching**: Cache config and BL->IBAN mappings
- **Indexes**: Proper indexing on IBAN, bank_config_id
- **Connection Pooling**: Separate connection pools per BL if needed

### Scalability
- **Database**: Current design supports unlimited BLs
- **API Rate Limits**: Track per bank_config_id, not globally
- **Storage**: Consider partitioning large tables by BL in future

### Backward Compatibility
- **Existing Code**: All existing queries work without BL parameter
- **Default BL**: If no BL selected, show first/all BLs
- **Migration**: Can run migration without downtime

---

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|-----------|
| Token encryption issues | High | Test thoroughly, have backup plan for file-based tokens |
| Performance degradation | Medium | Add caching, optimize queries, monitor performance |
| Data isolation breach | High | Extensive testing, code review, access control |
| Complex migration | Medium | Test on staging, have rollback plan |
| API credential management | High | Secure storage, limited access, audit logging |

---

## Success Criteria

✅ Multiple BLs can coexist with separate configurations  
✅ Same bank with different API accounts per BL works  
✅ UI allows easy BL selection and switching  
✅ Data is properly isolated per BL  
✅ No changes to existing transaction/statement tables  
✅ Backward compatible with existing functionality  
✅ API calls use correct credentials per BL/IBAN  
✅ Performance remains acceptable  
✅ Second BL successfully deployed  

---

## Timeline Summary

| Phase | Duration | Key Milestone |
|-------|----------|---------------|
| Phase 1: Database Setup | 1 week | Tables created, config migrated |
| Phase 2: Core Updates | 1 week | API calls use DB config |
| Phase 3: UI Updates | 1 week | BL selector working |
| Phase 4: Testing | 1 week | Second BL operational |
| Phase 5: Deployment | 1 week | Production ready |
| **Total** | **5 weeks** | Multi-BL support live |

---

## Next Steps

1. ✅ Review and approve this plan
2. ☐ Create backup of current system
3. ☐ Setup development/test environment
4. ☐ Execute Phase 1: Database Setup
5. ☐ Weekly status meetings during implementation

---

## Appendix A: Sample Configuration

### Example: Two Business Lines with Rabobank

```
Business Line: NL (Netherlands)
└── Bank Config: Rabobank NL
    ├── client_id: nl_client_123
    ├── IBANs:
    │   ├── NL91RABO0417164300
    │   └── NL12RABO9876543210

Business Line: BE (Belgium)  
└── Bank Config: Rabobank BE
    ├── client_id: be_client_456
    ├── IBANs:
    │   ├── BE68539007547034
    │   └── BE71096123456769
```

### Example: Business Line with Multiple Banks

```
Business Line: DE (Germany)
├── Bank Config: Deutsche Bank
│   ├── client_id: de_db_789
│   └── IBANs:
│       └── DE89370400440532013000
└── Bank Config: Commerzbank  
    ├── client_id: de_cb_012
    └── IBANs:
        └── DE44500105175407324931
```

---

## Appendix B: Database Migration Script

```sql
-- migration_001_add_business_lines.sql

-- Create tables
SOURCE create_business_lines_tables.sql;

-- Insert default BL
INSERT INTO business_lines (code, name, description, active) 
VALUES ('NL', 'Netherlands', 'Dutch business line', 1);

-- Migrate Rabobank config
-- (Run Python script to read file config and insert)

-- Verify migration
SELECT 
    bl.name,
    bc.bank_name,
    COUNT(bi.id) as iban_count
FROM business_lines bl
LEFT JOIN bank_configs bc ON bl.id = bc.business_line_id  
LEFT JOIN bl_ibans bi ON bc.id = bi.bank_config_id
GROUP BY bl.id, bc.id;
```

---

*Document Version: 1.0*  
*Last Updated: January 2, 2026*  
*Author: CashApp Development Team*
