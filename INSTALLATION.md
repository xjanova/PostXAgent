# PostXAgent - Installation Guide

## Quick Start (Automated Installation)

### Windows

1. **Run as Administrator** - Right-click `install.bat` and select "Run as administrator"

```cmd
install.bat
```

2. **Start Services**

```cmd
start.bat
```

3. **Open Setup Wizard**
   - Go to: http://localhost:8000/setup
   - Follow the setup wizard steps

### Linux/macOS

1. **Make scripts executable**

```bash
chmod +x install.sh start.sh
```

2. **Run installation**

```bash
./install.sh
```

3. **Start services**

```bash
./start.sh
```

4. **Open Setup Wizard**
   - Go to: http://localhost:8000/setup
   - Follow the setup wizard steps

---

## Prerequisites

Before running the installation, make sure you have:

- **PHP 8.2+** - [Download](https://www.php.net/downloads.php)
- **Composer** - [Download](https://getcomposer.org/download/)
- **Node.js 18+** - [Download](https://nodejs.org/)
- **MySQL/PostgreSQL** (optional, can use SQLite)
- **.NET SDK 8.0+** (optional, for AI Manager) - [Download](https://dotnet.microsoft.com/download)

### Verify Installation

```bash
php -v        # Should show PHP 8.2 or higher
composer -V   # Should show Composer version
node -v       # Should show Node.js v18 or higher
npm -v        # Should show npm version
dotnet --version  # Should show .NET SDK 8.0 or higher (optional)
```

---

## Manual Installation (Advanced)

If the automated scripts don't work, follow these steps:

### 1. Laravel Backend Setup

```bash
cd laravel-backend

# Install PHP dependencies
composer install

# Install JavaScript dependencies
npm install

# Build frontend assets
npm run build

# Setup environment
cp .env.example .env
php artisan key:generate

# Create storage directories
mkdir -p storage/framework/{sessions,views,cache}
chmod -R 775 storage bootstrap/cache
```

### 2. Configure Database

Edit `laravel-backend/.env`:

```env
DB_CONNECTION=mysql
DB_HOST=127.0.0.1
DB_PORT=3306
DB_DATABASE=postxagent
DB_USERNAME=root
DB_PASSWORD=your_password
```

### 3. Run Migrations (After Setup Wizard)

The setup wizard will handle this, but if needed manually:

```bash
php artisan migrate
```

### 4. Start Development Servers

**Terminal 1 - Vite Dev Server:**
```bash
cd laravel-backend
npm run dev
```

**Terminal 2 - Laravel Server:**
```bash
cd laravel-backend
php artisan serve
```

### 5. (Optional) Build AI Manager

```bash
cd AIManagerCore
dotnet build --configuration Release
```

---

## Setup Wizard

The first time you access the application, you'll be redirected to the **Setup Wizard**:

### Step 1: Welcome
- Overview of features
- System requirements check

### Step 2: Database Configuration
- Choose database type (MySQL, PostgreSQL, SQLite)
- Enter connection details
- Test connection
- Auto-run migrations

### Step 3: AI Providers
- Configure Ollama (local AI) - Recommended
- Configure Google Gemini (optional)
- Configure OpenAI GPT-4 (optional)
- Configure Anthropic Claude (optional)

### Step 4: Completion
- Review settings
- Quick tips for getting started

---

## Resetting Setup

If you need to run the setup wizard again:

```bash
# Via API (local environment only)
curl -X POST http://localhost:8000/setup/reset

# Or delete the setup file manually
rm laravel-backend/storage/setup-complete.json
```

---

## Troubleshooting

### Error: "Vite manifest not found"

**Solution:** Run the Vite dev server or build assets:

```bash
cd laravel-backend
npm run dev   # For development
# OR
npm run build # For production
```

### Error: "Class 'FirstRunService' not found"

**Solution:** Clear and rebuild autoload files:

```bash
cd laravel-backend
composer dump-autoload
php artisan config:clear
php artisan cache:clear
```

### Error: "Access denied for user"

**Solution:** Check your database credentials in `.env` file and make sure MySQL/PostgreSQL is running.

### Error: "Permission denied" (Linux/macOS)

**Solution:** Fix permissions:

```bash
chmod -R 775 laravel-backend/storage
chmod -R 775 laravel-backend/bootstrap/cache
```

### Port already in use

**Solution:** Kill existing processes:

```bash
# Windows
netstat -ano | findstr :8000
taskkill /PID <PID> /F

# Linux/macOS
lsof -ti:8000 | xargs kill -9
```

---

## Services URLs

After installation:

| Service | URL | Description |
|---------|-----|-------------|
| **Setup Wizard** | http://localhost:8000/setup | First-run setup |
| **Web Dashboard** | http://localhost:8000 | Main application |
| **API** | http://localhost:8000/api | REST API |
| **AI Manager** | http://localhost:5000 | C# AI Manager API (optional) |
| **Vite Dev Server** | http://localhost:5173 | Frontend hot reload (dev only) |

---

## What Gets Installed

### Laravel Backend (`laravel-backend/`)
- PHP dependencies via Composer
- JavaScript dependencies via npm
- Vue.js 3 + Vite
- Tailwind CSS
- Database migrations
- Setup wizard

### AI Manager (`AIManagerCore/`)
- .NET 8.0 application
- WPF UI (Windows)
- Worker services
- AI orchestration

---

## Next Steps

After installation:

1. ✅ Configure social media accounts in Settings
2. ✅ Test AI providers in AI Providers page
3. ✅ Create your first campaign
4. ✅ Generate AI content
5. ✅ Schedule posts

---

## Support

If you encounter issues:

1. Check the logs:
   - Laravel: `laravel-backend/storage/logs/laravel.log`
   - Vite: Console output
   - AI Manager: Windows Event Viewer

2. Clear all caches:
   ```bash
   php artisan cache:clear
   php artisan config:clear
   php artisan route:clear
   php artisan view:clear
   ```

3. Reinstall dependencies:
   ```bash
   rm -rf node_modules vendor
   npm install
   composer install
   ```

---

**Version:** 1.0.0
**Last Updated:** December 2025
