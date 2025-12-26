#!/bin/bash

GREEN='\033[0;32m'
BLUE='\033[0;34m'
NC='\033[0m'

echo "================================================"
echo "PostXAgent - Auto Start Script"
echo "================================================"
echo ""
echo "Starting all services..."
echo ""

# Cleanup function
cleanup() {
    echo ""
    echo "Stopping services..."
    kill $VITE_PID $LARAVEL_PID 2>/dev/null
    echo "Services stopped."
    exit 0
}

# Trap Ctrl+C
trap cleanup INT TERM

cd laravel-backend

# Start Vite dev server
echo "[1/3] Starting Vite dev server..."
npm run dev > /tmp/vite.log 2>&1 &
VITE_PID=$!
sleep 3

# Start Laravel server
echo "[2/3] Starting Laravel server..."
php artisan serve > /tmp/laravel.log 2>&1 &
LARAVEL_PID=$!
sleep 2

# Optional: Start AI Manager
# echo "[3/3] Starting AI Manager..."
# cd ../AIManagerCore/src/AIManager.UI
# dotnet run > /tmp/aimanager.log 2>&1 &
# AI_MANAGER_PID=$!
# cd ../../../laravel-backend

cd ..

echo ""
echo "================================================"
echo -e "${GREEN}All Services Started! 🚀${NC}"
echo "================================================"
echo ""
echo "Services running:"
echo -e "${BLUE}- Vite Dev Server: http://localhost:5173${NC}"
echo -e "${BLUE}- Laravel Server:  http://localhost:8000${NC}"
echo ""
echo -e "${GREEN}Setup Wizard: http://localhost:8000/setup${NC}"
echo ""
echo "Logs:"
echo "- Vite:    tail -f /tmp/vite.log"
echo "- Laravel: tail -f /tmp/laravel.log"
echo ""
echo "Press Ctrl+C to stop all services..."

# Wait for processes
wait $VITE_PID $LARAVEL_PID
