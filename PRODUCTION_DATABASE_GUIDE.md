# OptimalyAI - Průvodce produkční databází

## Rychlý start

### 1. Spuštění PostgreSQL databáze
```bash
# Spustit databázi
./docker-db-start.sh

# Zastavit databázi
./docker-db-stop.sh
```

### 2. Přepnutí na produkční databázi
V souboru `appsettings.json` změňte:
```json
"UseProductionDatabase": true
```

### 3. Spuštění aplikace
```bash
python run-dev.py
```

## Architektura databázového řešení

### Vývojové prostředí (výchozí)
- **Databáze**: In-Memory (Entity Framework Core)
- **Výhody**: 
  - Žádná konfigurace
  - Rychlé spuštění
  - Ideální pro vývoj a testování
- **Nevýhody**:
  - Data se ztratí po restartu
  - Omezená funkcionalita SQL

### Produkční prostředí
- **Databáze**: PostgreSQL 16
- **Docker Compose**: Automatická správa kontejnerů
- **pgAdmin**: Webové rozhraní pro správu databáze
- **Výhody**:
  - Plná podpora SQL
  - Trvalé uložení dat
  - Vysoký výkon
  - Podpora transakcí

## Správa databáze

### Migrace

```bash
# Vytvořit novou migraci
dotnet ef migrations add NazevMigrace

# Aplikovat migrace
dotnet ef database update

# Vrátit poslední migraci
dotnet ef migrations remove

# Zobrazit seznam migrací
dotnet ef migrations list
```

### Přímý přístup k databázi

```bash
# PostgreSQL klient
docker exec -it optimalyai-postgres psql -U optimaly -d optimalyai_db

# pgAdmin webové rozhraní
# URL: http://localhost:5050
# Email: admin@optimaly.ai
# Password: admin123
```

### SQL příkazy
```sql
-- Zobrazit všechny tabulky
\dt

-- Zobrazit strukturu tabulky
\d "BusinessRequests"

-- Počet požadavků
SELECT COUNT(*) FROM "BusinessRequests";

-- Zobrazit požadavky
SELECT "Id", "RequestNumber", "Title", "Status" 
FROM "BusinessRequests" 
ORDER BY "CreatedAt" DESC;
```

## Konfigurace

### Connection String
```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=optimalyai_db;Username=optimaly;Password=OptimalyAI2024!"
}
```

### Environmentální proměnné
```bash
# Použít produkční databázi
export UseProductionDatabase=true

# Nebo při spuštění
UseProductionDatabase=true dotnet run
```

## Zálohování a obnova

### Záloha databáze
```bash
# Vytvořit zálohu
docker exec optimalyai-postgres pg_dump -U optimaly optimalyai_db > backup.sql

# Komprimovaná záloha
docker exec optimalyai-postgres pg_dump -U optimaly -Fc optimalyai_db > backup.dump
```

### Obnovení databáze
```bash
# Z SQL souboru
docker exec -i optimalyai-postgres psql -U optimaly optimalyai_db < backup.sql

# Z komprimované zálohy
docker exec -i optimalyai-postgres pg_restore -U optimaly -d optimalyai_db < backup.dump
```

## Monitorování

### Zobrazit aktivní spojení
```sql
SELECT pid, usename, application_name, client_addr, state 
FROM pg_stat_activity 
WHERE datname = 'optimalyai_db';
```

### Velikost databáze
```sql
SELECT pg_database_size('optimalyai_db') / 1024 / 1024 AS size_mb;
```

### Velikost tabulek
```sql
SELECT 
  schemaname,
  tablename,
  pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) AS size
FROM pg_tables 
WHERE schemaname = 'public'
ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC;
```

## Řešení problémů

### Port 5432 je obsazený
```bash
# Najít proces používající port
lsof -i :5432

# Ukončit proces
kill -9 <PID>
```

### Nelze se připojit k databázi
```bash
# Zkontrolovat, zda kontejner běží
docker ps | grep optimalyai-postgres

# Zobrazit logy
docker logs optimalyai-postgres

# Restartovat kontejner
docker-compose restart postgres
```

### Smazat vše a začít znovu
```bash
# Zastavit a smazat kontejnery a data
docker-compose down -v

# Spustit znovu
./docker-db-start.sh

# Aplikovat migrace
dotnet ef database update
```

## Bezpečnost

### Doporučení pro produkci
1. **Změňte výchozí hesla** v `docker-compose.yml`
2. **Omezte přístup** k PostgreSQL pouze z aplikace
3. **Používejte SSL** pro šifrované spojení
4. **Pravidelně zálohujte** databázi
5. **Monitorujte** výkon a bezpečnost

### Příklad bezpečného connection stringu
```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=optimalyai_db;Username=optimaly;Password=<SECURE_PASSWORD>;SSL Mode=Require;Trust Server Certificate=true"
}
```

## Výkonové optimalizace

### Indexy
Aplikace automaticky vytváří indexy pro:
- `BusinessRequests.RequestNumber` (unique)
- `WorkflowTemplate.Name + Version` (unique)
- Všechny `CreatedAt` sloupce
- Foreign keys

### Vlastní indexy
```sql
-- Index pro vyhledávání podle klienta
CREATE INDEX IX_BusinessRequests_ClientId 
ON "BusinessRequests" ("ClientId") 
WHERE "ClientId" IS NOT NULL;

-- Index pro aktivní požadavky
CREATE INDEX IX_BusinessRequests_Status_Priority 
ON "BusinessRequests" ("Status", "Priority") 
WHERE "Status" IN (1, 2, 3); -- Queued, Processing, Failed
```