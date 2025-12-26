#!/bin/bash

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo "================================================"
echo "PostXAgent - Auto Installation Script"
echo "================================================"
echo ""

# Check prerequisites
echo "[1/8] Checking prerequisites..."
echo ""

# Check PHP
if ! command -v php &> /dev/null; then
    echo -e "${RED}[ERROR] PHP is not installed${NC}"
    echo "Please install PHP 8.2 or higher"
    exit 1
fi
echo -e "${GREEN}[OK] PHP found${NC}"

# Check Composer
if ! command -v composer &> /dev/null; then
    echo -e "${RED}[ERROR] Composer is not installed${NC}"
    echo "Please install Composer from https://getcomposer.org/"
    exit 1
fi
echo -e "${GREEN}[OK] Composer found${NC}"

# Check Node.js
if ! command -v node &> /dev/null; then
    echo -e "${RED}[ERROR] Node.js is not installed${NC}"
    echo "Please install Node.js from https://nodejs.org/"
    exit 1
fi
echo -e "${GREEN}[OK] Node.js found${NC}"

# Check npm
if ! command -v npm &> /dev/null; then
    echo -e "${RED}[ERROR] npm is not installed${NC}"
    exit 1
fi
echo -e "${GREEN}[OK] npm found${NC}"

# Check .NET SDK
if ! command -v dotnet &> /dev/null; then
    echo -e "${YELLOW}[WARNING] .NET SDK not found${NC}"
    echo "C# AI Manager will not be available"
else
    echo -e "${GREEN}[OK] .NET SDK found${NC}"
fi

# Install Laravel dependencies
echo ""
echo "[2/8] Installing Laravel dependencies..."
cd laravel-backend || exit 1
composer install --no-interaction --prefer-dist --optimize-autoloader
if [ $? -ne 0 ]; then
    echo -e "${RED}[ERROR] Composer install failed${NC}"
    exit 1
fi
echo -e "${GREEN}[OK] Laravel dependencies installed${NC}"

# Install NPM dependencies
echo ""
echo "[3/8] Installing NPM dependencies..."
npm install
if [ $? -ne 0 ]; then
    echo -e "${RED}[ERROR] npm install failed${NC}"
    exit 1
fi
echo -e "${GREEN}[OK] NPM dependencies installed${NC}"

# Build frontend
echo ""
echo "[4/8] Building frontend assets..."
npm run build
if [ $? -ne 0 ]; then
    echo -e "${RED}[ERROR] npm run build failed${NC}"
    exit 1
fi
echo -e "${GREEN}[OK] Frontend assets built${NC}"

# Setup .env
echo ""
echo "[5/8] Setting up environment file..."
if [ ! -f .env ]; then
    if [ -f .env.example ]; then
        cp .env.example .env
        echo -e "${GREEN}[OK] .env file created${NC}"
    else
        echo -e "${RED}[ERROR] .env.example not found${NC}"
        exit 1
    fi
else
    echo -e "${GREEN}[OK] .env file already exists${NC}"
fi

# Generate key
echo ""
echo "[6/8] Generating application key..."
php artisan key:generate --force
if [ $? -ne 0 ]; then
    echo -e "${RED}[ERROR] Failed to generate key${NC}"
    exit 1
fi
echo -e "${GREEN}[OK] Application key generated${NC}"

# Create storage directories
echo ""
echo "[7/8] Creating storage directories..."
mkdir -p storage/app/public
mkdir -p storage/framework/{cache,sessions,views}
mkdir -p storage/logs
chmod -R 775 storage
chmod -R 775 bootstrap/cache
echo -e "${GREEN}[OK] Storage directories created${NC}"

# Build C# (optional)
echo ""
echo "[8/8] Building C# AI Manager (optional)..."
cd ../AIManagerCore
if command -v dotnet &> /dev/null; then
    dotnet build --configuration Release > /dev/null 2>&1
    if [ $? -ne 0 ]; then
        echo -e "${YELLOW}[WARNING] C# build failed${NC}"
    else
        echo -e "${GREEN}[OK] C# AI Manager built${NC}"
    fi
else
    echo -e "${YELLOW}[SKIPPED] .NET SDK not installed${NC}"
fi

cd ..

echo ""
echo "================================================"
echo -e "${GREEN}Installation Complete! 🎉${NC}"
echo "================================================"
echo ""
echo "Next steps:"
echo "1. Configure database in: laravel-backend/.env"
echo "2. Start Laravel: cd laravel-backend && php artisan serve"
echo "3. Visit: http://localhost:8000/setup"
echo ""
echo "The setup wizard will guide you through the rest!"
echo ""
