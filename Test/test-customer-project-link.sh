#!/bin/bash

# Test pro ověření párování zákazníků a projektů

echo "========================================="
echo "Test párování zákazníků a projektů"
echo "========================================="

BASE_URL="https://localhost:5005"

# Získání seznamu zákazníků
echo -e "\n1. Získávání seznamu zákazníků..."
CUSTOMERS=$(curl -s -k "${BASE_URL}/api/customers" | jq -r '.data[] | "\(.id):\(.name)"' | head -5)

if [ -z "$CUSTOMERS" ]; then
    echo "Chyba: Žádní zákazníci nenalezeni!"
    exit 1
fi

echo "Nalezení zákazníci:"
echo "$CUSTOMERS"

# Vezmi prvního zákazníka
CUSTOMER_ID=$(echo "$CUSTOMERS" | head -1 | cut -d: -f1)
CUSTOMER_NAME=$(echo "$CUSTOMERS" | head -1 | cut -d: -f2)

echo -e "\n2. Používám zákazníka: $CUSTOMER_NAME (ID: $CUSTOMER_ID)"

# Vytvoření nového projektu pro tohoto zákazníka
echo -e "\n3. Vytváření nového projektu pro zákazníka..."

PROJECT_DATA=$(cat <<EOF
{
  "name": "Test Project $(date +%s)",
  "description": "Test project for customer link verification",
  "customerId": "$CUSTOMER_ID",
  "customerName": "$CUSTOMER_NAME", 
  "customerRequirement": "Test requirement",
  "status": 0,
  "projectType": "AI Development",
  "priority": 1,
  "startDate": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "estimatedHours": 10,
  "hourlyRate": 1500
}
EOF
)

echo "Odesílám data projektu:"
echo "$PROJECT_DATA" | jq .

PROJECT_RESPONSE=$(curl -s -k -X POST "${BASE_URL}/api/projects" \
  -H "Content-Type: application/json" \
  -d "$PROJECT_DATA")

echo -e "\nOdpověď serveru:"
echo "$PROJECT_RESPONSE" | jq .

PROJECT_ID=$(echo "$PROJECT_RESPONSE" | jq -r '.data.id')

if [ "$PROJECT_ID" == "null" ] || [ -z "$PROJECT_ID" ]; then
    echo "Chyba: Projekt nebyl vytvořen!"
    exit 1
fi

echo -e "\n4. Projekt úspěšně vytvořen s ID: $PROJECT_ID"

# Ověření, že projekt má správné CustomerId
echo -e "\n5. Ověřuji detail projektu..."
PROJECT_DETAIL=$(curl -s -k "${BASE_URL}/api/projects/$PROJECT_ID")

echo "Detail projektu:"
echo "$PROJECT_DETAIL" | jq '.data | {id, name, customerId, customerName}'

SAVED_CUSTOMER_ID=$(echo "$PROJECT_DETAIL" | jq -r '.data.customerId')

if [ "$SAVED_CUSTOMER_ID" == "$CUSTOMER_ID" ]; then
    echo -e "\n✅ CustomerId bylo správně uloženo!"
else
    echo -e "\n❌ CustomerId nebylo správně uloženo!"
    echo "Očekávané: $CUSTOMER_ID"
    echo "Skutečné: $SAVED_CUSTOMER_ID"
fi

# Ověření, že projekt je vidět v detailu zákazníka
echo -e "\n6. Ověřuji detail zákazníka..."
CUSTOMER_DETAIL=$(curl -s -k "${BASE_URL}/api/customers/$CUSTOMER_ID")

echo "Počet projektů zákazníka:"
echo "$CUSTOMER_DETAIL" | jq '.data | {projectsCount, recentProjects: .recentProjects | length}'

# Kontrola, zda je náš projekt v seznamu
PROJECT_FOUND=$(echo "$CUSTOMER_DETAIL" | jq --arg pid "$PROJECT_ID" '.data.recentProjects[]? | select(.id == $pid)')

if [ -n "$PROJECT_FOUND" ]; then
    echo -e "\n✅ Projekt je správně zobrazen v detailu zákazníka!"
    echo "$PROJECT_FOUND" | jq .
else
    echo -e "\n❌ Projekt není zobrazen v detailu zákazníka!"
    echo "Seznam projektů:"
    echo "$CUSTOMER_DETAIL" | jq '.data.recentProjects[]? | {id, name}'
fi

echo -e "\n========================================="
echo "Test dokončen"
echo "========================================="