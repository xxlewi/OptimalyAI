#!/bin/bash

# OptimalyAI PostgreSQL Docker Manager
# Jednoduchý skript pro správu PostgreSQL databáze

set -e

# Barvy pro výstup
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Funkce pro zobrazení menu
show_menu() {
    echo -e "\n${BLUE}════════════════════════════════════════${NC}"
    echo -e "${BLUE}   OptimalyAI PostgreSQL Manager${NC}"
    echo -e "${BLUE}════════════════════════════════════════${NC}\n"
    echo "1) 🚀 Start databáze"
    echo "2) 🛑 Stop databáze"
    echo "3) 🔄 Restart databáze"
    echo "4) 📊 Status"
    echo "5) 📝 Zobrazit logy"
    echo "6) 🔌 Připojit se do psql"
    echo "7) 🌐 Otevřít pgAdmin"
    echo "8) 💾 Backup databáze"
    echo "9) 📥 Restore databáze"
    echo "10) 🗑️  Smazat vše (POZOR!)"
    echo "11) ℹ️  Zobrazit connection string"
    echo "0) ❌ Konec"
    echo -e "\n${YELLOW}Vyber možnost:${NC} "
}

# Start databáze
start_db() {
    echo -e "${GREEN}Spouštím PostgreSQL a pgAdmin...${NC}"
    docker-compose up -d
    echo -e "${GREEN}✅ Databáze běží na localhost:5432${NC}"
    echo -e "${GREEN}✅ pgAdmin běží na http://localhost:5050${NC}"
    echo -e "${YELLOW}   Email: admin@optimaly.ai${NC}"
    echo -e "${YELLOW}   Heslo: admin123${NC}"
}

# Stop databáze
stop_db() {
    echo -e "${YELLOW}Zastavuji databázi...${NC}"
    docker-compose down
    echo -e "${GREEN}✅ Databáze zastavena${NC}"
}

# Status
show_status() {
    echo -e "${BLUE}Status služeb:${NC}"
    docker-compose ps
}

# Logy
show_logs() {
    echo -e "${BLUE}Zobrazuji posledních 50 řádků logů:${NC}"
    docker-compose logs --tail=50 postgres
}

# Připojení do psql
connect_psql() {
    echo -e "${GREEN}Připojuji se do PostgreSQL...${NC}"
    docker-compose exec postgres psql -U optimaly -d optimalyai_db
}

# Otevřít pgAdmin
open_pgadmin() {
    echo -e "${GREEN}Otevírám pgAdmin...${NC}"
    open http://localhost:5050
}

# Backup
backup_db() {
    TIMESTAMP=$(date +%Y%m%d_%H%M%S)
    BACKUP_FILE="backup_${TIMESTAMP}.sql"
    echo -e "${YELLOW}Vytvářím backup do ${BACKUP_FILE}...${NC}"
    docker-compose exec -T postgres pg_dump -U optimaly optimalyai_db > "backups/${BACKUP_FILE}"
    echo -e "${GREEN}✅ Backup vytvořen: backups/${BACKUP_FILE}${NC}"
}

# Restore
restore_db() {
    echo -e "${YELLOW}Dostupné backupy:${NC}"
    ls -la backups/*.sql 2>/dev/null || echo "Žádné backupy nenalezeny"
    echo -e "\n${YELLOW}Zadej název souboru pro restore:${NC} "
    read backup_file
    if [ -f "backups/${backup_file}" ]; then
        echo -e "${YELLOW}Obnovuji databázi z ${backup_file}...${NC}"
        docker-compose exec -T postgres psql -U optimaly optimalyai_db < "backups/${backup_file}"
        echo -e "${GREEN}✅ Databáze obnovena${NC}"
    else
        echo -e "${RED}❌ Soubor nenalezen${NC}"
    fi
}

# Smazat vše
destroy_all() {
    echo -e "${RED}⚠️  VAROVÁNÍ: Toto smaže všechna data!${NC}"
    echo -e "${YELLOW}Opravdu chceš smazat databázi a všechna data? (ano/ne):${NC} "
    read confirm
    if [ "$confirm" = "ano" ]; then
        docker-compose down -v
        echo -e "${GREEN}✅ Vše smazáno${NC}"
    else
        echo -e "${YELLOW}Akce zrušena${NC}"
    fi
}

# Connection string
show_connection() {
    echo -e "${BLUE}Connection string pro appsettings.json:${NC}"
    echo -e "${GREEN}\"DefaultConnection\": \"Host=localhost;Port=5432;Database=optimalyai_db;Username=optimaly;Password=OptimalyAI2024!\"${NC}"
    echo -e "\n${BLUE}Entity Framework migrations:${NC}"
    echo -e "${GREEN}dotnet ef migrations add InitialCreate${NC}"
    echo -e "${GREEN}dotnet ef database update${NC}"
}

# Vytvoření složky pro backupy
mkdir -p backups

# Hlavní smyčka
while true; do
    show_menu
    read choice
    
    case $choice in
        1) start_db ;;
        2) stop_db ;;
        3) stop_db && start_db ;;
        4) show_status ;;
        5) show_logs ;;
        6) connect_psql ;;
        7) open_pgadmin ;;
        8) backup_db ;;
        9) restore_db ;;
        10) destroy_all ;;
        11) show_connection ;;
        0) echo -e "${GREEN}Nashledanou!${NC}"; exit 0 ;;
        *) echo -e "${RED}Neplatná volba${NC}" ;;
    esac
    
    echo -e "\n${YELLOW}Stiskni Enter pro pokračování...${NC}"
    read
done