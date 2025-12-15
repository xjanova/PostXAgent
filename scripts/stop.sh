#!/bin/bash

# ===========================================
# PostXAgent Stop Script
# ===========================================

echo "🛑 Stopping PostXAgent..."

docker-compose down

echo "✅ PostXAgent stopped"
