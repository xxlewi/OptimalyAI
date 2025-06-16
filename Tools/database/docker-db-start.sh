#!/bin/bash

# Barvy pro výstup
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}🐳 OptimalyAI Docker PostgreSQL Manager${NC}"
echo "========================================"

# Zkontrolovat, jestli je Docker nainstalován
if ! command -v docker &> /dev/null; then
    echo -e "${RED}❌ Docker není nainstalován. Nainstalujte Docker a zkuste to znovu.${NC}"
    exit 1
fi

# Zkontrolovat, jestli Docker běží
if ! docker info &> /dev/null; then
    echo -e "${RED}❌ Docker není spuštěný. Spusťte Docker a zkuste to znovu.${NC}"
    exit 1
fi

# Spustit PostgreSQL
echo -e "${YELLOW}🚀 Spouštím PostgreSQL databázi...${NC}"
docker-compose up -d postgres

# Počkat na spuštění databáze
echo -e "${YELLOW}⏳ Čekám na spuštění databáze...${NC}"
sleep 5

# Zkontrolovat status
if docker-compose ps | grep -q "optimalyai-postgres.*Up"; then
    echo -e "${GREEN}✅ PostgreSQL databáze běží!${NC}"
    echo ""
    echo "📊 Informace o připojení:"
    echo "   Host: localhost"
    echo "   Port: 5432"
    echo "   Database: optimalyai_db"
    echo "   Username: optimaly"
    echo "   Password: OptimalyAI2024!"
    echo ""
    echo "🔗 Connection string:"
    echo "   Host=localhost;Port=5432;Database=optimalyai_db;Username=optimaly;Password=OptimalyAI2024!"
    echo ""
    echo "💡 Pro použití produkční databáze nastavte v appsettings.json:"
    echo "   \"UseProductionDatabase\": true"
else
    echo -e "${RED}❌ Nepodařilo se spustit PostgreSQL databázi${NC}"
    echo "Zkontrolujte logy pomocí: docker-compose logs postgres"
    exit 1
fi