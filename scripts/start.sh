#!/bin/bash

# ===========================================
# PostXAgent Start Script
# ===========================================

set -e

echo "🚀 Starting PostXAgent..."

# Check if .env exists
if [ ! -f .env ]; then
    echo "❌ Error: .env file not found!"
    echo "Please copy .env.example to .env and configure it."
    exit 1
fi

# Load environment
export $(cat .env | grep -v '^#' | xargs)

# Check required variables
required_vars=("DB_PASSWORD" "STRIPE_KEY" "STRIPE_SECRET")
for var in "${required_vars[@]}"; do
    if [ -z "${!var}" ]; then
        echo "❌ Error: $var is not set in .env"
        exit 1
    fi
done

# Start services
echo "📦 Starting Docker containers..."
docker-compose up -d

echo "⏳ Waiting for services to be ready..."
sleep 10

# Check services
echo "🔍 Checking service health..."

# Check PostgreSQL
until docker-compose exec -T postgres pg_isready -U postgres; do
    echo "Waiting for PostgreSQL..."
    sleep 2
done
echo "✅ PostgreSQL is ready"

# Check Redis
until docker-compose exec -T redis redis-cli ping; do
    echo "Waiting for Redis..."
    sleep 2
done
echo "✅ Redis is ready"

# Run Laravel migrations
echo "📊 Running database migrations..."
docker-compose exec -T laravel php artisan migrate --force

# Clear caches
echo "🧹 Clearing caches..."
docker-compose exec -T laravel php artisan config:cache
docker-compose exec -T laravel php artisan route:cache
docker-compose exec -T laravel php artisan view:cache

# Pull Ollama model (if not exists)
echo "🤖 Setting up Ollama..."
docker-compose exec -T ollama ollama pull llama2 || true

echo ""
echo "✅ PostXAgent is now running!"
echo ""
echo "🌐 API: http://localhost/api"
echo "📊 Grafana: http://localhost:3000"
echo ""
echo "To view logs: docker-compose logs -f"
echo "To stop: docker-compose down"
