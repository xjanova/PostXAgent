# Contributing to PostXAgent

ขอบคุณที่สนใจมีส่วนร่วมกับ PostXAgent! 🎉

Thank you for your interest in contributing to PostXAgent!

## Getting Started

### Prerequisites

- PHP 8.2+
- Composer
- Node.js 20+
- .NET 8.0 SDK
- Redis 7+
- MySQL 8.0+ or PostgreSQL

### Local Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/xjanova/PostXAgent.git
   cd PostXAgent
   ```

2. **Set up Laravel Backend**
   ```bash
   cd laravel-backend
   composer install
   npm install
   cp .env.example .env
   php artisan key:generate
   php artisan migrate
   npm run dev
   ```

3. **Set up AI Manager Core**
   ```bash
   cd AIManagerCore
   dotnet restore
   dotnet build
   ```

## How to Contribute

### Reporting Bugs

1. Check if the bug has already been reported in [Issues](https://github.com/xjanova/PostXAgent/issues)
2. If not, create a new issue using the Bug Report template
3. Provide as much detail as possible

### Suggesting Features

1. Check existing [Feature Requests](https://github.com/xjanova/PostXAgent/labels/enhancement)
2. Create a new issue using the Feature Request template
3. Explain the problem and proposed solution

### Submitting Pull Requests

1. **Fork the repository**
2. **Create a feature branch**
   ```bash
   git checkout -b feature/your-feature-name
   ```
3. **Make your changes**
4. **Run tests**
   ```bash
   # Laravel
   cd laravel-backend && php artisan test

   # .NET
   cd AIManagerCore && dotnet test
   ```
5. **Commit with conventional commits**
   ```bash
   git commit -m "feat: add new feature"
   git commit -m "fix: resolve issue with X"
   ```
6. **Push and create PR**
   ```bash
   git push origin feature/your-feature-name
   ```

## Code Style

### PHP/Laravel
- Follow PSR-12 coding standards
- Use `declare(strict_types=1)`
- Add type hints to all methods
- Use Laravel conventions

### C#/.NET
- Follow Microsoft C# coding conventions
- Use nullable reference types
- Use async/await properly
- Use dependency injection

### Vue.js
- Use Composition API with `<script setup>`
- Use scoped styles
- Follow Vue.js style guide

## Commit Messages

We use [Conventional Commits](https://www.conventionalcommits.org/):

- `feat:` - New feature
- `fix:` - Bug fix
- `docs:` - Documentation changes
- `style:` - Code style changes (formatting, etc.)
- `refactor:` - Code refactoring
- `test:` - Adding or updating tests
- `chore:` - Maintenance tasks

## Testing

- Write tests for new features
- Maintain minimum 70% code coverage
- All tests must pass before merging

## Documentation

- Update documentation for new features
- Add code comments where necessary
- Keep README.md up to date

## Questions?

- Create a [Question issue](https://github.com/xjanova/PostXAgent/issues/new?template=question.yml)
- Start a [Discussion](https://github.com/xjanova/PostXAgent/discussions)

## Code of Conduct

Be respectful and inclusive. We welcome contributions from everyone.

---

Thank you for contributing! 🙏
