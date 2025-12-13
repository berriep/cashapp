Top, ik pas de procedure aan met die expliciete vermelding.
Hier is de definitieve versie die je intern kunt gebruiken of in Word/Markdown kunt zetten:

---

# Procedure: Installeren van Rabobank BAI API certificaat op 2 servers

## Omgeving

* **Server 1 (CSR + export):** `eucceisapp51`

  * Map: `C:\Users\uipath\BAI\Certificate Production\`
  * Bestanden: `csr.inf`, `api_rabo.csr`, `api_rabo.pfx`
* **Server 2 (doel):** `eucceisapp50`

---

## 1. Op server eucceisapp51 (certificaatbron)

### 1.1 Certificaat aanvragen

1. Genereer CSR:

   ```powershell
   certreq -new "C:\Users\uipath\BAI\Certificate Production\csr.inf" "C:\Users\uipath\BAI\Certificate Production\api_rabo.csr"
   ```
2. Upload `api_rabo.csr` bij Networking4all/Rabobank.
3. Ontvang het uitgegeven certificaat en sla op als:
   `C:\Users\uipath\BAI\Certificate Production\api_rabo.cer`

### 1.2 Certificaat koppelen

```powershell
certreq -accept "C:\Users\uipath\BAI\Certificate Production\api_rabo.cer"
```

### 1.3 Exporteren naar PFX

1. Open **certlm.msc** → Personal → Certificates.
2. Zoek `CN=api.rabobank.centerparcs.nl`.
3. Exporteren → **Yes, export private key**.
4. Encryptie: **AES256-SHA256**.
5. **Gebruik het wachtwoord dat in KeePass is opgeslagen onder de naam “Rabo\_cert pfx”.**
6. Opslaan als:
   `C:\Users\uipath\BAI\Certificate Production\api_rabo.pfx`

---

## 2. Overzetten naar server eucceisapp50

### 2.1 Veilig kopiëren

Gebruik een beveiligde methode, bijvoorbeeld:

```powershell
Copy-Item "C:\Users\uipath\BAI\Certificate Production\api_rabo.pfx" "\\eucceisapp50\C$\Temp\api_rabo.pfx"
```

Of via een beveiligde share, SFTP of versleutelde USB-stick.

---

## 3. Op server eucceisapp50 (doelserver)

### 3.1 Importeren via PowerShell

1. Open KeePass, kopieer het wachtwoord onder de naam **“Rabo\_cert pfx”**.
2. Run het importscript met dit wachtwoord:

```powershell
$pwd = ConvertTo-SecureString -String "<WACHTWOORD_UIT_KEEPASS>" -Force -AsPlainText
Import-PfxCertificate -FilePath "C:\Temp\api_rabo.pfx" `
  -CertStoreLocation "Cert:\LocalMachine\My" `
  -Password $pwd
```

*(vervang `<WACHTWOORD_UIT_KEEPASS>` door het echte wachtwoord uit KeePass onder de naam "Rabo_cert pfx" tijdens het uitvoeren, niet opslaan in scripts of bestanden! )*

### 3.2 Controleren

```powershell
Get-ChildItem Cert:\LocalMachine\My | Select-Object Subject, Thumbprint
```

Het certificaat en de thumbprint moeten identiek zijn aan die op `eucceisapp51`.

---

## 4. Opruimen

* Verwijder de `.pfx` na installatie van beide servers.
* Laat alleen het certificaat + private key in de **Windows Certificate Store** staan.
* Bewaar het exportwachtwoord uitsluitend in KeePass (naam: **Rabo\_cert pfx**).

---

Resultaat: zowel **eucceisapp51** als **eucceisapp50** beschikken over hetzelfde Rabobank BAI API certificaat (met private key) en kunnen via mTLS verbinding maken.

---

Wil je dat ik dit meteen omzet naar een **netjes opgemaakte Word-template** (met koppen, genummerde stappen en screenshots-plekholders), zodat je het als officiële werkinstructie kunt verspreiden?
