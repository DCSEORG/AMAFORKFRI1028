#!/bin/bash
set -e

# Color output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}Azure Expense Management App Deployment${NC}"
echo -e "${BLUE}========================================${NC}"

# Variables (customize these as needed)
RESOURCE_GROUP="rg-expense-mgmt-dev"
LOCATION="uksouth"
DEPLOYMENT_NAME="expense-mgmt-$(date +%Y%m%d-%H%M%S)"

# Create resource group if it doesn't exist
echo -e "\n${BLUE}Creating resource group...${NC}"
az group create \
  --name $RESOURCE_GROUP \
  --location $LOCATION \
  --output table

# Deploy infrastructure (without GenAI)
echo -e "\n${BLUE}Deploying Azure infrastructure...${NC}"
DEPLOYMENT_OUTPUT=$(az deployment group create \
  --resource-group $RESOURCE_GROUP \
  --template-file infrastructure/main.bicep \
  --parameters deployGenAI=false \
  --name $DEPLOYMENT_NAME \
  --output json)

# Extract outputs
APP_SERVICE_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.properties.outputs.appServiceName.value')
APP_SERVICE_URL=$(echo $DEPLOYMENT_OUTPUT | jq -r '.properties.outputs.appServiceUrl.value')
MANAGED_IDENTITY_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.properties.outputs.managedIdentityName.value')
MANAGED_IDENTITY_CLIENT_ID=$(echo $DEPLOYMENT_OUTPUT | jq -r '.properties.outputs.managedIdentityClientId.value')

echo -e "\n${GREEN}Infrastructure deployed successfully!${NC}"
echo -e "App Service: ${BLUE}$APP_SERVICE_NAME${NC}"
echo -e "Managed Identity: ${BLUE}$MANAGED_IDENTITY_NAME${NC}"

# Update script.sql with managed identity name (cross-platform compatible)
echo -e "\n${BLUE}Configuring SQL managed identity...${NC}"
cd infrastructure
cp script.sql script.sql.bak
sed -i.bak "s/MANAGED-IDENTITY-NAME/$MANAGED_IDENTITY_NAME/g" script.sql && rm -f script.sql.bak

# Install required Python packages if not already installed
echo -e "\n${BLUE}Installing Python dependencies...${NC}"
pip3 install --quiet pyodbc azure-identity

# Run the Python script to configure SQL
echo -e "\n${BLUE}Running SQL configuration script...${NC}"
python3 run-sql.py

# Restore original script.sql
mv script.sql.bak script.sql 2>/dev/null || true
cd ..

# Configure App Service settings for SQL connection
echo -e "\n${BLUE}Configuring App Service settings...${NC}"
az webapp config appsettings set \
  --resource-group $RESOURCE_GROUP \
  --name $APP_SERVICE_NAME \
  --settings \
    "ConnectionStrings__ExpenseDb=Server=tcp:sql-expense-mgmt-xyz.database.windows.net,1433;Initial Catalog=ExpenseManagementDB;Authentication=Active Directory Managed Identity;User Id=$MANAGED_IDENTITY_CLIENT_ID;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;" \
  --output table

# Deploy the application
if [ -f "app.zip" ]; then
  echo -e "\n${BLUE}Deploying application code...${NC}"
  az webapp deploy \
    --resource-group $RESOURCE_GROUP \
    --name $APP_SERVICE_NAME \
    --src-path app.zip \
    --type zip \
    --output table
  
  echo -e "\n${GREEN}========================================${NC}"
  echo -e "${GREEN}Deployment completed successfully!${NC}"
  echo -e "${GREEN}========================================${NC}"
  echo -e "\nApp URL: ${BLUE}${APP_SERVICE_URL}/Index${NC}"
  echo -e "\nNote: Navigate to ${BLUE}/Index${NC} to view the application"
else
  echo -e "\n${RED}Warning: app.zip not found. Please build and zip the application first.${NC}"
  echo -e "After building, run: ${BLUE}./deploy.sh${NC} again"
fi

echo -e "\n${BLUE}Deployment script completed!${NC}"
